// $Id: RTPConnection.cs 774 2005-09-21 20:22:33Z pediddle $

using System;
using System.Net;
using MSR.LST;
using MSR.LST.Net.Rtp;
using System.Collections;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using UW.ClassroomPresenter.Network.Messages;
using UW.ClassroomPresenter.Network.Chunking;
using UW.ClassroomPresenter.Network.RTP;
using System.Threading;
using UW.ClassroomPresenter.Model;

using System.Diagnostics;

namespace UW.ClassroomPresenter.Test.Network.Common {
    /// <summary>
    /// Summary description for RTPConnection.
    /// </summary>
    
    #region Receive
    public class RTPReceiveConnection {
        private readonly RtpSession m_Session;
        private readonly RtpParticipant m_Participant;
        private ArrayList streams = new ArrayList();
        private BinaryFormatter m_bf = new BinaryFormatter();
        private NetworkArchiver m_archiver;

        public RTPReceiveConnection(IPEndPoint ip, NetworkArchiver na) {
            RtpEvents.RtpStreamAdded += new MSR.LST.Net.Rtp.RtpEvents.RtpStreamAddedEventHandler(this.handleRtpStreamAdded);
            RtpEvents.RtpStreamRemoved += new MSR.LST.Net.Rtp.RtpEvents.RtpStreamRemovedEventHandler(this.handleRtpStreamRemoved);
            this.m_Participant = new RtpParticipant("Recorder", "Recorder");
            this.m_Session = new RtpSession(ip, this.m_Participant, true, true);
            this.m_archiver = na;
        }

        public void Dispose() {
            RtpEvents.RtpStreamAdded -= new MSR.LST.Net.Rtp.RtpEvents.RtpStreamAddedEventHandler(this.handleRtpStreamAdded);
            RtpEvents.RtpStreamRemoved -= new MSR.LST.Net.Rtp.RtpEvents.RtpStreamRemovedEventHandler(this.handleRtpStreamRemoved);
        }

        private void handleRtpStreamAdded(object sender, MSR.LST.Net.Rtp.RtpEvents.RtpStreamEventArgs ea) {
            this.streams.Add(ea.RtpStream);
            ea.RtpStream.FrameReceived += new MSR.LST.Net.Rtp.RtpStream.FrameReceivedEventHandler(this.handleFrameReceived);
        }

        private void handleRtpStreamRemoved(object sender, MSR.LST.Net.Rtp.RtpEvents.RtpStreamEventArgs ea) {
            if (this.streams.Contains(ea.RtpStream)) {
                this.streams.Remove(ea.RtpStream);
            }
            ea.RtpStream.FrameReceived -= new MSR.LST.Net.Rtp.RtpStream.FrameReceivedEventHandler(this.handleFrameReceived);
        }

        private void handleFrameReceived(object sender, MSR.LST.Net.Rtp.RtpStream.FrameReceivedEventArgs ea) {
            BufferChunk chunk = ea.Frame;
            string source = ea.RtpStream.Properties.CName;
            try {
                using (MemoryStream ms = new MemoryStream(chunk.Buffer)) {
                    object msg = this.m_bf.Deserialize(ms);
                    if (msg is UW.ClassroomPresenter.Network.Messages.Message) {
                        NetworkEvent newEvent = new NetworkMessageEvent((Message)msg, DateTime.Now.Ticks, source);
                        this.m_archiver.AppendEvent(newEvent);
                        EventArchivedDelegate ead = new EventArchivedDelegate(this.EventArchived);
                        ead(this, newEvent);
                    } else if (msg is UW.ClassroomPresenter.Network.Chunking.Chunk) {
                        NetworkEvent newEvent = new NetworkChunkEvent((UW.ClassroomPresenter.Network.Chunking.Chunk)msg, DateTime.Now.Ticks, source);
                        this.m_archiver.AppendEvent(newEvent);
                        EventArchivedDelegate ead = new EventArchivedDelegate(this.EventArchived);
                        ead(this, newEvent);
                    } else if (msg is UW.ClassroomPresenter.Network.RTP.RtpNackMessage) {
                        NetworkEvent newEvent = new NetworkNACKMessageEvent((UW.ClassroomPresenter.Network.RTP.RtpNackMessage)msg, DateTime.Now.Ticks, source);
                        this.m_archiver.AppendEvent(newEvent);
                        EventArchivedDelegate ead = new EventArchivedDelegate(this.EventArchived);
                        ead(this, newEvent);
                    }
                }
            } catch (Exception e) {
                Debug.WriteLine("EXCEPTION : " + e.ToString() + " : " + e.StackTrace);
            }
        }

