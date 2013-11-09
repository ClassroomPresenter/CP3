
#if RTP_BUILD
using System;
using System.Collections.Generic;
using System.Text;
using System.Net;


using MSR.LST.Net.Rtp;

using System.Diagnostics;
using UW.ClassroomPresenter.Network.Messages;
using UW.ClassroomPresenter.Model;
using UW.ClassroomPresenter.Network.Chunking;
using UW.ClassroomPresenter.Network.Groups;
using System.IO;
using System.Collections;
using System.Threading;
using MSR.LST;

namespace UW.ClassroomPresenter.Network.TCP {
    /// <summary>
    /// Bridge unicast messages into a multicast group.  
    /// </summary>
    /// <remarks>
    /// This is not to be confused with the application's multicast (RTP) presentation 
    /// functionality -- This code is to be enabled only in scenarios where 
    /// we need to use ConferenceXP archiving and/or live streaming infrastructure with a *unicast* 
    /// presentation.  Serialized messages are passed from the TCPServer on an instructor, or
    /// from the TCPClient on a public node, and reflected to the specified multicast group.  Since this
    /// code does not support a listener, student nodes should not be connected using multicast in this
    /// scenario, but should instead connect directly to the TCPServer.
    /// Since this supports an unusual use case, we will enable and configure with app.config.
    /// Also note that the TCP transport does not normally use the beacon, but we add a beacon here
    /// in order to support basic navigation for the case where the archiver joins late.
    /// </remarks>
    class UnicastToMulticastBridge : SendingQueue {
        //Inherit from SendingQueue in order to integrate with the BeaconService.

        //The MSR.RTP items
        private RtpParticipant m_RtpParticipant = null;
        private RtpSession m_RtpSession = null;
        private RtpSender m_RtpSender = null;

        //Configurable items
        private string m_Cname;
        private string m_Name;
        private IPEndPoint m_MulticastEndpoint;
        private ushort m_Fec;
        private short m_InterpacketDelay;

        //Items for BeaconService support
        private Beacons.BridgeBeaconService m_BeaconService;
        private readonly Chunk.ChunkEncoder m_Encoder;
        private ulong m_ChunkSequence;

        //Items for the Send Thread.
        private PriorityQueue<BridgeMessage> m_SendQueue;
        private Thread m_SendThread;
        private bool m_Disposing;
        private  EventWaitHandle m_SendQueueWait;

        public UnicastToMulticastBridge(PresenterModel model) {
            GetConfig();

            //Prepare for RTP sending
            m_RtpParticipant = new RtpParticipant(m_Cname, m_Name);
            try {
                m_RtpSession = new RtpSession(m_MulticastEndpoint, m_RtpParticipant, true, false);
            }
            catch (Exception e) {
                Trace.WriteLine(e.ToString());
                m_RtpSession = null;
                m_RtpParticipant = null;
                return;
            }

            try {
                /// Notes about FEC:  There are two types supported by the MSR RTP stack:  Frame-based and Packet-based.
                /// Setting cDataPx to zero forces frame-based.  In this case cFecPx is a percentage.  It must be greater than zero, but can be
                /// very large, eg. 1000.  Frame-based FEC appears to be better for large frames (which the stack splits into multiple packets)
                /// possibly because the FEC packets for the frame are not interlaced in time sequence with the frame packets.
                /// If cDataPx is not zero, packet-based FEC is used.  In this mode cDataPx and cFecPx are a ratio of data packets to fec packets.
                /// For single packet frames, 1:1 and 0:100 are identical.  Single packet frames are frames smaller than
                /// the MTU which is normally 1500 bytes.  Presenter's Chunk encoder creates frames up to 16kbytes.  Many frames are smaller
                /// than this, but only a few are below the MTU.  For this reason we will always use Frame-based FEC.  Usful values are expected to
                /// be from around 10 up to around 100.  More than 100 might be good in some cases, but might impact performance.
                /// More than 500 would probably never be advised.
                m_RtpSender = m_RtpSession.CreateRtpSenderFec("Classroom Presenter Unicast to Multicast Bridge", PayloadType.dynamicPresentation, null, 0, m_Fec);
                m_RtpSender.DelayBetweenPackets = m_InterpacketDelay;
            }
            catch (Exception e) { 
                Trace.WriteLine(e.ToString());
                try {
                    m_RtpSession.Dispose();
                }
                catch { }
                m_RtpParticipant = null;
                m_RtpSession = null;
                m_RtpSender = null;
                return; 
            }

            //Prepare the beacon
            this.m_ChunkSequence = 0;
            this.m_Encoder = new Chunk.ChunkEncoder();
            this.m_BeaconService = new Beacons.BridgeBeaconService(this, model);

            //Prepare the queue and sending thread
            m_Disposing = false;
            m_SendQueueWait = new EventWaitHandle(false,EventResetMode.AutoReset);
            m_SendQueue = new PriorityQueue<BridgeMessage>();
            m_SendThread = new Thread(new ThreadStart(SendThread));
            m_SendThread.Start();
        }

