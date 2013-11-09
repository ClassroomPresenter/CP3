// $Id: RTPMessageSender.cs 1603 2008-05-02 18:03:03Z fred $
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

namespace UW.ClassroomPresenter.Network.RTP {
    public class RTPMessageSender : SendingQueue, IRTPMessageSender {
        private readonly PresenterModel m_Model;
        private readonly ClassroomModel m_Classroom;

        private readonly ParticipantManager m_ParticipantManager;

        private readonly RtpLocalParticipant m_RtpLocalParticipant;
        private readonly RtpSession m_RtpSession;
        private RtpSender m_RtpSender;

        private readonly Beacons.BeaconService m_BeaconService;

        private readonly RTPNackManager m_NackManager;
        private readonly FrameBuffer m_FrameBuffer;
        private readonly Chunk.ChunkEncoder m_Encoder;
        private ulong m_MessageSequence;
        private ulong m_FrameSequence;
        private ulong m_LastMessageSequence;

        private readonly PresenterNetworkService m_PresenterNetworkService;
        private readonly StudentSubmissionNetworkService m_StudentSubmissionNetworkService;
        private readonly SynchronizationNetworkService m_SynchronizationNetworkService;
        private readonly ScriptingNetworkService m_ScriptingNetworkService;

        private bool m_Disposed;

        public override ClassroomModel Classroom {
            get { return this.m_Classroom; }
        }

        /// <summary>
        /// If greater than 0, randomly drop this percentage of frames.
        /// </summary>
        private static readonly int DEBUG_DROP_RANDOM_FRAMES = 0;

