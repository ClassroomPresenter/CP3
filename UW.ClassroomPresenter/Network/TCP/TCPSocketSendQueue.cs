using System;
using System.Collections.Generic;
using System.Text;
using System.Collections;
using System.Net.Sockets;
using System.Diagnostics;
using UW.ClassroomPresenter.Model;

namespace UW.ClassroomPresenter.Network.TCP {
    /// <summary>
    /// Keep a queue of pending outbound messages.  Beyond the normal queue functions, 
    /// we support the following special features:
    /// -Dequeue accepts a list of sockets to be excluded -- in other words "dequeue 
    /// the next message for any socket that is not in the list."
    /// -DequeueAll operation supports the case where all messages for a particular 
    /// socket need to be removed from the queue and returned all at once.
    /// -Dequeues are biased in favor of pending messages without any slide designation
    /// first, followed by pending messages for the current slide, followed by messages 
    /// for other slides.
    /// -Dequeues will drop messages marked as real-time in some scenarios.
    /// </summary>
    class TCPSocketSendQueue {
        /// This is implemented with a hashtable of SocketQueues, one queue per socket.
        private Hashtable m_QueueList;          //key is a Socket, value is a SocketQueue
        private ulong m_NextSequenceNumber = 0; //The next item enqueued gets this sequence number

        public TCPSocketSendQueue() {
            m_QueueList = new Hashtable();
        }

        /// <summary>
        /// Set the SequenceNumber property for the specified item, then enqueue it.
        /// </summary>
        /// <param name="sp"></param>
        public void Enqueue(SocketSendParameters sp) {
            lock (this) {
                sp.SequenceNumber = m_NextSequenceNumber;
                m_NextSequenceNumber++;

                if (m_QueueList.ContainsKey(sp.Socket)) {
                    ((SocketQueue)m_QueueList[sp.Socket]).Enqueue(sp);
                }
                else {
                    SocketQueue sq = new SocketQueue(sp.Socket);
                    sq.Enqueue(sp);
                    m_QueueList.Add(sp.Socket, sq);
                }
                //Trace.WriteLine("Enqueue guid=" + sp.Tags.SlideID.ToString() + "; seq =" + sp.SequenceNumber.ToString(), this.GetType().ToString());
            }
        }

        /// <summary>
        /// Get the next item from the queue which is not in the provided exclusion list.  
        /// Return null if the queue is empty or if there are no qualifying items.
        /// </summary>
        /// <returns></returns>
        public SocketSendParameters Dequeue(Hashtable exclusionList, Guid currentSlide) {
            lock (this) {
                SocketQueue bestSocketQueue = null;
                SocketSendParameters bestMessage = null;
                foreach (SocketQueue sq in m_QueueList.Values) {
                    if (((exclusionList.ContainsKey(sq.Socket)) && (((ReferenceCounter)exclusionList[sq.Socket]).IsZero)) ||
                        (!exclusionList.ContainsKey(sq.Socket))) {
                        SocketSendParameters thisSp = sq.Peek(currentSlide);
                        if (thisSp != null) {
                            if (thisSp.CompareTo(bestMessage, currentSlide) < 0) {
                                bestMessage = thisSp;
                                bestSocketQueue = sq;
                            }
                        }
                    }
                }
                if (bestSocketQueue != null) {
                    //Trace.WriteLine("Dequeue: best message tag: " + bestMessage.Tags.SlideID.ToString() +
                    //    "; current slide=" + currentSlide.ToString() + 
                    //    "; seq number=" + bestMessage.SequenceNumber.ToString(), this.GetType().ToString());
                    return bestSocketQueue.Dequeue(currentSlide);
                }
            }
            return null;
        }

        /// <summary>
        /// Return all items in the queue for the given socket, and remove them from the queue.
        /// Return null if there are no enqueued items for this socket.  
        /// </summary>
        /// <param name="socket"></param>
        /// <returns></returns>
        public SocketSendParameters[] DequeueAll(Socket socket, Guid currentSlide) {
            lock (this) {
                if (m_QueueList.ContainsKey(socket)) {
                    SocketQueue sq = ((SocketQueue)m_QueueList[socket]);
                    ArrayList l = new ArrayList();
                    SocketSendParameters ssp = null;
                    while (true) {
                        ssp = sq.Dequeue(currentSlide);
                        if (ssp == null)
                            break;
                        l.Add(ssp);
                    }
                    if (l.Count == 0)
                        return null;

                    return (SocketSendParameters[])l.ToArray(typeof(SocketSendParameters));
                }
            }
            return null;
        }


        #region SocketQueue Class

        /// <summary>
        /// Queueing and prioritization for messages bound for one socket.
        /// </summary>
        /// Use a linked list to implement the main queue, and use auxiliary queues referencing
        /// linked list nodes to permit slide priority optimizations.  Drop real-time messages
        /// in network constrained scenarios.
        private class SocketQueue {
            private Socket m_Socket;
            private LinkedList<SocketSendParameters> m_List;
            private Hashtable m_SlideQueues;

            public SocketQueue(Socket s)
            {
                this.m_Socket = s;
                this.m_List = new LinkedList<SocketSendParameters>();
                this.m_SlideQueues = new Hashtable();
            }

