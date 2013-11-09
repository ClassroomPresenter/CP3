using System;
using System.Collections.Generic;
using System.Text;
using System.Collections;
using System.Net.Sockets;
using System.Diagnostics;
using UW.ClassroomPresenter.Model;
using UW.ClassroomPresenter.Model.Network;
using UW.ClassroomPresenter.Network.Groups;

namespace UW.ClassroomPresenter.Network.TCP {
    /// <summary>
    /// Keep a queue of pending outbound messages.  Beyond the normal queue functions, 
    /// we support the following special features:
    /// -Dequeue accepts a list of receivers to be excluded -- in other words "dequeue 
    /// the next message for any client that is not in the list."
    /// -Dequeues are biased in favor of pending messages without any slide designation
    /// first, followed by pending messages for the current slide, followed by messages 
    /// for other slides.
    /// -Dequeues are biased in favor of messages bound for public display nodes.
    /// -Dequeues will drop messages marked as real-time in some scenarios.
    /// -We support enabling and disabling client queues.  Dequeue will never return a message 
    /// from a disabled client queue, but messages can still be added to the queue.
    /// -When disabled clients are re-enabled, we support a degree of look-behind buffering to recover
    /// messages that were dequeued, but which the client did not receive.
    /// </summary>
    class TCPServerSendQueue {
        private Hashtable m_QueueList;          //key is a Guid, value is a ClientQueue
        private Hashtable m_DisabledClients;    //key is a Guid.
        private ulong m_NextSequenceNumber = 0; //The next item enqueued gets this sequence number

        public TCPServerSendQueue() {
            m_QueueList = new Hashtable();
            m_DisabledClients = new Hashtable();
        }

        /// <summary>
        /// Set the SequenceNumber property for the specified item, then enqueue it.
        /// </summary>
        /// <param name="sp"></param>
        public void Enqueue(SendParameters sp, ParticipantModel participant) {
            lock (this) {
                sp.SequenceNumber = m_NextSequenceNumber;
                m_NextSequenceNumber++;

                if (m_QueueList.ContainsKey(sp.Id)) {
                    ((ClientQueue)m_QueueList[sp.Id]).Enqueue(sp);
                }
                else {
                    ClientQueue cq = new ClientQueue(sp.Id, participant);
                    cq.Enqueue(sp);
                    m_QueueList.Add(sp.Id, cq);
                }
                //Trace.WriteLine("Enqueue guid=" + sp.Tags.SlideID.ToString() + "; seq =" + sp.SequenceNumber.ToString(), this.GetType().ToString());
            }
        }

        /// <summary>
        /// clean up a queue for one client.
        /// </summary>
        /// <param name="id"></param>
        public void RemoveQueue(Guid id) {
            Trace.WriteLine("TCPServerSendQueue Removing Send Queue for client id=" + id.ToString(), this.GetType().ToString());
            lock (this) { 
                if (m_QueueList.ContainsKey(id)) {
                    m_QueueList.Remove(id);
                }
            }
        }

        /// <summary>
        /// Enable dequeue for a client's queue
        /// </summary>
        /// <param name="client"></param>
        public void EnableClient(ClientData client, ulong lastMessage, ulong lastChunk) {
            Trace.WriteLine("TCPServerSendQueue Enable client id=" + client.Id.ToString(), this.GetType().ToString());
            lock (this) {
                if (m_DisabledClients.ContainsKey(client.Id)) {
                    m_DisabledClients.Remove(client.Id);
                }
                if (m_QueueList.ContainsKey(client.Id)) {
                    ((ClientQueue)m_QueueList[client.Id]).RecoverMessages(lastMessage, lastChunk);
                    ((ClientQueue)m_QueueList[client.Id]).UpdateParticipant(client.Participant);
                }
            }
        }

        /// <summary>
        /// Disable dequeue for a client's queue
        /// </summary>
        /// <param name="id"></param>
        public void DisableClient(Guid id) {
            Trace.WriteLine("TCPServerSendQueue Disable client id=" + id.ToString(), this.GetType().ToString());
            lock (this) {
                if (!m_DisabledClients.ContainsKey(id)) {
                    m_DisabledClients.Add(id, null);
                }
            }
        }