        public RTPMessageSender(IPEndPoint ep, PresenterModel model, ClassroomModel classroom) {
            this.m_Model = model;
            this.m_Classroom = classroom;

            this.m_RtpLocalParticipant = new RtpLocalParticipant(this.m_Model.Participant);

            using(Synchronizer.Lock(this)) {
                // Register the stream event listeners first, since otherwise there would be a chance
                // that streams could be added between creating the RtpSession (which connects immediately).
                this.m_ParticipantManager = new ParticipantManager(this);

                this.m_RtpSession = new RtpSession(ep, this.m_RtpLocalParticipant, true, true);

                // TODO: Choose a meaningful value for the RtpSender name.
                ushort fec;
                short interpacketdelay;
                using (Synchronizer.Lock(model.SyncRoot)) {
                    using (Synchronizer.Lock(model.ViewerState.SyncRoot)) {
                        fec = (ushort)model.ViewerState.ForwardErrorCorrection;
                        interpacketdelay = (short)model.ViewerState.InterPacketDelay;
                    }
                }
                if( fec == 0 )
                    this.m_RtpSender = this.m_RtpSession.CreateRtpSender( "Classroom Presenter", PayloadType.dynamicPresentation, null );
                else
                    this.m_RtpSender = this.m_RtpSession.CreateRtpSenderFec( "Classroom Presenter", PayloadType.dynamicPresentation, null, 0, fec );
                this.m_RtpSender.DelayBetweenPackets = interpacketdelay;

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
        }

        #region IDisposable Members

        protected override void Dispose(bool disposing) {
            base.Dispose(disposing);

            if(disposing) {
                if(this.m_Disposed) return;
                this.m_Disposed = true;

                this.m_ParticipantManager.Dispose();

                // Simply invoke Dispose() on the Rtp backends.  This will cause them to disconnect.
                // Dispose components in reverse order from that with which they were created.
                this.m_BeaconService.Dispose();
                this.m_PresenterNetworkService.Dispose();
                this.m_RtpSender.Dispose();
                this.m_NackManager.Dispose();
                try {
                    this.m_RtpSession.Dispose();
                } catch(ThreadStateException) {
                    // Microsoft occasionally fails to catch a ThreadStateException thrown in RtcpSender.Dispose(bool).
                }
                this.m_RtpLocalParticipant.Dispose();
            }
        }

        #endregion

        #region Restarting and Recovery

        /// <summary>
        /// Thread entry point for attempting to recreate the network connection
        /// </summary>
        private void ResetThread() {
            while( true ) {
                using( Synchronizer.Lock( this ) ) {
                    try {
                        // Try to Reconnect
                        ushort fec;
                        short interpacketdelay;
                        using( Synchronizer.Lock( this.m_Model.SyncRoot ) ) {
                            using( Synchronizer.Lock( this.m_Model.ViewerState.SyncRoot ) ) {
                                fec = (ushort)this.m_Model.ViewerState.ForwardErrorCorrection;
                                interpacketdelay = (short)this.m_Model.ViewerState.InterPacketDelay;
                            }
                        }
                        if( fec == 0 )
                            this.m_RtpSender = this.m_RtpSession.CreateRtpSender( "Classroom Presenter", PayloadType.dynamicPresentation, null);
                        else
                            this.m_RtpSender = this.m_RtpSession.CreateRtpSenderFec( "Classroom Presenter", PayloadType.dynamicPresentation, null, 0, fec );
                        this.m_RtpSender.DelayBetweenPackets = interpacketdelay;

                        return;
                    } catch( Exception ) {
                        System.Threading.Thread.Sleep( 100 );
                    }
                }
            }
        }

        /// <summary>
        /// The current thread that is being used to try to reset the network device
        /// </summary>
        private Thread m_ResetThread = null;

        /// <summary>
        /// Reset this MessageSender by re-creating the RtpSender
        /// </summary>
        private void Reset() {
            using( Synchronizer.Lock( this ) ) {
                System.Threading.ThreadStart ts = new ThreadStart( ResetThread );
                if( this.m_ResetThread == null ) 
                    this.m_ResetThread = new Thread( ts );

                if( this.m_ResetThread.ThreadState == System.Threading.ThreadState.Stopped )
                    this.m_ResetThread.Start();
            }
        }

        #endregion

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
                    this.RtpSend(chunk, priority);
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
                using (MemoryStream stream = new MemoryStream()) {
                    new BinaryFormatter().Serialize(stream, nack);

                    //Debug.WriteLine(string.Format("NACKing frames {0}",
                    //    nack.MissingSequences.ToString()), this.GetType().ToString());

                    try {
                        this.m_RtpSender.Send(new BufferChunk(stream.GetBuffer(), 0, (int)stream.Length));
                    } catch (Exception) {
                        this.Reset();
                    }
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
        internal void ProcessNack(RtpNackMessage nack) {
            // If the NACK was not meant for us, send it to the NackManager so
            // we can avoid duplicating it.
            if(this.m_RtpSender.SSRC != nack.SenderSSRC) {
                this.m_NackManager.Delay(nack.SenderSSRC, nack.MissingSequences);
            }

            else {
                // Otherwise send all frames which are not already queued and
                // which are still available in the frame buffer.
                //Debug.WriteLine(string.Format("Received NACK requesting frames {0}",
                //    nack.MissingSequences.ToString()), this.GetType().ToString());

                foreach(ulong missing in nack.MissingSequences) {
                    Chunk chunk;
                    if(this.m_FrameBuffer.Take(missing, out chunk)) {
                        if (chunk != null) {
                            //Debug.WriteLine(string.Format("Queueing REPLACEMENT for frame #{0} ({1} bytes, message #{2}, chunk #{3} of {4})",
                            //    chunk.FrameSequence,
                            //    chunk.Data.Length,
                            //    chunk.MessageSequence,
                            //    chunk.ChunkSequenceInMessage + 1,
                            //    chunk.NumberOfChunksInMessage),
                            //    this.GetType().ToString());

                            this.Post(delegate() {
                                this.RtpSend(chunk, MessagePriority.RealTime);
                            }, MessagePriority.RealTime);
                        } else {
                            //Debug.WriteLine(string.Format("NOT queueing replacement for frame #{0} (already in queue or missing)",
                            //    missing), this.GetType().ToString());
                        }
                    } else {
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
        private void RtpSend(Chunk chunk, MessagePriority priority) {
            using(Synchronizer.Lock(this)) {
                using(MemoryStream stream = new MemoryStream((int) (this.m_Encoder.MaximumChunkSize * 2))) {
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

                    // Serialize the chunk (which itself contains a serialized message).
                    // TODO: see whether the wire protocol can be made more efficient.
                    stream.Position = 0;
                    this.m_Encoder.EncodeChunk(stream, chunk);

                    // Prepare the frame to be sent over RTP.
                    BufferChunk buffer = new BufferChunk(stream.GetBuffer(), 0, (int) stream.Length);

                    // Send it (or drop some percentage of packets if the debug option is enabled).
                    if(DEBUG_DROP_RANDOM_FRAMES <= 0 || new Random().Next(100) >= DEBUG_DROP_RANDOM_FRAMES) {

                        Debug.WriteLine(string.Format("Sending frame #{0} ({1} bytes, message #{2}, chunk #{3} of {4}, depending on #{5}, priority {6})",
                            chunk.FrameSequence,
                            chunk.Data.Length,
                            chunk.MessageSequence,
                            chunk.ChunkSequenceInMessage + 1,
                            chunk.NumberOfChunksInMessage,
                            chunk.MessageDependency,
                            priority),
                            this.GetType().ToString());

                        try {
                            this.m_RtpSender.Send( buffer );
                        } catch( Exception ) {
                            this.Reset();
                        }
                    }

                    else {
                        Debug.WriteLine(string.Format("DEBUG: Dropping frame #{0} ({1} bytes, message #{2}, chunk #{3} of {4}, depending on #{5}, priority {6})",
                            chunk.FrameSequence,
                            chunk.Data.Length,
                            chunk.MessageSequence,
                            chunk.ChunkSequenceInMessage + 1,
                            chunk.NumberOfChunksInMessage,
                            chunk.MessageDependency,
                            priority),
                            this.GetType().ToString());
                    }
                }
            }
        }


        /// <summary>
        /// Responsible for creating and destroying <see cref="ParticipantModel"/> and
        /// <see cref="RTPMessageReceiver"/> instances for each participant in the classroom.
        /// </summary>
        private class ParticipantManager : IDisposable {
            #region IDisposable Members

            /// <summary>
            /// The <see cref="RTPMessageSender"/> which created this <see cref="ParticipantManager"/>.
            /// </summary>
            private readonly RTPMessageSender m_Sender;

            private readonly Hashtable m_Receivers;
            private readonly Hashtable m_Participants;

            private bool m_Disposed;

            public ParticipantManager(RTPMessageSender sender) {
                this.m_Sender = sender;

                this.m_Receivers = new Hashtable();
                this.m_Participants = new Hashtable();

                RtpEvents.RtpStreamAdded += new RtpEvents.RtpStreamAddedEventHandler(this.HandleStreamAdded);
                RtpEvents.RtpStreamRemoved += new RtpEvents.RtpStreamRemovedEventHandler(this.HandleStreamRemoved);
            }

            ~ParticipantManager() {
                this.Dispose(false);
            }

            public void Dispose() {
                this.Dispose(true);
                System.GC.SuppressFinalize(this);
            }

            protected virtual void Dispose(bool disposing) {
                if (disposing) {
                    using (Synchronizer.Lock(this)) {
                        if (this.m_Disposed) return;
                        this.m_Disposed = true;

                        // Remove the stream event handlers first so we don't get spurious events while disposing.
                        RtpEvents.RtpStreamAdded -= new RtpEvents.RtpStreamAddedEventHandler(this.HandleStreamAdded);
                        RtpEvents.RtpStreamRemoved -= new RtpEvents.RtpStreamRemovedEventHandler(this.HandleStreamRemoved);
                    }

                    // Clean up any receivers which are still open.
                    foreach (RTPMessageReceiver receiver in this.m_Receivers.Values) {
                        receiver.Dispose();
                    }

                    // Remove the receivers' participants from the classroom.
                    using (Synchronizer.Lock(this.m_Sender.m_Classroom.SyncRoot)) {
                        foreach (ParticipantModel participant in this.m_Participants.Values) {
                            if (this.m_Sender.m_Classroom.Participants.Contains(participant))
                                this.m_Sender.m_Classroom.Participants.Remove(participant);
                        }
                    }
                }
            }

            #endregion

            #region RtpEvents Handlers

            /// <summary>
            /// Creates a <see cref="ParticipantModel"/> and <see cref="RTPMessageReceiver"/>
            /// whenever a new stream is connected to the venue.  The <see cref="ParticipantModel"/>
            /// is added to the current <see cref="ClassroomModel"/>.
            /// </summary>
            private void HandleStreamAdded(object sender, RtpEvents.RtpStreamEventArgs args) {
                using(Synchronizer.Lock(this.m_Sender.m_Classroom.SyncRoot)) {
                    using(Synchronizer.Lock(this)) {
                        if(this.m_Disposed) throw new ObjectDisposedException("RTPMessageSender");

                        RtpStream stream = args.RtpStream;

                        // Ignore streams that are not part of our session.
                        if(this.m_Sender.m_RtpSession.ContainsStream(stream)) {
                            // Ignore streams that are not DynamicPresentations.
                            if(stream.PayloadType == PayloadType.dynamicPresentation) {
                                // Ignore our own stream, but create a listener for all others.
                                if(stream.SSRC != this.m_Sender.m_RtpSender.SSRC) {

                                    // If we've not seen this client before, create a new ParticipantModel,
                                    // identified by the participant's CName, which, if the participant
                                    // is running Classroom Presenter, is a Guid string.
                                    string cname = stream.Properties.CName;

                                    // It's possible for a participant to generate more than one Rtp stream,
                                    // so we need to keep a different table for m_Participants than m_Receivers.
                                    ParticipantModel participant = ((ParticipantModel) this.m_Participants[cname]);
                                    if(participant == null) {
                                        // Also get the remote client's HumanName from the participant data.
                                        RtpParticipant client = this.m_Sender.m_RtpSession.Participants[cname];
                                        participant = new ParticipantModel(new Guid(cname), client.Name);
                                        // Add the participant to our table.
                                        this.m_Participants.Add(cname, participant);
                                    }

                                    // Add the participant to the classroom if it is not already a member.
                                    if(!this.m_Sender.m_Classroom.Participants.Contains(participant))
                                        this.m_Sender.m_Classroom.Participants.Add(participant);

                                    // Create a receiver for this specific stream (there may be more than one stream per participant)
                                    // and add it to the table of receivers so it can be disposed when the stream is removed.
                                    RTPMessageReceiver receiver = new RTPMessageReceiver(this.m_Sender, stream,
                                        this.m_Sender.m_Model, this.m_Sender.m_Classroom, participant);
                                    this.m_Receivers.Add(stream, receiver);
                                }
                            }
                        }
                    }
                }
            }

            private void HandleStreamRemoved(object sender, MSR.LST.Net.Rtp.RtpEvents.RtpStreamEventArgs args) {
                using(Synchronizer.Lock(this.m_Sender.m_Classroom.SyncRoot)) {
                    using(Synchronizer.Lock(this)) {
                        // Do not check (or care) whether the RTPMessageSender has been disposed,
                        // because we will get lots of RtpStreamRemoved events when the RtpSession is closed.

                        RtpStream stream = args.RtpStream;

                        RTPMessageReceiver receiver = ((RTPMessageReceiver) this.m_Receivers[stream]);
                        if(receiver != null) {
                            receiver.Dispose();
                            this.m_Receivers.Remove(stream);
                        }

                        // Discard all pending Nacks so the NackManager doesn't keep nacking them forever.
                        this.m_Sender.NackManager.Discard( stream.SSRC, Range.UNIVERSE );

                        // If this was the last stream for the participant, unregister the ParticipantModel
                        // and remove it from the classroom.
                        string cname = stream.Properties.CName;
                        RtpParticipant client = this.m_Sender.m_RtpSession.Participants[cname];
                        if(client == null || client.SSRCs.Count <= 0) {
                            ParticipantModel participant = ((ParticipantModel) this.m_Participants[cname]);
                            if(participant != null) {

                                // Remove the participant from the classroom.
                                if(this.m_Sender.m_Classroom.Participants.Contains(participant))
                                    this.m_Sender.m_Classroom.Participants.Remove(participant);
                            }

                            // Remove the participant from the table.
                            this.m_Participants.Remove(cname);
                        }
                    }
                }
            }

            #endregion RtpEvents Handlers
        }
    }
}

#endif