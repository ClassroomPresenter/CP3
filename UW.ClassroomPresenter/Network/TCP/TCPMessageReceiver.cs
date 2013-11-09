
using System;
using System.Collections;
using System.Diagnostics;
using System.Threading;

using System.IO;
using System.Net.Sockets;
using System.Runtime.Serialization;

using UW.ClassroomPresenter.Model;
using UW.ClassroomPresenter.Model.Network;
using UW.ClassroomPresenter.Network.Messages;
using UW.ClassroomPresenter.Network.Messages.Network;
using UW.ClassroomPresenter.Network.Chunking;
using System.Collections.Generic;
using UW.ClassroomPresenter.Network.Messages.Presentation;

namespace UW.ClassroomPresenter.Network.TCP {
    /// <summary>
    /// Processes messages received over the TCP network interface
    /// </summary>
    /// <remarks>
    /// </remarks>
    public class TCPMessageReceiver : IDisposable {
        private readonly PresenterModel m_Model;
        private readonly ClassroomModel m_Classroom;
        private readonly ITCPReceiver m_Receiver;
        private readonly MessageProcessingQueue m_Queue;

        private readonly ChunkAssembler m_Assembler;
#if RTP_BUILD
        private ClientUnicastToMulticastBridge m_U2MBridge = null;
#endif
        private bool m_Disposed;

        public TCPMessageReceiver(ITCPReceiver receiver, PresenterModel model, ClassroomModel classroom) {

            this.m_Model = model;
            this.m_Classroom = classroom;
            this.m_Receiver = receiver;
            this.m_Assembler = new ChunkAssembler();

#if RTP_BUILD
            //If we are in the public role and the receiver enabled the client-side bridge, start the bridge.
            bool isPublicRole = false;
            using (Synchronizer.Lock(m_Model.SyncRoot)) {
                using (Synchronizer.Lock(m_Model.Participant.SyncRoot)) {
                    if (m_Model.Participant.Role is PublicModel) {
                        isPublicRole = true;
                    }
                }
            }
            if ((isPublicRole) && (receiver is TCPClient) && ((TCPClient)receiver).BridgeEnabled) {
                m_U2MBridge = new ClientUnicastToMulticastBridge(m_Model);
            }
#endif
            this.m_Queue = new MessageProcessingQueue(this);


            Thread thread = new Thread(new ThreadStart(this.ReceiveThread));
            thread.Name = "TCPMessageReceiver";
            thread.Start();
        }

        #region IDisposable Members

        ~TCPMessageReceiver() {
            this.Dispose(false);
        }

        public void Dispose() {
            this.Dispose(true);
            System.GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing) {
            if (this.m_Disposed) return;
            if (disposing) {
#if RTP_BUILD
                if (m_U2MBridge != null) {
                    this.m_U2MBridge.Dispose(true);
                    this.m_U2MBridge = null;
                }
#endif
                this.m_Queue.Dispose();
            }
            this.m_Disposed = true;
        }

        public bool Disposed {
            get { return this.m_Disposed; }
        }

        #endregion

