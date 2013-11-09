using System;
using System.Collections.Generic;
using System.Text;
using UW.ClassroomPresenter.Model.Network;
using System.Net.Sockets;
using UW.ClassroomPresenter.Network.Groups;
using System.Collections;
using System.Threading;
using System.Diagnostics;
using UW.ClassroomPresenter.Model;
using UW.ClassroomPresenter.Model.Presentation;
using UW.ClassroomPresenter.Network.Messages;
using UW.ClassroomPresenter.Misc;
using Microsoft.Win32;

namespace UW.ClassroomPresenter.Network.TCP {
    /// <summary>
    /// Accept send requests from the caller.  Buffer them and pass them on to the underlying sockets in such a way that 
    /// high priority clients such as public displays receive data with low latency, and messages pertaining to the global
    /// presentation or the current slide are sent before messages for other slides.
    /// </summary>
    /// We limit the total number of concurrent send operations to clients, and 
    /// permit only one concurrent send operation per client.  We use a TCPServerSendQueue to manage 
    /// message prioritization. 
    class TCPServerSender : IDisposable {
        #region Members

        TCPServerSendQueue m_SendQueue;             //The queue with priority enhancements
        bool m_Disposed = false;
        int m_PendingOutboundMessageCount = 0;      //Count of BeginSends without corresponding callbacks.
        Hashtable m_SendingParticipants;            //Key is Guid, Value is ReferenceCounter
        Hashtable m_SocketMap;                      //Key is Guid, value is Socket
        private InstructorModel m_Instructor;       //Use to get current slide for message prioritization
        private DeckTraversalModel m_DeckTraversal; // ..ditto.
        private Guid m_CurrentSlideID;              //Current slide identifier
        private Hashtable m_ClosingSockets;         //Key is a socket, value is a ManualResetEvent.
        private int m_MaxConcurrentSends;

        /// <summary>
        /// The default for the maximum number of BeginSend operations in process at any time.
        /// </summary>
        private const int MAX_CONCURRENT_SENDS = 50;

        #endregion Members

        #region Construct

        public TCPServerSender(InstructorModel instructor) {
            m_MaxConcurrentSends = MAX_CONCURRENT_SENDS;
            this.RestoreConfig();

            //Register for events to track current slide
            m_Instructor = instructor;
            m_CurrentSlideID = GetSlideID(instructor); // Get initial slide if any
            m_Instructor.Changed["CurrentDeckTraversal"].Add(new PropertyEventHandler(OnCurrentDeckTraversalChanged));

            //Create the queue
            m_SendQueue = new TCPServerSendQueue();
            //Clients currently engaged in a send operation
            m_SendingParticipants = new Hashtable();
            //For synchronizing the closing of client sockets
            m_ClosingSockets = new Hashtable();
            //Current map of Client Guid to Socket
            m_SocketMap = new Hashtable();

            ///A thread to keep data flowing from the queue:
            Thread sendThread = new Thread(new ThreadStart(SendThread));
            sendThread.Name = "TCPServer Send Thread";
            sendThread.Start();
        }

        #endregion Construct

        #region Internal Methods



        /// <summary>
        /// Enqueue the send to occur when network bandwidth is available.
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="length"></param>
        /// <param name="socket"></param>
        /// <param name="participant"></param>
        internal void Send(byte[] buffer, int length, ClientData client, MessageTags tags,
            ulong messageSequence, ulong chunkSequence, bool isHeartbeat) {
            using (Synchronizer.Lock(m_SocketMap)) {
                Debug.Assert(m_SocketMap.ContainsKey(client.Id), "TCPServerSender.Send found a missing Socket Map entry.");
            }

            //Trace.WriteLine("TCPServerSender.Send seq=" + messageSequence.ToString());

            //Enqueue the message
            using (Synchronizer.Cookie cookie = Synchronizer.Lock(m_SendQueue)) {
                m_SendQueue.Enqueue(new SendParameters(buffer, length, client.Id, tags, messageSequence, chunkSequence, isHeartbeat),
                    client.Participant);

                // Notify the sender thread that the queue's status has changed.
                cookie.PulseAll();
            }
        }

        internal void AddClient(ClientData client) {
            using (Synchronizer.Lock(m_SocketMap)) {
                Debug.Assert(!m_SocketMap.ContainsKey(client.Id), "TCPServerSender.AddClient: Attempt to add a duplicate Socket Map entry.");
                m_SocketMap.Add(client.Id, client.Socket);
            }
        }