        public event EventArchivedDelegate EventArchived;
    }

    public delegate void EventArchivedDelegate(object sender, NetworkEvent e);

    #endregion

    #region Send

    public class RTPSendConnection {
        private readonly RtpSession m_Session;
        private readonly RtpParticipant m_Participant;
        private readonly RtpSender m_Sender;
        public readonly SendingQueue m_Queue;
        private readonly BinaryFormatter m_bf = new BinaryFormatter();

        public RTPSendConnection(IPEndPoint ipe) {
            this.m_Participant = new RtpParticipant(Guid.NewGuid().ToString(), "Classroom Playback");
            this.m_Session = new RtpSession(ipe, this.m_Participant, true, false);
            this.m_Sender = this.m_Session.CreateRtpSenderFec("Classroom Presenter", PayloadType.dynamicPresentation, null, 0, 100);
            this.m_Queue = new SendingQueue(this);
        }

        public void Dispose() {
            //Disconnect
            this.m_Sender.Dispose();
            this.m_Session.Dispose();
        }

        public void Send(Message message) {
            using(MemoryStream ms = new MemoryStream()) {
                this.m_bf.Serialize(ms, message);
                this.m_Sender.Send(new BufferChunk(ms.GetBuffer(), 0, (int) ms.Length));
            }
        }

        public void Send(Chunk chunk) {
            using(MemoryStream ms = new MemoryStream()) {
                this.m_bf.Serialize(ms, chunk);
                this.m_Sender.Send(new BufferChunk(ms.GetBuffer(), 0, (int) ms.Length));
            }
        }

        public void Send(RtpNackMessage message) {
            using(MemoryStream ms = new MemoryStream()) {
                this.m_bf.Serialize(ms, message);
                this.m_Sender.Send(new BufferChunk(ms.GetBuffer(), 0, (int) ms.Length));
            }
        }
    }

    public class SendingQueue : ThreadEventQueue {
        private readonly RTPSendConnection m_Sender;
        private readonly SendMessageDelegate m_SendMessageDelegate;
        private readonly SendChunkDelegate m_SendChunkDelegate;
        private readonly SendNACKDelegate m_SendNACKDelegate;

        public SendingQueue(RTPSendConnection sender) {
            this.m_Sender = sender;
            this.m_SendMessageDelegate = new SendMessageDelegate(this.m_Sender.Send);
            this.m_SendChunkDelegate = new SendChunkDelegate(this.m_Sender.Send);
            this.m_SendNACKDelegate = new SendNACKDelegate(this.m_Sender.Send);
        }

        public void Send(Message message) {
            this.Post(this.m_SendMessageDelegate, new object[] { message });
        }

        public void Send(Chunk chunk) {
            this.Post(this.m_SendChunkDelegate, new object[] { chunk });
        }

        public void Send(RtpNackMessage message) {
            this.Post(this.m_SendNACKDelegate, new object[] { message });
        }

        private delegate void SendMessageDelegate(Message message);
        private delegate void SendChunkDelegate(Chunk chunk);
        private delegate void SendNACKDelegate(RtpNackMessage message);
    }

    #endregion
}