            public Socket Socket {
                get { return m_Socket; }
            }

            public void Enqueue(SocketSendParameters sp) {
                lock (this) {
                    LinkedListNode<SocketSendParameters> node = new LinkedListNode<SocketSendParameters>(sp);
                    m_List.AddLast(node);
                    if (!m_SlideQueues.ContainsKey(sp.Tags.SlideID))
                        m_SlideQueues.Add(sp.Tags.SlideID, new SlideQueue());

                    ((SlideQueue)m_SlideQueues[sp.Tags.SlideID]).Enqueue(node);    
                }
            }

            /// <summary>
            /// First, return the next message with Guid.Empty if any.
            /// Second, return the next message for the CurrentSlide, after skipping over real-time messages (if conditions are met for this).
            /// Third, return the message with lowest sequence number for any slide, here skipping unconditionally over real-time messages.  
            /// Finally, return null if there are no messages.
            /// </summary>
            public SocketSendParameters Peek(Guid CurrentSlideID) {
                lock (this) {
                    //Next global message chunk
                    if (m_SlideQueues.ContainsKey(Guid.Empty)) {
                        if (((SlideQueue)m_SlideQueues[Guid.Empty]).Count > 0) {
                            LinkedListNode<SocketSendParameters> node = 
                                (LinkedListNode<SocketSendParameters>)((SlideQueue)m_SlideQueues[Guid.Empty]).Peek(m_List,DropRealTimeChunks.Never);
                            return node.Value;
                        }
                    }

                    //Next chunk for current slide
                    if (m_SlideQueues.ContainsKey(CurrentSlideID)) {
                        if (((SlideQueue)m_SlideQueues[CurrentSlideID]).Count > 0) {
                            LinkedListNode<SocketSendParameters> node = 
                                (LinkedListNode<SocketSendParameters>)((SlideQueue)m_SlideQueues[CurrentSlideID]).Peek(m_List,DropRealTimeChunks.Sometimes);
                            return node.Value;
                        }
                    }

                    //Next chunk for any slide, which is not a real-time chunk.
                    while ((m_List.First != null) && (m_List.First.Value.Tags.Priority == MessagePriority.RealTime)) {
                        ((SlideQueue)m_SlideQueues[m_List.First.Value.Tags.SlideID]).Dequeue(m_List, DropRealTimeChunks.Never);
                        m_List.RemoveFirst();
                    }
                    if (m_List.First != null) {
                        return m_List.First.Value;
                    }

                    return null;
                }
            }

            /// <summary>
            /// First, return the next message with Guid.Empty if any.
            /// Second, return the next message for the CurrentSlide, after skipping over real-time messages (if conditions are met for this).
            /// Third, return the message with lowest sequence number for any slide, here skipping unconditionally over real-time messages.  
            /// Finally, return null if there are no messages.
            /// Notice that we could return a different item than Peek did if new items were added between the Peek and the Dequeue, 
            /// but if we do return a different item, that item will always have higher priority.
            /// </summary>
            public SocketSendParameters Dequeue(Guid CurrentSlideID) {
                lock (this) { 
                    if (m_SlideQueues.ContainsKey(Guid.Empty)) {
                        if (((SlideQueue)m_SlideQueues[Guid.Empty]).Count > 0) {
                            LinkedListNode<SocketSendParameters> node = 
                                (LinkedListNode<SocketSendParameters>)((SlideQueue)m_SlideQueues[Guid.Empty]).Dequeue(m_List,DropRealTimeChunks.Never);
                            m_List.Remove(node);
                            return node.Value;
                        }
                    }

                    if (m_SlideQueues.ContainsKey(CurrentSlideID)) {
                        if (((SlideQueue)m_SlideQueues[CurrentSlideID]).Count > 0) {
                            LinkedListNode<SocketSendParameters> node = 
                                (LinkedListNode<SocketSendParameters>)((SlideQueue)m_SlideQueues[CurrentSlideID]).Dequeue(m_List,DropRealTimeChunks.Sometimes);
                            m_List.Remove(node);
                            return node.Value;
                        }
                    }

                    //Next chunk for any slide, which is not a real-time chunk.
                    while ((m_List.First != null) && (m_List.First.Value.Tags.Priority == MessagePriority.RealTime)) {
                        ((SlideQueue)m_SlideQueues[m_List.First.Value.Tags.SlideID]).Dequeue(m_List, DropRealTimeChunks.Never);
                        m_List.RemoveFirst();
                    }

                    if (m_List.First != null) {
                        LinkedListNode<SocketSendParameters> node = m_List.First;
                        m_List.RemoveFirst();
                        ((SlideQueue)m_SlideQueues[node.Value.Tags.SlideID]).Dequeue(m_List,DropRealTimeChunks.Never);
                        return node.Value;
                    }

                    return null;     
                }
            }

            #region SlideQueue Class


            private class SlideQueue {
                private Queue m_Queue;
                private int m_NonRealTimeChunkCount;

                /// <summary>
                /// When more than this number of items are enqueued we drop any items that have
                /// priority marked as real-time.
                /// </summary>
                private const int DROP_REALTIME_THRESHOLD = 3;

