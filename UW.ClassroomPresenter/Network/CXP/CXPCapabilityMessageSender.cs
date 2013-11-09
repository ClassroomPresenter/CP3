// $Id: RTPMessageSender.cs 1112 2006-08-11 00:40:16Z pediddle $
#if RTP_BUILD
using System;
using System.Collections;
using System.Diagnostics;
using System.Threading;

using System.Net;
using MSR.LST;
using MSR.LST.Net.Rtp;

using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;

using UW.ClassroomPresenter.Model;
using UW.ClassroomPresenter.Model.Network;
using UW.ClassroomPresenter.Network.Messages;
using UW.ClassroomPresenter.Network.Messages.Network;
using UW.ClassroomPresenter.Network.Chunking;
using UW.ClassroomPresenter.Network.RTP;
using MSR.LST.ConferenceXP;
using UW.ClassroomPresenter.Viewer;
using System.Collections.Generic;

 namespace UW.ClassroomPresenter.Network.CXP {
    public class CXPCapabilityMessageSender : SendingQueue, IRTPMessageSender {
        private readonly PresenterModel m_Model;
        private readonly ClassroomModel m_Classroom;

        private readonly Beacons.BeaconService m_BeaconService;
        private readonly RTPNackManager m_NackManager;
        private readonly FrameBuffer m_FrameBuffer;
        private readonly Chunk.ChunkEncoder m_Encoder;
        private ulong m_MessageSequence;
        private ulong m_FrameSequence;
        private ulong m_LastMessageSequence;
        private Guid m_LocalId;
        private Dictionary<uint, Guid> m_SsrcToSenderId;
        private string m_AssociationCname;

        private readonly PresenterNetworkService m_PresenterNetworkService;
        private readonly StudentSubmissionNetworkService m_StudentSubmissionNetworkService;
        private readonly SynchronizationNetworkService m_SynchronizationNetworkService;
        private readonly ScriptingNetworkService m_ScriptingNetworkService;

        private bool m_Disposed;
        private PresenterCapability m_Capability;
        private CapabilityNetworkStatus m_CapabilityNetworkStatus;

        /// <summary>
        /// Key: remote node CNAME; Value: Message receiver
        /// </summary>
        private Dictionary<string, CXPCapabilityMessageReceiver> m_Receivers;

        /// <summary>
        /// Key: remote node CNAME; value: corresponding ParticipantModel
        /// </summary>
        private Dictionary<string,ParticipantModel> m_Participants;


        public override ClassroomModel Classroom {
            get { return this.m_Classroom; }
        }


        public CXPCapabilityMessageSender(PresenterModel model, PresenterCapability capability) {
            this.m_Model = model;
            this.m_LocalId = m_Model.Participant.Guid;
            this.m_Classroom = new ClassroomModel(null, "CXP Capability Classroom", ClassroomModelType.CXPCapability);
            this.m_Capability = capability;
            this.m_Participants = new Dictionary<string, ParticipantModel>();
            this.m_SsrcToSenderId = new Dictionary<uint, Guid>();
            this.m_Receivers = new Dictionary<string, CXPCapabilityMessageReceiver>();

            m_Capability.OnStreamAdded += new PresenterCapability.OnStreamAddedHandler(OnStreamAdded);
            m_Capability.OnStreamRemoved +=new PresenterCapability.OnStreamRemovedHandler(OnStreamRemoved);
            using(Synchronizer.Lock(this)) {
                // Initialize the message chunking utilities.
                this.m_Encoder = new Chunk.ChunkEncoder();
                // TODO: Make the buffer size dynamic, ie., grow if we send a single very large message.
                // Make the buffer store up to 5MB worth of data.
                this.m_FrameBuffer = new FrameBuffer(5 * 1024 * 1024 / this.m_Encoder.MaximumChunkSize);

                // Create the NackManager which is responsible for sending NACKs on behalf of
                // our set of RTPMessageReceivers.                
                this.m_NackManager = new RTPNackManager(this, this.m_Classroom);
            }

            // Create network services outside of the "lock(this)" so they can lock their own objects
            // without worrying about locking order.

            // Create the PresenterNetworkService which will watch for changes to the model and send messages.
            this.m_PresenterNetworkService = new PresenterNetworkService(this, this.m_Model);

            // Create the StudentSubmissionsNetworkService which will watch for requests to submit and send messages.
            this.m_StudentSubmissionNetworkService = new StudentSubmissionNetworkService(this, this.m_Model);

            // Create the SynchronizationNetworkService which will watch for all synchronization messages.
            this.m_SynchronizationNetworkService = new SynchronizationNetworkService( this, this.m_Model );

            // Create the ScriptingNetworkService which will watch for all scripting messages.
            this.m_ScriptingNetworkService = new ScriptingNetworkService( this, this.m_Model );

            // Create the BeaconService which will broadcast periodic information about the presentation.
            this.m_BeaconService = new Beacons.DefaultBeaconService(this, this.m_Model);

            // Report Network status to the UI
            m_CapabilityNetworkStatus = new CapabilityNetworkStatus(this.m_Model);
            m_CapabilityNetworkStatus.Register();

            // Send an initial message to announce our node to others in the venue.
            SendObject(new CapabilityMessageWrapper(null, m_LocalId, Guid.Empty, m_Capability.IsSender));
        }

        void OnStreamAdded(MSR.LST.Net.Rtp.RtpStream rtpStream) {
            //Debug.WriteLine("OnStreamAdded ssrc=" + rtpStream.SSRC.ToString() + ";cname=" + rtpStream.Properties.CName);
            using (Synchronizer.Lock(this.m_Classroom.SyncRoot)) {
                if (rtpStream.SSRC != this.m_Capability.RtpSender.SSRC) {
                    //We need the participant Guid before we can set up the participant model.  Do that on receipt of the first message from a given remote node.
                    //Debug.WriteLine("Adding stream ssrc: " + rtpStream.SSRC.ToString());
                }
                else { 
                     //Debug.WriteLine("Ignoring RTP Stream Add because the stream SSRC matches the RTPSender SSRC");               
                }
            }
        }

        void OnStreamRemoved(MSR.LST.Net.Rtp.RtpStream rtpStream) {
            //Debug.WriteLine("OnStreamRemoved ssrc=" + rtpStream.SSRC.ToString() + ";cname=" + rtpStream.Properties.CName);
            // Do not check (or care) whether the RTPMessageSender has been disposed,
            // because we will get lots of RtpStreamRemoved events when the RtpSession is closed.

            // Unregister the ParticipantModel and remove it from the classroom.
            string cname = rtpStream.Properties.CName;
            if (this.m_Participants.ContainsKey(cname)) {
                using (Synchronizer.Lock(this.m_Classroom.SyncRoot)) {
                    if (this.m_Classroom.Participants.Contains(m_Participants[cname])) {
                        this.m_Classroom.Participants.Remove(m_Participants[cname]);
                    }
                }
                this.m_Participants.Remove(cname);
            }
            
            //If the stream being removed is our currently associated instructor:
            if (cname.Equals(m_AssociationCname)) {
                using (Synchronizer.Lock(this.m_Model.Network.SyncRoot)) {
                    this.m_Model.Network.Association = null;
                }
                m_AssociationCname = null;
            }

            if (this.m_Receivers.ContainsKey(cname)) {
                uint remoteSsrc = this.m_Receivers[cname].RemoteSsrc;
                if (this.m_SsrcToSenderId.ContainsKey(remoteSsrc)) {
                    this.m_SsrcToSenderId.Remove(remoteSsrc);
                }
                else {
                    Trace.WriteLine("!!!!Warning: Failed to find SSRC in lookup table when removing stream.");
                }

                // Discard all pending Nacks so the NackManager doesn't keep nacking them forever.
                this.NackManager.Discard(remoteSsrc, Range.UNIVERSE);

                //Clean up the receiver                
                //Debug.WriteLine("Removed receiver cname: " + cname + "; receiver SSRC: " + remoteSsrc.ToString());
                this.m_Receivers[cname].Dispose();
                this.m_Receivers.Remove(cname);
            }
            else {
                //Debug.WriteLine("Removed a stream for which we have no capability receiver cname: " + cname);
            }
        }

        #region IDisposable Members

        protected override void Dispose(bool disposing) {
            base.Dispose(disposing);

            if(disposing) {
                if(this.m_Disposed) return;
                this.m_Disposed = true;
                this.m_CapabilityNetworkStatus.Unregister();
                this.m_BeaconService.Dispose();
                this.m_PresenterNetworkService.Dispose();
                this.m_NackManager.Dispose();
                m_Capability.OnStreamAdded -= new PresenterCapability.OnStreamAddedHandler(OnStreamAdded);
                m_Capability.OnStreamRemoved -= new PresenterCapability.OnStreamRemovedHandler(OnStreamRemoved);
            }
        }

        #endregion

        internal void Receive(object data, RtpParticipant rtpParticipant) {
            //Drop messages received from the local node
            if (this.m_Capability.RtpSender.RtcpProperties.CName.Equals(rtpParticipant.CName)) {
                return;
            }

            //If this is the first message from a given node, attempt to create the receiver for it.
            if (!this.m_Receivers.ContainsKey(rtpParticipant.CName)) {
                if (data is CapabilityMessageWrapper) {
                    Guid sender_id = ((CapabilityMessageWrapper)data).SenderId;
                    bool sender_is_instructor = ((CapabilityMessageWrapper)data).SenderIsInstructor;
                    ParticipantModel participant = new ParticipantModel(sender_id, rtpParticipant.Name);
                    m_Participants.Add(rtpParticipant.CName, participant);

                    //Add to classroom if the local node is instructor
                    if (m_Capability.IsSender) {
                        using (Synchronizer.Lock(m_Classroom.SyncRoot)) {
                            this.m_Classroom.Participants.Add(participant);
                        }
                    }
                    this.m_SsrcToSenderId.Add(rtpParticipant.SSRC, sender_id);
                    this.m_Receivers.Add(rtpParticipant.CName, new CXPCapabilityMessageReceiver(this, rtpParticipant.SSRC, m_Model, m_Classroom, participant));
                    
                    //Set the Network Association only if local node is not an instructor, and the remote node is Instructor.
                    if ((!m_Capability.IsSender) && (sender_is_instructor)) {
                        m_AssociationCname = rtpParticipant.CName;
                        using (Synchronizer.Lock(this.m_Model.Network.SyncRoot)) {
                            this.m_Model.Network.Association = participant;
                        }
                    }

                    //Debug.WriteLine("Created new receiver on message receipt: ssrc=" + rtpParticipant.SSRC.ToString() + ";cname=" + rtpParticipant.CName);
                }
                else {
                    //Trace.WriteLine("Dropping message of type " + data.GetType().ToString() + " from " + rtpParticipant.CName + " because we don't yet have the sender's Guid.");
                    return;
                }
            }
            this.m_Receivers[rtpParticipant.CName].Receive(data);
        }

 
        public override void Send(Message message, MessagePriority priority) {
            // NOTE: Added by CMPRINCE for testing network performance
            // Save this message
            this.m_Model.ViewerState.Diagnostic.LogMessageSend(message, priority);

            Chunk[] chunks;
            // Serialize the message and split it into chunks with the chunk encoder.
            // The encoder updates the current message- and chunk sequence numbers.
            using(Synchronizer.Lock(this)) {
                chunks = this.m_Encoder.MakeChunks(message, ref this.m_MessageSequence);
            }

            // Sanity check: make sure the message indices are the same for all chunks.
            Debug.Assert(chunks.Length < 0 || Array.TrueForAll(chunks, delegate(Chunk test) {
                return test.MessageSequence == chunks[0].MessageSequence
                    && test.NumberOfChunksInMessage == chunks[0].NumberOfChunksInMessage;
            }));

            // Set the dependency to depend on the most recent message, but only if it
            // is default priority.  Higher priority messages shouldn't be delayed
            // (by default) by dropped packets, and lower priority messages are usually
            // stuff like deck content that doesn't depend on other messages.
            // FIXME: Come up with a better default dependency scheme.
            // Currently, the only message priorities used in the whole of Classroom Presenter
            // are Default, Low (for deck images), and RealTime (for ink).
            // We need to evaluate all different kinds of messages and make sure
            // whatever choice is made works for new types as well.
            if (priority == MessagePriority.Default) {
                for (int i = 0; i < chunks.Length; i++)
                    chunks[i].MessageDependency = this.m_LastMessageSequence;

                // Update the previous message sequence number so the next message
                // can depend on it.
                if (chunks.Length > 0 && chunks[0].MessageSequence > ulong.MinValue)
                    this.m_LastMessageSequence = chunks[0].MessageSequence;
            }

            // Send each chunk asynchronously, with the requested priority.
            Array.ForEach(chunks, delegate(Chunk chunk) {
                this.Post(delegate() {
                    this.CapabilitySend(chunk, priority);
                }, priority);
            });
        }

        internal RTPNackManager NackManager {
            get { return this.m_NackManager; }
        }

        /// <summary>
        /// This method is intended to be called only by <see cref="RTPMessageReceiver.HandleAssemblerNack"/>.
        /// Serialize the NACK packet (as a single chunk), and 
        /// broadcast it <i>immediately</i> (preempting all other prioritized 
        /// chunks in the queue, except a chunk which is currently being sent).
        /// </summary>
        /// <param name="nack">The NACK packet to send</param>
        public void SendNack(RtpNackMessage nack) {
            using (Synchronizer.Lock(this)) {
                if (this.m_SsrcToSenderId.ContainsKey(nack.SenderSSRC)) {
                    //Debug.WriteLine(string.Format("NACKing frames {0}", nack.MissingSequences.ToString()), this.GetType().ToString());
                    SendObject(new CapabilityMessageWrapper(nack,m_LocalId,this.m_SsrcToSenderId[nack.SenderSSRC],m_Capability.IsSender));
                }
                else {
                    Trace.WriteLine("!!!!!Warning: Failed to find sender SSRC in lookup table while trying to send NACK");
                }
            }
        }

        /// <summary>
        /// This method is intended to be called only by <see cref="RTPMessageReceiver.HandleFrameReceived"/>.
        /// If the NACK is addressed to this sender, the requested
        /// frames are retrieved from our buffer and resent, if available.  Otherwise, 
        /// the <see cref="RTPNackManager"/> is notified so we don't send duplicate NACKs
        /// to the addressee.
        /// </summary>
        /// <remarks>
        /// The frames are enqueued with <see cref="MessagePriority.RealTime"/>real-time priority</see>.
        /// The rationale behind this is that, even if a frame was originally low-priority, the clients 
        /// have already waited "long enough".  Also, a replacement frame shouldn't
        /// be subject to NACKing.
        /// </remarks>
        /// <param name="nack">The received/deserialized NACK message</param>
        internal void ProcessNack(RtpNackMessage nack, Guid intendedNackRecipientId) {
            // If the NACK was not meant for us, send it to the NackManager so
            // we can avoid duplicating it.
            if (!this.m_LocalId.Equals(intendedNackRecipientId)) {
                this.m_NackManager.Delay(nack.SenderSSRC, nack.MissingSequences);
            }
            else {
                // Otherwise send all frames which are not already queued and
                // which are still available in the frame buffer.
                //Debug.WriteLine(string.Format("Received NACK requesting frames {0}",
                //    nack.MissingSequences.ToString()), this.GetType().ToString());

                foreach (ulong missing in nack.MissingSequences) {
                    Chunk chunk;
                    if (this.m_FrameBuffer.Take(missing, out chunk)) {
                        if (chunk != null) {
                            //Debug.WriteLine(string.Format("Queueing REPLACEMENT for frame #{0} ({1} bytes, message #{2}, chunk #{3} of {4})",
                            //    chunk.FrameSequence,
                            //    chunk.Data.Length,
                            //    chunk.MessageSequence,
                            //    chunk.ChunkSequenceInMessage + 1,
                            //    chunk.NumberOfChunksInMessage),
                            //    this.GetType().ToString());

                            this.Post(delegate() {
                                this.CapabilitySend(chunk, MessagePriority.RealTime);
                            }, MessagePriority.RealTime);
                        }
                        else {
                            //Debug.WriteLine(string.Format("NOT queueing replacement for frame #{0} (already in queue or missing)",
                            //    missing), this.GetType().ToString());
                        }
                    }
                    else {
                        //Debug.WriteLine(string.Format("Frame #{0} not in buffer; giving up",
                        //    missing), this.GetType().ToString());
                    }
                }
            }
        }

        /// <summary>
        /// Broadcasts a single chunk and stores it in the frame buffer.
        /// </summary>
        /// <param name="chunk">The chunk to send</param>
        private void CapabilitySend(Chunk chunk, MessagePriority priority) {
            using(Synchronizer.Lock(this)) {
                // Store the sequence number of non-real-time chunks so they can be NACKed.
                // Use "++this.m_FrameSequence" so that non-real-time chunks always have a
                // FrameSequence that is greater than 0.
                if (priority != MessagePriority.RealTime)
                    chunk.FrameSequence = ++this.m_FrameSequence;

                // If the chunk is not real time (or it is being resent a second time),
                // store the chunk in the buffer so a NACK request can resend it.
                if (chunk.FrameSequence > ulong.MinValue)
                    this.m_FrameBuffer.Insert(chunk);

                // Buffering the chunk probably evicted other chunks from the buffer,
                // so get the oldest frame in the buffer so receivers don't waste time
                // sending NACKs for chunks that are no longer available.
                chunk.OldestRecoverableFrame = this.m_FrameBuffer.OldestRecoverableFrame;
                chunk.OldestRecoverableMessage = this.m_FrameBuffer.OldestRecoverableMessage;

                Debug.WriteLine(string.Format("Sending frame #{0} ({1} bytes, message #{2}, chunk #{3} of {4}, depending on #{5}, priority {6})",
                    chunk.FrameSequence,
                    chunk.Data.Length,
                    chunk.MessageSequence,
                    chunk.ChunkSequenceInMessage + 1,
                    chunk.NumberOfChunksInMessage,
                    chunk.MessageDependency,
                    priority),
                    this.GetType().ToString());

                SendObject(new CapabilityMessageWrapper(chunk, m_LocalId, Guid.Empty, m_Capability.IsSender));
            }
        }

        private void SendObject(object o) {
            try {
                if (this.m_Capability.IsSending) {  
                    this.m_Capability.SendObject(o);
                }
                else {
                    //This should not happen since we take pains to not create this instance until after the StartSending method has completed.
                    Trace.WriteLine("Warning: dropping outbound message because capability.IsSending is false!!! ");
                }
            }
            catch (Exception ex) {
                Trace.WriteLine(ex.ToString());
            }
        }

        /// <summary>
        /// Publish the NetworkStatus property
        /// </summary>
        private class CapabilityNetworkStatus : PropertyPublisher{
            private NetworkStatus m_NetworkStatus;
            private PresenterModel m_Model;

            [Published]
            public NetworkStatus NetworkStatus {
                get { return this.GetPublishedProperty("NetworkStatus", ref m_NetworkStatus); }
            }

            public CapabilityNetworkStatus(PresenterModel model) {
                m_Model = model;
            }

            public void Register() {
                m_NetworkStatus = new NetworkStatus(ConnectionStatus.Connected, ConnectionProtocolType.CXPCapability, TCPRole.None, 0);
                m_NetworkStatus.ClassroomName = "ConferenceXP Capability";
                m_Model.Network.RegisterNetworkStatusProvider(this, true, m_NetworkStatus);
            }

            public void Unregister() {
                this.m_Model.Network.RegisterNetworkStatusProvider(this, false, null);
            }
        }

    }

     /// <summary>
     /// The wrapper class is designed to work around a couple of assumptions which break in the capability context.
     /// The stand-alone app uses the participant ID for the Cname, and assumes a single SSRC.  In the CXP capability
     /// context we don't control the cname, so we need to transmit the sender ID using different means.
     /// In the capability context the SSRC used by a sender for the capability channel is not easy for the receiver
     /// to positively identify.  Instead we use a recipient ID to identify the intended recipient of a message.
     /// Note that this latter problem only comes into play in the case of NACKing.
     /// </summary>
     [Serializable]
     public class CapabilityMessageWrapper {
         public object Message;
         public Guid SenderId;
         public Guid RecipientId;
         public bool SenderIsInstructor;
         public CapabilityMessageWrapper(object message, Guid sender, Guid recipient, bool senderIsInstructor) {
             Message = message;
             SenderId = sender;
             RecipientId = recipient;
             SenderIsInstructor = senderIsInstructor;
         }
     }
}

#endif