        /// <summary>
        /// If a client reconnects, update the socket map and enable the client's queue
        /// </summary>
        /// <param name="id"></param>
        /// <param name="s"></param>
        internal void Reconnect(ClientData client, ulong lastMessage, ulong lastChunk) {
            using (Synchronizer.Lock(this.m_SocketMap)) {
                if (m_SocketMap.ContainsKey(client.Id)) {
                    m_SocketMap[client.Id] = client.Socket;
                }
            }
            using (Synchronizer.Cookie cookie = Synchronizer.Lock(this.m_SendQueue)) {
                m_SendQueue.EnableClient(client, lastMessage, lastChunk);
                cookie.PulseAll();
            }
        }

        /// <summary>
        /// If a disconnected client times out, clean up the client's queue
        /// </summary>
        /// <param name="id"></param>
        internal void RemoveQueue(Guid id) {
            using (Synchronizer.Lock(this.m_SocketMap)) {
                if (m_SocketMap.ContainsKey(id)) {
                    m_SocketMap.Remove(id);
                }
            }
            using (Synchronizer.Lock(this.m_SendQueue)) {
                m_SendQueue.RemoveQueue(id);
            }
        }


        /// <summary>
        /// Disable the queue for this client.  If there are messages on the socket's queue,
        /// Close the socket and wait for the Send Callbacks, otherwise just close 
        /// the socket.
        /// </summary>
        /// <param name="id"></param>
        /// <param name="s"></param>
        internal void ForceSocketClose(Guid id, Socket s) {
            DisableClient(id);
            ManualResetEvent waitEvent = null;

            //If messages are in the socket queue, close the socket and wait for the last SendCallback.  
            using (Synchronizer.Lock(m_SendingParticipants)) {
                if ((m_SendingParticipants.ContainsKey(id)) && (!((ReferenceCounter)m_SendingParticipants[id]).IsZero)) {
                    try {
                        waitEvent = new ManualResetEvent(false);
                        waitEvent.Reset();
                        m_ClosingSockets.Add(s, waitEvent);
                        s.Shutdown(SocketShutdown.Both);
                        s.Close();
                    }
                    catch (Exception e) {
                        Trace.WriteLine("ForceSocketClose exception while closing socket: " + e.ToString(), this.GetType().ToString());
                    }
                }
            }

            //If no messages are in the socket queue, just close the socket.
            if (waitEvent == null) {
                try {
                    s.Close();
                }
                catch (Exception e) {
                    Trace.WriteLine("ForceSocketClose exception while closing socket: " + e.ToString(), this.GetType().ToString());
                }
            }

            //Finish waiting for socket queue to clear if necessary.
            if (waitEvent != null) {
                if (!waitEvent.WaitOne(10000, true))
                    Trace.WriteLine("Warning: ForceSocketClose: Closing socket wait timed out.", this.GetType().ToString());
                m_ClosingSockets.Remove(s);
            }

        }

        #endregion Internal Methods

        #region Private Methods

        /// <summary>
        /// Restore config stored in registry
        /// </summary>
        private void RestoreConfig() {
            RegistryKey baseKey = Registry.CurrentUser.OpenSubKey(RegistryService.m_szRegistryString, true);
            if (baseKey == null) {
                baseKey = Registry.CurrentUser.CreateSubKey(RegistryService.m_szRegistryString);
                baseKey.SetValue("MaxConcurrentSends", MAX_CONCURRENT_SENDS.ToString());
                return;
            }

            object result = baseKey.GetValue("MaxConcurrentSends");
            if (result == null) {
                baseKey.SetValue("MaxConcurrentSends", MAX_CONCURRENT_SENDS.ToString());
                return;
            }

            int tmp;
            if (Int32.TryParse(result.ToString(), out tmp)) {
                if (tmp > 0) {
                    this.m_MaxConcurrentSends = tmp;
                    Trace.WriteLine("TCPServerSender setting Max Concurrent Sends to " + m_MaxConcurrentSends.ToString() + ".", this.GetType().ToString());
                    return;
                }
            }
        }