        /// <summary>
        /// Processes a message received over the TCP network interface.
        /// </summary>
        private void ReceiveThread() {
            while (true) {
                try {
                    if (this.m_Disposed)
                        return;

                    object message = null;
                    ReceiveMessageAndParticipant rm;
                    
                    rm = (ReceiveMessageAndParticipant)this.m_Receiver.Read(); //does not block.
                    if (rm == null) { //probably because of empty receive queue
                        Thread.Sleep(50);
                        continue;
                    }
                    message = rm.Message;
                    ReceiveContext context = new ReceiveContext(m_Model,m_Classroom,rm.Participant);

                    // If the message is a chunked, process it with the Chunk Assembler.
                    // (Messages don't have to be chunked, in which case they are processed
                    // immediately below).  If the chunk is the last remaining chunk composing
                    // a message, then the Assembler returns the completed message.
                    if (message is Chunk) {
                        Chunk chunk = message as Chunk;
                        try {
                            Debug.WriteLine(string.Format("Received message #{0}, chunk #{1} of {2}, {3} bytes.",
                                chunk.MessageSequence,
                                chunk.ChunkSequenceInMessage + 1,
                                chunk.NumberOfChunksInMessage,
                                chunk.Data.Length),
                                this.GetType().ToString());

                            message = this.m_Assembler.Add((Chunk)message);

                        }
                        catch (Exception e) {
                            Trace.WriteLine(string.Format("Error deserializing a message from message #{0}, chunk #{1} of {2} ({3} bytes): {4}",
                                chunk.MessageSequence,
                                chunk.ChunkSequenceInMessage + 1,
                                chunk.NumberOfChunksInMessage,
                                chunk.Data.Length, e.ToString()),
                                this.GetType().ToString());
                            continue;
                        }
                    }

                    // If we have a valid/complete message, queue it for processing on the separate
                    // message execution thread.
                    if (message is IEnumerable<object>) {
                        foreach (object decoded in (IEnumerable<object>)message) {
                            if (decoded is Message) {
                                Message m = (Message)decoded;

                                this.m_Model.ViewerState.Diagnostic.LogMessageRecv(m);

                                //Debug.WriteLine(string.Format("Received message: {0} bytes, type {1}",
                                //    "???", m.GetType()), this.GetType().ToString());
                                Debug.WriteLine("Received message:" + m.ToString(),"TCPMessageReceiver");
                                //If enabled, do client-slide bridging
#if RTP_BUILD
                                if (this.m_U2MBridge != null) {
                                    this.m_U2MBridge.SendToBridge(m.Clone());
                                }
#endif
                                // Send it off for processing.
                                this.m_Queue.ProcessMessage(m, context);
                            }
                            else if (decoded != null) {
                                Trace.WriteLine("Received unknown message type: " + decoded.GetType().ToString(),
                                    this.GetType().ToString());
                            }
                        }
                    }
                    else if (message != null) { 
                        Trace.WriteLine("Received unknown message type: " + message.GetType().ToString(),
                            this.GetType().ToString());              
                    }

                }
                catch (Exception e) {
                    // The application should not crash on account of malformed messages sent from remote clients.
                    // Therefore, we catch all exceptions.  We only want to print an error message.
                    Trace.WriteLine("Error processing a message: " + e.ToString(),
                        this.GetType().ToString());
                }
            }
        }

        /// <summary>
        /// Executes completed messages on a separate thread from that of the TCP networking thread.
        /// </summary>
        private class MessageProcessingQueue : ThreadEventQueue {
            private readonly TCPMessageReceiver m_Receiver;
            private Message m_Waiting;

            public MessageProcessingQueue(TCPMessageReceiver receiver) {
                this.m_Receiver = receiver;
            }

            /// <summary>
            /// Enqueues a message for later execution on the message thread.
            /// If the message thread is busy, and other messages are already
            /// enqueued, the messages are combined (<see cref="Message.ZipInto"/>),
            /// which may help to avoid duplicating work in some situations.
            /// For example, multiple "Ink Erase" messages received in a row
            /// can be combined into a single message, which saves the UI work
            /// by only repainting itself once.
            /// </summary>
            /// <remarks>
            /// (In fact, there is no "queue" -- meaning a list of messages -- but
            /// rather the "zipped" messages provide all the functionality we need.)
            /// 
            /// FIXME:  If we zip messages from different remote nodes, we 
            /// call OnReceive for all of them with the context of the last. 
            /// 
            /// </remarks>
            /// <param name="message">The message to execute</param>
            public void ProcessMessage(Message message,ReceiveContext context) {
                using (Synchronizer.Lock(this)) {
                    if (this.m_Waiting == null) {
                        this.m_Waiting = message;
                        this.Post(delegate() {
                            Message waited;
                            using (Synchronizer.Lock(this)) {
                                if (this.m_Waiting != null) {
                                    waited = this.m_Waiting;
                                    this.m_Waiting = null;
                                }
                                else {
                                    return;
                                }
                            }

                            waited.OnReceive(context);
                        });
                    }
                    else {
                        message.ZipInto(ref this.m_Waiting);
                    }
                }
            }
        }
    }

    public class ReceiveMessageAndParticipant {
        public object Message;
        public ParticipantModel Participant;
        public ReceiveMessageAndParticipant(object message, ParticipantModel participant) {
            this.Message = message;
            this.Participant = participant;
        }
    }
}