        /// <summary>
        /// Watch the queue; Send messages as they are available
        /// </summary>
        private void SendThread() {
            while (!m_Disposing) {
                BridgeMessage bmsg = null;
                lock (m_SendQueue) {
                    while (m_SendQueue.Count > 0) {
                        bmsg = m_SendQueue.Dequeue();
                        if ((m_SendQueue.Count < 5) || !(bmsg.BridgePriority.Equals(MessagePriority.RealTime))) {
                            break;
                        }
                    }
                }
                if (bmsg != null) {
                    if (m_RtpSender != null) {
                        try {
                            Trace.WriteLine("Bridge Sending frame; size=" + bmsg.Buffer.Length.ToString() + ";priority=" + bmsg.BridgePriority.ToString());
                            m_RtpSender.Send(bmsg.Buffer); //This is a synchronous call that may take a significant amount of time to return.
                        }
                        catch (ObjectDisposedException) {
                            //This happens when we dispose the MSR stuff.  No big deal.
                            Trace.WriteLine("ObjectDisposedException in RtpSender.Send", this.GetType().ToString());
                        }
                        catch (Exception e) {
                            Trace.WriteLine(e.ToString());
                        }
                    }
                }
                else {
                    m_SendQueueWait.WaitOne();
                }
            }
        }

        /// <summary>
        /// The main Send method.  Called both by the unicast code and as a result of calls from the BeaconService thread.
        /// </summary>
        /// <param name="buf"></param>
        public void Send(byte[] buf, int length, MessageTags tags) {

            BridgeMessage bmsg = new BridgeMessage(new BufferChunk(buf,0,length), tags.BridgePriority);
            if (m_SendQueue != null) {
                lock (m_SendQueue) {
                    m_SendQueue.Enqueue(bmsg);
                    m_SendQueueWait.Set();
                }
            }

        }

        /// <summary>
        /// Prepare Bridge configuration from app.config
        /// </summary>
        private void GetConfig() {
            m_Cname = "u2mBridge@unknown.host.org";
            m_Name = "Classroom Presenter Unicast to Multicast Bridge on unknown host";
            try {
                string hostname = Dns.GetHostName();
                IPHostEntry entry = Dns.GetHostEntry(hostname);
                m_Cname = "u2mBridge@" + entry.HostName;
                m_Name = "Classroom Presenter Unicast to Multicast Bridge on " + hostname;
            }
            catch { }

            string tmp = System.Configuration.ConfigurationManager.AppSettings[this.GetType().ToString() + ".Cname"];
            if ((tmp != null) && (tmp.Trim() != "")) {
                Trace.WriteLine("Setting Cname from app.config: " + tmp, this.GetType().ToString());
                m_Cname = tmp.Trim();
            }

            tmp = System.Configuration.ConfigurationManager.AppSettings[this.GetType().ToString() + ".Name"];
            if ((tmp != null) && (tmp.Trim() != "")) {
                Trace.WriteLine("Setting Name from app.config: " + tmp, this.GetType().ToString());
                m_Name = tmp.Trim();
            }

            /// Note: FEC of 100% is probably as far to the conservative side as we need to go.  Setting it down
            /// around 20 actually works fine most of the time (wired).  One challanging scenario that has been observed is
            /// deleting a lot of strokes(ca 40) with one sweep of the eraser.  100% and 10mS IPD were needed to
            /// make sure none of the strokes were missed.
            m_Fec = 100;
            tmp = System.Configuration.ConfigurationManager.AppSettings[this.GetType().ToString() + ".Fec"];
            if ((tmp != null) && (tmp.Trim() != "")) {
                ushort tmpfec;
                if (ushort.TryParse(tmp, out tmpfec)) {
                    if ((tmpfec > 0) && (tmpfec <= 1000)) { 
                        Trace.WriteLine("Setting Fec from app.config: " + tmpfec.ToString(), this.GetType().ToString());
                        m_Fec = tmpfec;
                    }
                }
            }

            /// Note: previous tests have shown that there is insignificant benefit to increasing IPD beyond about 10mS.
            /// Using a value of 3mS works fine in most cases on wired networks.  One exception is the
            /// erase scenario noted above.
            m_InterpacketDelay = 10;
            tmp = System.Configuration.ConfigurationManager.AppSettings[this.GetType().ToString() + ".InterpacketDelay"];
            if ((tmp != null) && (tmp.Trim() != "")) {
                short tmpipd;
                if (short.TryParse(tmp, out tmpipd)) {
                    if ((tmpipd >= 0) && (tmpipd <= 30)) {
                        Trace.WriteLine("Setting InterpacketDelay from app.config: " + tmpipd.ToString(), this.GetType().ToString());
                        m_InterpacketDelay = tmpipd;
                    }
                }
            }

            int multicastPort = 5004;
            IPAddress multicastAddress = IPAddress.Parse("234.3.0.1");

            tmp = System.Configuration.ConfigurationManager.AppSettings[this.GetType().ToString() + ".MulticastPort"];
            if ((tmp != null) && (tmp.Trim() != "")) {
                int tmpmp;
                if (int.TryParse(tmp, out tmpmp)) {
                    Trace.WriteLine("Setting MulticastPort from app.config: " + tmpmp.ToString(), this.GetType().ToString());
                    multicastPort = tmpmp;
                }
            }

            tmp = System.Configuration.ConfigurationManager.AppSettings[this.GetType().ToString() + ".MulticastAddress"];
            if ((tmp != null) && (tmp.Trim() != "")) {
                IPAddress tmpma;
                if (IPAddress.TryParse(tmp, out tmpma)) {
                    Trace.WriteLine("Setting MulticastAddress from app.config: " + tmpma.ToString(), this.GetType().ToString());
                    multicastAddress = tmpma;
                }
            }

            m_MulticastEndpoint = new IPEndPoint(multicastAddress,multicastPort);
        }