        /// <summary>
        /// Find the current slide
        /// </summary>
        /// <param name="instructor"></param>
        /// <returns></returns>
        private Guid GetSlideID(InstructorModel instructor) {
            using (Synchronizer.Lock(instructor.SyncRoot)) {
                DeckTraversalModel dtm = instructor.CurrentDeckTraversal;
                if (dtm == null)
                    return Guid.Empty;
                using (Synchronizer.Lock(dtm.SyncRoot)) {
                    TableOfContentsModel.Entry e = dtm.Current;
                    using (Synchronizer.Lock(e.SyncRoot)) {
                        SlideModel s = e.Slide;
                        using (Synchronizer.Lock(s.SyncRoot)) {
                            return s.Id;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Queue the send to the Socket buffer with no further delay.
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="length"></param>
        /// <param name="s"></param>
        private void SendNow(SendParameters sendItem) {
            Synchronizer.AssertLockIsHeld(m_SendQueue);
            AsyncCallback ac = new AsyncCallback(SendCallback);
            Socket s = null;
            while (true) {
                try {
                    s = null;
                    using (Synchronizer.Lock(this.m_SocketMap)) {
                        if (m_SocketMap.ContainsKey(sendItem.Id)) {
                            s = (Socket)m_SocketMap[sendItem.Id];
                        }
                        else {
                            //I believe this should never happen.  
                            Debug.Assert(false, "TCPServerSender.SendNow thinks there is a missing socket map entry for client id: " + sendItem.Id.ToString());
                        }
                    }
                    using (Synchronizer.Lock(m_SendingParticipants)) {
                        if (m_SendingParticipants.ContainsKey(sendItem.Id)) {
                            ((ReferenceCounter)m_SendingParticipants[sendItem.Id]).Increment();
                        }
                        else {
                            m_SendingParticipants.Add(sendItem.Id, new ReferenceCounter());
                        }
                        ++m_PendingOutboundMessageCount;
                        s.BeginSend(sendItem.Buffer, 0, sendItem.Length, SocketFlags.None, ac, new SendState(s, sendItem)); //BeginSend won't block.
                    }

                    break;
                }
                catch (Exception e) {
                    using (Synchronizer.Lock(m_SendingParticipants)) {
                        if (m_SendingParticipants.ContainsKey(sendItem.Id)) {
                            ((ReferenceCounter)m_SendingParticipants[sendItem.Id]).Decrement();
                        }
                        --m_PendingOutboundMessageCount;
                        Debug.Assert(m_PendingOutboundMessageCount >= 0, "TCPServerSender.SendNow found negative pending message count");
                    }
                    if (e is SocketException) {
                        SocketException se = (SocketException)e;
                        if (se.ErrorCode == 10055) {
                            //Note that this shouldn't happen since we manually control the queue length.
                            Trace.WriteLine("!!!Server Send queue is full.  Sleeping 50 mS", this.GetType().ToString());
                            Thread.Sleep(50);  //Note that sleeping here could cause various bad performance because other threads may be waiting on us..
                        }
                        else {
                            Trace.WriteLine("Send socket exception: " + se.ToString() + " Error code: " + se.ErrorCode.ToString(), this.GetType().ToString());
                            this.DisableClient(sendItem.Id);
                            break;
                        }
                    }
                    else if (e is ObjectDisposedException) {
                        ObjectDisposedException ode = (ObjectDisposedException)e;
                        this.DisableClient(sendItem.Id);
                        Trace.WriteLine(ode.Message, this.GetType().ToString());
                        break;
                    }
                    else {
                        Trace.WriteLine(e.ToString(), this.GetType().ToString());
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// Put this client's queue in a disabled state
        /// </summary>
        /// <param name="id"></param>
        private void DisableClient(Guid id) {
            using (Synchronizer.Lock(this.m_SendQueue)) {
                m_SendQueue.DisableClient(id);
            }
        }

        #endregion Private Methods

        #region Event Handlers

        public void OnCurrentDeckTraversalChanged(object sender, PropertyEventArgs pea) {
            PropertyChangeEventArgs args = (PropertyChangeEventArgs)pea;
            if (m_DeckTraversal != null) {
                m_DeckTraversal.Changed["Current"].Remove(new PropertyEventHandler(OnTableOfContentsEntryChanged));
            }
            m_DeckTraversal = (DeckTraversalModel)args.NewValue;
            if (m_DeckTraversal != null) {
                using (Synchronizer.Lock(m_DeckTraversal.SyncRoot)) {
                    TableOfContentsModel.Entry e = m_DeckTraversal.Current;
                    using (Synchronizer.Lock(e.SyncRoot)) {
                        SlideModel s = e.Slide;
                        using (Synchronizer.Lock(s.SyncRoot)) {
                            m_CurrentSlideID = s.Id;
                        }
                    }
                }

                m_DeckTraversal.Changed["Current"].Add(new PropertyEventHandler(OnTableOfContentsEntryChanged));
            }
        }

        public void OnTableOfContentsEntryChanged(object sender, PropertyEventArgs pea) {
            PropertyChangeEventArgs args = (PropertyChangeEventArgs)pea;
            TableOfContentsModel.Entry e = (TableOfContentsModel.Entry)args.NewValue;
            if (e != null) {
                using (Synchronizer.Lock(e.SyncRoot)) {
                    SlideModel slide = e.Slide;
                    using (Synchronizer.Lock(slide.SyncRoot)) {
                        m_CurrentSlideID = slide.Id;                   
                    }
                }
            }
        }

        #endregion Event Handlers

        #region Thread Methods

        /// <summary>
        /// Ensure that messages in the low priority sending queue are sent in a timely manner, but in a way such that they do not interfere with
        /// high priority messages. 
        /// </summary>
        private void SendThread() {
            while (!m_Disposed) {
                //Thread.Sleep(25); //Uncomment to simulate a really slow network.
                using (Synchronizer.Cookie cookie = Synchronizer.Lock(m_SendQueue)) {
                    //find out if it is currently safe to send
                    if (m_PendingOutboundMessageCount < m_MaxConcurrentSends) {
                        SendParameters nextSendItem = null;
                        using (Synchronizer.Lock(m_SendingParticipants)) {
                            nextSendItem = (SendParameters)m_SendQueue.Dequeue(m_SendingParticipants,m_CurrentSlideID);
                        }

                        if (nextSendItem != null) {
                            //Trace.WriteLine("SendNow: sequence=" + nextSendItem.MessageSequence.ToString());
                            //Trace.WriteLine("SendNow: CurrentSlide=" + m_CurrentSlideID.ToString() + 
                            //    ";msg slide guid=" + nextSendItem.Tags.SlideID.ToString() + 
                            //    ";IsPublicNode=" + nextSendItem.IsPublicNode.ToString(), "");
                            SendNow(nextSendItem);
                            continue;
                        }
                    }

                    // If nothing can be sent, wait for the queue's status to change.
                    // This happens either when new items are inserted or m_PendingOutboundMessageCount is updated.
                    cookie.Wait();
                }
            }
        }

        #endregion Thread Methods

        #region Callback Methods

        private void SendCallback(IAsyncResult ar) {
            SendState state = (SendState)ar.AsyncState;
            try {
                int len = state.SendSocket.EndSend(ar);
                if (len != state.SendParameters.Length) {
                    //I don't know how this can happen, but just in case..
                    Debug.Assert(false,"Critical error: Server did not send the expected number of bytes.  Buffer length = " + state.SendParameters.Length.ToString() +
                       "; bytes sent = " + len.ToString() + "; remote endpoint = " + state.SendSocket.RemoteEndPoint.ToString());
                }
            }
            catch (SocketException se) {
                Trace.WriteLine("SendCallback SocketException errorcode: " + se.ErrorCode.ToString(), this.GetType().ToString());
                this.DisableClient(state.SendParameters.Id);
            }
            catch (ObjectDisposedException ode) {
                Trace.WriteLine("SendCallback: " + ode.Message, this.GetType().ToString());
                this.DisableClient(state.SendParameters.Id);
            }
            catch (Exception e) {
                Trace.WriteLine(e.ToString(), this.GetType().ToString());
                this.DisableClient(state.SendParameters.Id);
            }
            finally {
                using (Synchronizer.Cookie cookie = Synchronizer.Lock(this.m_SendQueue)) {
                    using (Synchronizer.Lock(m_SendingParticipants)) {
                        if (m_SendingParticipants.ContainsKey(state.SendParameters.Id)) {
                            ((ReferenceCounter)m_SendingParticipants[state.SendParameters.Id]).Decrement();
                        }
                        else {
                            Debug.Assert(false,"Server send callback found a missing reference counter.");
                        }
                        --m_PendingOutboundMessageCount;
                        Debug.Assert(m_PendingOutboundMessageCount >= 0,"Server send callback found a negative pending message count.");

                        //Notify threads closing the socket if this is the last item in the socket queue.
                        if (m_ClosingSockets.ContainsKey(state.SendSocket)) {
                            if (((ReferenceCounter)m_SendingParticipants[state.SendParameters.Id]).IsZero) { 
                                ((ManualResetEvent)m_ClosingSockets[state.SendSocket]).Set();
                            }
                        }

                        // Notify the sender thread that the queue's status has changed.
                        cookie.PulseAll();
                    }
                }
            }
        }

        #endregion Callback Methods

        #region IDisposable Members

        ~TCPServerSender() {
            this.Dispose(false);
        }

        public void Dispose() {
            this.Dispose(true);
            System.GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing) {
            this.m_Disposed = true;
            if (disposing) {
                m_Instructor.Changed["CurrentDeckTraversal"].Remove(new PropertyEventHandler(OnCurrentDeckTraversalChanged));
                if (m_DeckTraversal != null) {
                    m_DeckTraversal.Changed["Current"].Remove(new PropertyEventHandler(OnTableOfContentsEntryChanged));
                }

                // Notify the sending thread that we're disposed.
                using (Synchronizer.Cookie cookie = Synchronizer.Lock(this.m_SendQueue))
                    cookie.PulseAll();
            }
        }

        #endregion

        #region SendState Class

        /// <summary>
        /// A class to carry information between the caller of BeginSend and the callback function.
        /// </summary>
        private class SendState {
            public Socket SendSocket;
            public SendParameters SendParameters;

            public SendState(Socket s, SendParameters sendParams) {
                this.SendSocket = s;
                this.SendParameters = sendParams;
            }
        }

        #endregion SendState Class
    }

    #region SendParameters Class

    /// <summary>
    /// The things associated with a send that we need to enqueue
    /// </summary>
    class SendParameters {
        public byte[] Buffer;
        public int Length;
        public Guid Id;
        public ulong SequenceNumber;
        public MessageTags Tags;
        public bool IsPublicNode;
        public ulong MessageSequence;
        public ulong ChunkSequence; //within message.  Zero-based
        public bool IsHeartbeatMessage;

        public SendParameters(byte[] buffer, int length, Guid id, MessageTags tags, 
            ulong messageSequence, ulong chunkSequence, bool isHeartbeat) {
            this.Buffer = buffer;
            this.Length = length;
            this.Id = id;
            this.Tags = tags;
            this.SequenceNumber = ulong.MaxValue;
            this.MessageSequence = messageSequence;
            this.ChunkSequence = chunkSequence;
            IsPublicNode = false;
            this.IsHeartbeatMessage = isHeartbeat;
        }

        /// <summary>
        /// This has priority over other: return less than zero.
        /// Other has priority over this: return greater than zero.
        /// </summary>
        /// <param name="other"></param>
        /// <param name="currentSlideID"></param>
        /// <returns></returns>
        /// order:
        /// public node GuidEmpty
        /// public node current slide
        /// other node guid empty
        /// other node current slide
        /// lowest sequence number
        public int CompareTo(SendParameters other, Guid currentSlideID) {
            //If other is null, this has priority
            if (other == null)
                return -1;

            //Note: Guid.Empty indicates a message of global significance.
            //First priority for public node global or current slide message
            if (this.IsPublicNode != other.IsPublicNode) {
                if (this.IsPublicNode) {
                    if ((this.Tags.SlideID == Guid.Empty) || (this.Tags.SlideID == currentSlideID))
                        return -1;
                }
                else { 
                    if ((other.Tags.SlideID == Guid.Empty) || (other.Tags.SlideID == currentSlideID))
                        return 1;
                }
            }

            //Second priority for global message
            if (this.Tags.SlideID == Guid.Empty) {
                if (other.Tags.SlideID == Guid.Empty) {
                        return (this.SequenceNumber < other.SequenceNumber) ? -1 : 1;
                }
                else
                    return -1;
            }
            else if (other.Tags.SlideID==Guid.Empty)
                return 1;

            //Third priority goes to messages for the current slide.
            if (this.Tags.SlideID == currentSlideID) {
                if (other.Tags.SlideID == currentSlideID)
                    return (this.SequenceNumber < other.SequenceNumber) ? -1 : 1;
                else
                    return -1;
            }
            else if (other.Tags.SlideID==currentSlideID)
                return 1;
            
            //finally just prioritize by sequence number.
            return (this.SequenceNumber < other.SequenceNumber) ? -1 : 1;
        }
    }

    #endregion SendParameters Class

    #region ReferenceCounter Class

    /// <summary>
    /// A simple class to implement a synchronized reference counter
    /// </summary>
    public class ReferenceCounter {
        int count;

        /// <summary>
        /// Assume one reference right away when we are constructed.
        /// </summary>
        public ReferenceCounter() {
            count = 1;
        }

        public void Increment() {
            lock (this) {
                count++;
            }
        }

        public void Decrement() {
            lock (this) {
                count--;
                if (count < 0)
                    Trace.WriteLine("!!! Critical Error: Server Sender found a negative reference count.", this.GetType().ToString()); 
            }
        }

        public bool IsZero {
            get {
                lock (this) {
                    if (count == 0)
                        return true;
                    return false;
                }
            }
        }

        public override string ToString() {
            lock (this) {
                return count.ToString();
            }
        }
    
    }

    #endregion ReferenceCounter Class
}