                public SlideQueue() { 
                    m_Queue = new Queue();
                    m_NonRealTimeChunkCount = 0;
                }

                public int Count {
                    get { lock (this) { return m_Queue.Count; } }
                }

                public void Enqueue(LinkedListNode<SocketSendParameters> node) {
                    lock (this) {
                        m_Queue.Enqueue(node);
                        if (node.Value.Tags.Priority != MessagePriority.RealTime)
                            m_NonRealTimeChunkCount++;
                    }
                }

                /// <summary>
                /// Return the next item from the queue, dropping real-time items as requested.  
                /// Any items that we drop will also be removed from the 
                /// Linked list.  The item returned will not be removed from the Linked list, 
                /// so it must be removed by the caller.
                /// Return null if the queue is empty.
                /// </summary>
                /// <param name="list"></param>
                /// <param name="drop"></param>
                /// <returns></returns>
                public LinkedListNode<SocketSendParameters> Dequeue (LinkedList<SocketSendParameters> list, DropRealTimeChunks drop) {
                    lock (this) {
                        if (this.m_Queue.Count > 0) {

                            LinkedListNode<SocketSendParameters> node = (LinkedListNode<SocketSendParameters>)this.m_Queue.Dequeue();

                            if (node.Value.Tags.Priority != MessagePriority.RealTime) {
                                //If item is not real-time, we're done.
                                m_NonRealTimeChunkCount--;
                            }
                            else if ((drop==DropRealTimeChunks.Sometimes) &&
                                    (this.m_Queue.Count >= DROP_REALTIME_THRESHOLD) &&
                                    (m_NonRealTimeChunkCount > 0)) {
                                //Here we drop real-time items when we are over a queue length
                                //threshold, but only if there is at least one
                                //non-real-time item in the queue.
                                while (node.Value.Tags.Priority == MessagePriority.RealTime) {
                                    Trace.WriteLine("Dropping Real-Time Chunk", this.GetType().ToString());
                                    list.Remove(node);
                                    node = (LinkedListNode<SocketSendParameters>)m_Queue.Dequeue();
                                }
                                m_NonRealTimeChunkCount--;
                            }
                            else if (drop == DropRealTimeChunks.Always) { 
                                //Here we drop real-time items unconditionally until we come to
                                //a non-real-time item, or until the queue is empty.
                                while (node.Value.Tags.Priority == MessagePriority.RealTime) {
                                    Trace.WriteLine("Dropping Real-Time Chunk", this.GetType().ToString());
                                    list.Remove(node);
                                    if (m_Queue.Count > 0) {
                                        node = (LinkedListNode<SocketSendParameters>)m_Queue.Dequeue();
                                    }
                                    else {
                                        node = null;
                                        break;
                                    }
                                }
                                if (node != null)
                                    m_NonRealTimeChunkCount--;   
                            }
                            return node;
                        }
                    }
                    return null;
                }


                public LinkedListNode<SocketSendParameters> Peek (LinkedList<SocketSendParameters> list, DropRealTimeChunks drop) {
                    lock (this) {
                        if (this.m_Queue.Count > 0) {

                            LinkedListNode<SocketSendParameters> node = (LinkedListNode<SocketSendParameters>)this.m_Queue.Peek();

                            if (node.Value.Tags.Priority != MessagePriority.RealTime) {
                                //If item is not real-time, we're done.
                            }
                            else if ((drop == DropRealTimeChunks.Sometimes) &&
                                    (this.m_Queue.Count >= DROP_REALTIME_THRESHOLD) &&
                                    (m_NonRealTimeChunkCount > 0)) {
                                //Here we drop real-time items when we are over a queue length
                                //threshold, but only if there is at least one
                                //non-real-time item in the queue.
                                while (node.Value.Tags.Priority == MessagePriority.RealTime) {
                                    Trace.WriteLine("Dropping Real-Time Chunk", this.GetType().ToString());
                                    node = (LinkedListNode<SocketSendParameters>)m_Queue.Dequeue();
                                    list.Remove(node);
                                    node = (LinkedListNode<SocketSendParameters>)m_Queue.Peek();
                                }
                            }
                            else if (drop == DropRealTimeChunks.Always) {
                                //Here we drop real-time items unconditionally until we come to
                                //a non-real-time item, or until the queue is empty.
                                while (node.Value.Tags.Priority == MessagePriority.RealTime) {
                                    Trace.WriteLine("Dropping Real-Time Chunk", this.GetType().ToString());
                                    node = (LinkedListNode<SocketSendParameters>)m_Queue.Dequeue();
                                    list.Remove(node);
                                    if (m_Queue.Count > 0) {
                                        node = (LinkedListNode<SocketSendParameters>)m_Queue.Peek();
                                    }
                                    else {
                                        node = null;
                                        break;
                                    }
                                }
                            }
                            return node;
                        }
                    }
                    return null;
                }
            }

            #endregion SlideQueue Class

            private enum DropRealTimeChunks { 
                Always,Sometimes,Never
            }
        }


        #endregion SocketQueue Class

    }
}