        /// <summary>
        /// Get the next item from the queue which is not in the provided exclusion list.  
        /// Return null if the queue is empty or if there are no qualifying items.
        /// </summary>
        /// <returns></returns>
        public SendParameters Dequeue(Hashtable exclusionList, Guid currentSlide) {
            lock (this) {
                ClientQueue bestClient = null;
                SendParameters bestMessage = null;
                foreach (ClientQueue cq in m_QueueList.Values) {
                    //Block student messages when sending public messages.
                    if ((exclusionList.ContainsKey(cq.Id)) && (cq.IsPublicNode) && !((ReferenceCounter)exclusionList[cq.Id]).IsZero)
                        return cq.Dequeue(currentSlide);
                    
                    if (m_DisabledClients.ContainsKey(cq.Id))
                        continue;
                    if (((exclusionList.ContainsKey(cq.Id)) && (((ReferenceCounter)exclusionList[cq.Id]).IsZero)) ||
                        (!exclusionList.ContainsKey(cq.Id))) {
                        SendParameters thisSp = cq.Peek(currentSlide);
                        if (thisSp != null) {
                            if (thisSp.CompareTo(bestMessage, currentSlide) < 0) {
                                bestMessage = thisSp;
                                bestClient = cq;
                            }
                        }
                    }
                }
                if (bestClient != null) {
                    //Trace.WriteLine("Dequeue: best message tag: " + bestMessage.Tags.SlideID.ToString() +
                    //    "; current slide=" + currentSlide.ToString() + 
                    //    "; seq number=" + bestMessage.SequenceNumber.ToString(), this.GetType().ToString());
                    return bestClient.Dequeue(currentSlide);
                }
            }
            return null;
        }

        #region ClientQueue Class

        /// <summary>
        /// Queueing and prioritization for messages bound for one client.
        /// </summary>
        /// Use a linked list to implement the main queue, and use auxiliary queues referencing
        /// linked list nodes to permit slide priority optimizations.  Drop real-time messages
        /// in network constrained scenarios.
        private class ClientQueue: IDisposable {
            private Guid m_Id;
            private LinkedList<SendParameters> m_List;
            private Hashtable m_SlideQueues;
            private ParticipantModel m_Participant;
            private bool m_IsPublicNode;
            private ReconnectBuffer m_ReconnectBuffer;

            public ClientQueue(Guid id, ParticipantModel participant)
            {
                m_Participant = participant;
                m_IsPublicNode = false;
                m_Participant.Changed["Role"].Add(new PropertyEventHandler(OnRoleChanged));
                using (Synchronizer.Lock(m_Participant.SyncRoot)) {
                    if (m_Participant.Groups.Contains(Group.AllPublic)) {
                        m_IsPublicNode = true;
                    }
                }

                this.m_Id = id;
                this.m_List = new LinkedList<SendParameters>();
                this.m_SlideQueues = new Hashtable();
                this.m_ReconnectBuffer = new ReconnectBuffer();
            }

            private void OnRoleChanged(object sender, PropertyEventArgs args) {
                PropertyChangeEventArgs pcargs = (PropertyChangeEventArgs)args;
                if (pcargs.NewValue is PublicModel) {
                    m_IsPublicNode = true;
                }
                else {
                    m_IsPublicNode = false;
                }
            }

            /// <summary>
            /// After a client reconnect, we may have a new ParticipantModel, so resubscribe for Role changes.
            /// </summary>
            /// <param name="participant"></param>
            internal void UpdateParticipant(ParticipantModel participant) {
                this.m_Participant.Changed["Role"].Remove(new PropertyEventHandler(OnRoleChanged));
                m_Participant = participant;
                m_IsPublicNode = false;
                m_Participant.Changed["Role"].Add(new PropertyEventHandler(OnRoleChanged));
                using (Synchronizer.Lock(m_Participant.SyncRoot)) {
                    if (m_Participant.Groups.Contains(Group.AllPublic)) {
                        m_IsPublicNode = true;
                    }
                }
            }

            public Guid Id {
                get { return m_Id; }
            }

