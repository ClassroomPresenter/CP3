// $Id: TCPMessageSender.cs 1363 2007-05-15 18:27:43Z fred $

using System;
using System.Collections;
using System.Diagnostics;
using System.Threading;

using System.Net;
using System.Net.Sockets;

using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;

using UW.ClassroomPresenter.Model;
using UW.ClassroomPresenter.Model.Network;
using UW.ClassroomPresenter.Network.Messages;
using UW.ClassroomPresenter.Network.Messages.Network;
using UW.ClassroomPresenter.Network.Chunking;
using UW.ClassroomPresenter.Network.Groups;
using UW.ClassroomPresenter.Network.Messages.Presentation;
using System.Reflection;

namespace UW.ClassroomPresenter.Network.TCP {
    public class TCPMessageSender : SendingQueue {
        private readonly PresenterModel m_Model;
        private readonly ClassroomModel m_Classroom;

        private readonly ITCPSender m_Sender;

        private readonly Chunk.ChunkEncoder m_Encoder;
        private ulong m_ChunkSequence;

        private readonly PresenterNetworkService m_PresenterNetworkService;
        private readonly StudentSubmissionNetworkService m_StudentSubmissionNetworkService;
        //private readonly SynchronizationNetworkService m_SynchronizationNetworkService;
        //private readonly ScriptingNetworkService m_ScriptingNetworkService;

        private bool m_Disposed;

        public TCPMessageSender(ITCPSender server, PresenterModel model, ClassroomModel classroom) {
            this.m_Model = model;
            this.m_Classroom = classroom;
            this.m_Sender = server;

            // Initialize the message chunking utilities.
            this.m_Encoder = new Chunk.ChunkEncoder();

            // Most of the same services are created as in RTPMessageSender, with the exception
            // (for now, at least) that there is no BeaconService.

            // Create the PresenterNetworkService which will watch for changes to the model and send messages.
            this.m_PresenterNetworkService = new PresenterNetworkService(this, this.m_Model);

            // Create the StudentSubmissionsNetworkService which will watch for requests to submit and send messages.
            this.m_StudentSubmissionNetworkService = new StudentSubmissionNetworkService(this, this.m_Model);

            // Create the SynchronizationNetworkService which will watch for all synchronization messages.
            //this.m_SynchronizationNetworkService = new SynchronizationNetworkService(this, this.m_Model);

            // Create the ScriptingNetworkService which will watch for all scripting messages.
            //this.m_ScriptingNetworkService = new ScriptingNetworkService(this, this.m_Model);

        }

        public override ClassroomModel Classroom {
            get { return this.m_Classroom; }
        }

        public void ForceUpdate(Group receivers) {
            this.m_PresenterNetworkService.ForceUpdate(receivers);
        }

        #region IDisposable Members

        protected override void Dispose(bool disposing) {
            base.Dispose(disposing);

            if(disposing) {
                if(this.m_Disposed) return;
                this.m_Disposed = true;

                //this.m_SynchronizationNetworkService.Dispose();
                this.m_StudentSubmissionNetworkService.Dispose();
                this.m_PresenterNetworkService.Dispose();
            }
        }

        #endregion


        public override void Send(Message message, MessagePriority priority) {
            Group receivers = message.Group != null ? message.Group : Group.AllParticipant;

            Chunk[] chunks;
            // Serialize the message and split it into chunks with the chunk encoder.
            // The encoder updates the current message- and chunk sequence numbers.
            using(Synchronizer.Lock(this)) {
                chunks = this.m_Encoder.MakeChunks(message, ref this.m_ChunkSequence);
            }
#if DEBUG
            string caller = "";
            StackTrace st = new StackTrace(false);
            if (st.GetFrame(2).GetMethod().IsConstructor) { 
                MethodBase mb = st.GetFrame(2).GetMethod();
                string n = mb.ReflectedType.FullName;
                caller = n.Substring(n.LastIndexOf('.') + 1) + ".ctor";
            }
            else {
                caller = st.GetFrame(2).GetMethod().Name;
            }
            Trace.WriteLine("Message send caller=" + caller + " sequence=" + chunks[0].MessageSequence.ToString() +
                " chunk count=" + chunks.Length.ToString() + " type=" + message.ToString() +
                " priority=" + priority.ToString());
#endif
            if (message.Tags != null) {
                Trace.WriteLine("  " + message.Tags.ToString());
            }
            else {
                Trace.WriteLine("  NO TAGS");
                message.Tags = new MessageTags();
            }

            // Send each chunk asynchronously, with the requested priority.
            Array.ForEach(chunks, delegate(Chunk chunk) {
                this.Post(delegate() {
                    this.m_Sender.Send(chunk, receivers, message.Tags);
                }, priority);
            });
        }
    }
}