        #region IDisposable Members

        protected override void Dispose(bool disposing) {
            base.Dispose(disposing);
            if (disposing) {
                m_Disposing = true;
                m_SendQueueWait.Set();
                this.m_BeaconService.Dispose();
                if (m_RtpSender != null) {
                    m_RtpSender.Dispose();
                    m_RtpSender = null;
                }
                if (m_RtpSession != null) {
                    try {
                        m_RtpSession.Dispose();
                    }
                    catch { }
                    m_RtpSession = null;
                }
                m_RtpParticipant = null;
                if (m_SendThread != null) {
                    if (!m_SendThread.Join(5000)) {
                        m_SendThread.Abort();
                    }
                }
            }
        }

        #endregion

        /// <summary>
        /// The override of SendingQueue.Send used by the BeaconService.  Chunk, serialize and pass to the main send method.
        /// </summary>
        /// <param name="message"></param>
        /// <param name="priority"></param>
        public override void Send(Message message, UW.ClassroomPresenter.Model.MessagePriority priority) {
            Group receivers = message.Group != null ? message.Group : Group.AllParticipant;
            Chunk[] chunks;
            // Serialize the message and split it into chunks with the chunk encoder.
            // The encoder updates the current message- and chunk sequence numbers.
            using (Synchronizer.Lock(this)) {
                chunks = this.m_Encoder.MakeChunks(message, ref this.m_ChunkSequence);
            }

            foreach (Chunk c in chunks) {
                using (MemoryStream stream = new MemoryStream((int)(this.m_Encoder.MaximumChunkSize * 2))) {
                    stream.Position = 0;
                    this.m_Encoder.EncodeChunk(stream, c);
                    byte[] buffer = stream.GetBuffer();
                    int length = (int)stream.Length;
                    MessageTags tags = new MessageTags();
                    tags.BridgePriority = MessagePriority.Higher;
                    this.Send(buffer,length, tags);
                }
            }

        }

        /// <summary>
        /// SendingQueue override: unused in this context.
        /// </summary>
        public override UW.ClassroomPresenter.Model.Network.ClassroomModel Classroom {
            get { throw new Exception("The method or operation is not implemented."); }
        }

        /// <summary>
        /// A helper class implementing IComparable to use with PriorityQueue.
        /// </summary>
        private class BridgeMessage: IComparable<BridgeMessage> {
            public BufferChunk Buffer;
            private ulong m_Priority;
            public MessagePriority BridgePriority;
            private static long c_Sequence;

            public BridgeMessage(BufferChunk bc, MessagePriority priority) {
                Buffer = bc;
                BridgePriority = priority;

                //For the purpose of the PriorityQueue, turn realtime into higher.
                MessagePriority adjustedPriority = priority.Equals(MessagePriority.RealTime) ? MessagePriority.Higher : priority;

                // Divide the ulong space into 6 segments (from MessagePriority.Lowest to RealTime).
                // Then add a global sequence number, so we sort first by priority, then by sequence.
                // Notice that MessagePriority values range from -2 to +3.
                this.m_Priority = ((ulong.MaxValue / 6))
                    * (5 - ((ulong)adjustedPriority + 2))
                    + ((ulong)Interlocked.Increment(ref c_Sequence));
            }

            #region IComparable<BridgeMessage> Members

            public int CompareTo(BridgeMessage other) {
                return this.m_Priority.CompareTo(other.m_Priority);
            }

            #endregion
        }    
    
    }
}

#endif