            public bool IsPublicNode {
                get { return m_IsPublicNode; }
            }
            /// <summary>
            /// Undo a dequeue operation.
            /// </summary>
            /// <param name="sp"></param>
            private void Requeue(SendParameters sp) {
                lock (this) { 
                    LinkedListNode<SendParameters> newNode = new LinkedListNode<SendParameters>(sp);

                    LinkedListNode<SendParameters> n = m_List.First;
                    while (true) {
                        if (n == null) {
                            m_List.AddLast(newNode);
                            break;
                        }
                        if (newNode.Value.SequenceNumber < n.Value.SequenceNumber) {
                            m_List.AddBefore(n, newNode);
                            break;
                        }
                        n = n.Next;
                    }

                    if (!m_SlideQueues.ContainsKey(sp.Tags.SlideID))
                        m_SlideQueues.Add(sp.Tags.SlideID, new SlideQueue());

                    ((SlideQueue)m_SlideQueues[sp.Tags.SlideID]).Requeue(newNode);                     
                }
            }

            public void Enqueue(SendParameters sp) {
                lock (this) {
                    LinkedListNode<SendParameters> node = new LinkedListNode<SendParameters>(sp);
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
            public SendParameters Peek(Guid CurrentSlideID) {
                lock (this) {
                    //Next global message chunk
                    if (m_SlideQueues.ContainsKey(Guid.Empty)) {
                        if (((SlideQueue)m_SlideQueues[Guid.Empty]).Count > 0) {
                            LinkedListNode<SendParameters> node = 
                                (LinkedListNode<SendParameters>)((SlideQueue)m_SlideQueues[Guid.Empty]).Peek(m_List,DropRealTimeChunks.Never);
                            node.Value.IsPublicNode = m_IsPublicNode;
                            return node.Value;
                        }
                    }

                    //Next chunk for current slide
                    if (m_SlideQueues.ContainsKey(CurrentSlideID)) {
                        if (((SlideQueue)m_SlideQueues[CurrentSlideID]).Count > 0) {
                            LinkedListNode<SendParameters> node = 
                                (LinkedListNode<SendParameters>)((SlideQueue)m_SlideQueues[CurrentSlideID]).Peek(m_List,DropRealTimeChunks.Sometimes);
                            node.Value.IsPublicNode = m_IsPublicNode;
                            return node.Value;
                        }
                    }

                    //Next chunk for any slide, which is not a real-time chunk.
                    while ((m_List.First != null) && (m_List.First.Value.Tags.Priority == MessagePriority.RealTime)) {
                        ((SlideQueue)m_SlideQueues[m_List.First.Value.Tags.SlideID]).Dequeue(m_List, DropRealTimeChunks.Never);
                        m_List.RemoveFirst();
                    }
                    if (m_List.First != null) {
                        m_List.First.Value.IsPublicNode = m_IsPublicNode;
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
            public SendParameters Dequeue(Guid CurrentSlideID) {
                lock (this) { 
                    if (m_SlideQueues.ContainsKey(Guid.Empty)) {
                        if (((SlideQueue)m_SlideQueues[Guid.Empty]).Count > 0) {
                            LinkedListNode<SendParameters> node = 
                                (LinkedListNode<SendParameters>)((SlideQueue)m_SlideQueues[Guid.Empty]).Dequeue(m_List,DropRealTimeChunks.Never);
                            m_List.Remove(node);
                            node.Value.IsPublicNode = m_IsPublicNode;
                            m_ReconnectBuffer.Add(node);
                            return node.Value;
                        }
                    }

                    if (m_SlideQueues.ContainsKey(CurrentSlideID)) {
                        if (((SlideQueue)m_SlideQueues[CurrentSlideID]).Count > 0) {
                            LinkedListNode<SendParameters> node = 
                                (LinkedListNode<SendParameters>)((SlideQueue)m_SlideQueues[CurrentSlideID]).Dequeue(m_List,DropRealTimeChunks.Sometimes);
                            m_List.Remove(node);
                            node.Value.IsPublicNode = m_IsPublicNode;
                            m_ReconnectBuffer.Add(node);
                            return node.Value;
                        }
                    }

                    //Next chunk for any slide, which is not a real-time chunk.
                    while ((m_List.First != null) && (m_List.First.Value.Tags.Priority == MessagePriority.RealTime)) {
                        ((SlideQueue)m_SlideQueues[m_List.First.Value.Tags.SlideID]).Dequeue(m_List, DropRealTimeChunks.Never);
                        m_List.RemoveFirst();
                    }

                    if (m_List.First != null) {
                        LinkedListNode<SendParameters> node = m_List.First;
                        m_List.RemoveFirst();
                        ((SlideQueue)m_SlideQueues[node.Value.Tags.SlideID]).Dequeue(m_List,DropRealTimeChunks.Never);
                        node.Value.IsPublicNode = m_IsPublicNode;
                        m_ReconnectBuffer.Add(node);
                        return node.Value;
                    }

                    return null;     
                }
            }

            /// <summary>
            /// Retrieve lost messages from ReconnectBuffer and requeue them.
            /// </summary>
            /// <param name="lastMessage"></param>
            /// <param name="lastChunk"></param>
            internal void RecoverMessages(ulong lastMessage, ulong lastChunk) {
                SendParameters[] msgs = m_ReconnectBuffer.RecoverMessages(lastMessage, lastChunk);
                //requeue most recent first
                for (int i = 0; i < msgs.Length; i++) {
                    this.Requeue(msgs[i]);
                }
            }


            #region SlideQueue Class

            /// <summary>
            /// Queue for messages pertaining to one slide (or as a special case, to global presentation state), which 
            /// are bound for one client.
            /// </summary>
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

                public void Enqueue(LinkedListNode<SendParameters> node) {
                    //Trace.WriteLine("SlideQueue.Enqueue " + node.Value.Tags.SlideID.ToString() + " seq=" + node.Value.MessageSequence.ToString());
                    lock (this) {
                        m_Queue.Enqueue(node);
                        if (node.Value.Tags.Priority != MessagePriority.RealTime)
                            m_NonRealTimeChunkCount++;
                    }
                }

                /// <summary>
                /// Undo a dequeue.  This is not efficient, but should happen rarely. 
                /// </summary>
                /// <param name="node"></param>
                public void Requeue(LinkedListNode<SendParameters> node) {
                    lock (this) {                    
                        if (node.Value.Tags.Priority != MessagePriority.RealTime)
                            m_NonRealTimeChunkCount++;

                        if (m_Queue.Count == 0) {
                            m_Queue.Enqueue(node);
                        }
                        else {
                            Queue newQueue = new Queue();
                            LinkedListNode<SendParameters> n;
                            while (this.m_Queue.Count > 0) {
                                n = (LinkedListNode<SendParameters>)m_Queue.Dequeue();
                                if ((node != null) && (node.Value.SequenceNumber < n.Value.SequenceNumber)) {
                                    newQueue.Enqueue(node);
                                    node = null;
                                }
                                newQueue.Enqueue(n);
                            }
                            if (node != null)
                                newQueue.Enqueue(node);

                            m_Queue = newQueue;
                        }
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
                public LinkedListNode<SendParameters> Dequeue (LinkedList<SendParameters> list, DropRealTimeChunks drop) {
                    lock (this) {
                        if (this.m_Queue.Count > 0) {

                            LinkedListNode<SendParameters> node = (LinkedListNode<SendParameters>)this.m_Queue.Dequeue();

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
                                    node = (LinkedListNode<SendParameters>)m_Queue.Dequeue();
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
                                        node = (LinkedListNode<SendParameters>)m_Queue.Dequeue();
                                    }
                                    else {
                                        node = null;
                                        break;
                                    }
                                }
                                if (node != null)
                                    m_NonRealTimeChunkCount--;   
                            }
                            //Trace.WriteLine("SlideQueue.Dequeue " + node.Value.Tags.SlideID.ToString() + " seq=" + node.Value.MessageSequence.ToString());
                            return node;
                        }
                    }
                    return null;
                }


                public LinkedListNode<SendParameters> Peek (LinkedList<SendParameters> list, DropRealTimeChunks drop) {
                    lock (this) {
                        if (this.m_Queue.Count > 0) {

                            LinkedListNode<SendParameters> node = (LinkedListNode<SendParameters>)this.m_Queue.Peek();

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
                                    node = (LinkedListNode<SendParameters>)m_Queue.Dequeue();
                                    list.Remove(node);
                                    node = (LinkedListNode<SendParameters>)m_Queue.Peek();
                                }
                            }
                            else if (drop == DropRealTimeChunks.Always) {
                                //Here we drop real-time items unconditionally until we come to
                                //a non-real-time item, or until the queue is empty.
                                while (node.Value.Tags.Priority == MessagePriority.RealTime) {
                                    Trace.WriteLine("Dropping Real-Time Chunk", this.GetType().ToString());
                                    node = (LinkedListNode<SendParameters>)m_Queue.Dequeue();
                                    list.Remove(node);
                                    if (m_Queue.Count > 0) {
                                        node = (LinkedListNode<SendParameters>)m_Queue.Peek();
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

            #region IDisposable Members
        
            ~ClientQueue() {
                this.Dispose(false);
            }

            public void Dispose() {
                this.Dispose(true);
                System.GC.SuppressFinalize(this);
            }

            protected virtual void Dispose(bool disposing) {
                if (disposing) {
                    this.m_Participant.Changed["Role"].Remove(new PropertyEventHandler(OnRoleChanged));
                }
            }

            #endregion

            #region ReconnectBuffer Class

            /// <summary>
            /// Ordered list of the last N sent messages.  This is used when a client reconnects to allow us to
            /// requeue messages that were dequeued but not received.  This is needed because in some pathological
            /// network states, a number of Socket.Send operations will return with apparent success even though the 
            /// messages are never received.
            /// </summary>
            private class ReconnectBuffer {
                //Most recent messages at the head of the list
                LinkedList<SendParameters> buffer;

                /// <summary>
                /// Discard old entries as needed such that we do not grow past this size.
                /// (Typical number of entries needed is believed to be about 6, but this will
                /// vary based on message size and Socket Send Buffer size.)
                /// </summary>
                private const int MAX_SIZE = 100;  

                public ReconnectBuffer() {
                    buffer = new LinkedList<SendParameters>();
                }

                /// <summary>
                /// Add this node to the buffer.  We ignore heartbeat messages, and discard old messages
                /// if we are at MAX_SIZE.
                /// </summary>
                /// <param name="node"></param>
                public void Add(LinkedListNode<SendParameters> node) {
                    if (node.Value.IsHeartbeatMessage)
                        return;
                    if (buffer.Count == MAX_SIZE) {
                        buffer.RemoveLast();
                    }
                    buffer.AddFirst(node);
                }

                /// <summary>
                /// Search from the head of the list for the given message and chunk sequence.
                /// If a match is found, return the messages to requeue, ordered by most recent first.
                /// If no match is found, return everything in the buffer, ordered most recent first.
                /// </summary>
                /// <param name="lastMessageSeq"></param>
                /// <param name="lastChunkSeq"></param>
                /// <returns></returns>
                public SendParameters[] RecoverMessages(ulong lastMessageSeq, ulong lastChunkSeq) {
                    LinkedListNode<SendParameters> thisNode = buffer.First;
                    List<SendParameters> returnList = new List<SendParameters>();
                    while (thisNode != null) {
                        if ((thisNode.Value.MessageSequence == lastMessageSeq) &&
                            (thisNode.Value.ChunkSequence == lastChunkSeq)) {
                            break;
                        }
                        returnList.Add(thisNode.Value);
                        thisNode = thisNode.Next;
                    }

                    if (thisNode == null) {
                        Trace.WriteLine("ReconnectBuffer.RecoverMessages did not find a match.", this.GetType().ToString());
                    }
                    Trace.WriteLine("ReconnectBuffer.RecoverMessages returning " + returnList.Count.ToString() + " messages.", this.GetType().ToString());
                    //Regardless of whether there was a match, return what we got
                    return returnList.ToArray();
                }
            }

            #endregion ReconnectBuffer Class

        }

        #endregion ClientQueue Class

    }
}
