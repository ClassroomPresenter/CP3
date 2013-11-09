#if RTP_BUILD

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;

using MSR.LST;
using MSR.LST.Net.Rtp;

using UW.ClassroomPresenter.Model;
using UW.ClassroomPresenter.Model.Network;
using UW.ClassroomPresenter.Network.Messages;
using UW.ClassroomPresenter.Network.Messages.Network;
using UW.ClassroomPresenter.Network.Chunking;
using System.Diagnostics;
using UW.ClassroomPresenter.Network.RTP;

namespace UW.ClassroomPresenter.Network.CXP {
    class CXPCapabilityMessageReceiver : IDisposable {

        private readonly PresenterModel m_Model;
        private readonly CXPCapabilityMessageSender m_Sender;

        private readonly ReceiveContext m_Context;
        private readonly object[] m_ContextArgs;
        private readonly MessageProcessingQueue m_Queue;

        private readonly ChunkAssembler m_Assembler;
        private uint m_RemoteSSRC;
        private bool m_Disposed;

        public CXPCapabilityMessageReceiver(CXPCapabilityMessageSender sender, uint ssrc, PresenterModel model, ClassroomModel classroom, ParticipantModel participant) {
            this.m_Model = model;
            this.m_Sender = sender;
            this.m_RemoteSSRC = ssrc;

            this.m_Context = new ReceiveContext(model, classroom, participant);
            this.m_ContextArgs = new object[] { this.m_Context };
            this.m_Queue = new MessageProcessingQueue(this);

            this.m_Assembler = new ChunkAssembler();
            this.m_Assembler.Nack += new ChunkAssembler.NackDelegate(this.HandleAssemblerNack);
        }

        #region IDisposable Members

        ~CXPCapabilityMessageReceiver() {
            this.Dispose(false);
        }

        public void Dispose() {
            this.Dispose(true);
            System.GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing) {
            if(this.m_Disposed) return;
            if(disposing) {
                this.m_Sender.NackManager.Discard(this.m_RemoteSSRC, Range.UNIVERSE);
                this.m_Queue.Dispose();
            }
            this.m_Disposed = true;
        }

        #endregion


        public uint RemoteSsrc {
            get { return m_RemoteSSRC; }
        }

        /// <summary>
        /// Processes a message received over a Capability channel
        /// </summary>
        public void Receive(object message) {
            // If the message is a chunk, process it with the Chunk Assembler.
            // (Messages don't have to be chunked, in which case they are processed
            // immediately below).  If the chunk is the last remaining chunk composing
            // a message, then the Assembler returns the completed message.
            object unwrappedMessage = null;
            if (message is CapabilityMessageWrapper) {
                unwrappedMessage = ((CapabilityMessageWrapper)message).Message;
                if (unwrappedMessage is Chunk) {
                    Chunk chunked = (Chunk)unwrappedMessage;
                    // Ensure that no more NACKs are sent for this chunk, if it was recovered.
                    if (chunked.FrameSequence > ulong.MinValue)
                        this.m_Sender.NackManager.Discard(this.m_RemoteSSRC, chunked.FrameSequence);

                    // Ensure that no futile NACKs are sent for unrecoverable chunks.
                    if (chunked.OldestRecoverableFrame - 1 > ulong.MinValue)
                        this.m_Sender.NackManager.Discard(this.m_RemoteSSRC,
                            new Range(ulong.MinValue, chunked.OldestRecoverableFrame - 1));

                    message = this.m_Assembler.Add(chunked);
                }
                else if (unwrappedMessage is RtpNackMessage) {
                    // Otherwise, if the message is a NACK, delegate it to the RTP sender,
                    // which attempts to resend chunks from its buffer.
                    RtpNackMessage nack = ((RtpNackMessage)unwrappedMessage);
                    Guid destinationId = ((CapabilityMessageWrapper)message).RecipientId;
                    this.m_Sender.ProcessNack(nack, destinationId);
                    return;
                }
            }

            // If we have a valid/complete message, queue it for processing on the separate
            // message execution thread.
            if (message is IEnumerable<object>) {
                foreach (object decoded in (IEnumerable<object>)message) {
                    if (decoded is Message) {
                        // NOTE: Added by CMPRINCE for testing network performance
                        // Save this message
                        this.m_Model.ViewerState.Diagnostic.LogMessageRecv((Message)decoded);

                        Debug.WriteLine(string.Format("Received message: type {0}",
                            decoded.GetType()), this.GetType().ToString());
                        Debug.Indent();
                        try {
                            this.m_Queue.ProcessMessage((Message)decoded);
                        }
                        finally {
                            Debug.Unindent();
                        }
                    }
                    else {
                        Trace.WriteLine("Received invalid message: " + decoded != null ? decoded.GetType().ToString() : null,
                            this.GetType().ToString());
                    }
                }
            }
            else if (message != null) {
                Trace.WriteLine("Received invalid message: " + message.GetType().ToString(),
                    this.GetType().ToString());
            }
        }

        /// <summary>
        /// Executes completed messages on a separate thread from that of the RTP networking thread.
        /// </summary>
        private class MessageProcessingQueue : ThreadEventQueue {
            private readonly CXPCapabilityMessageReceiver m_Receiver;
            private Message m_Waiting;

            public MessageProcessingQueue(CXPCapabilityMessageReceiver receiver) {
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
            /// </remarks>
            /// <param name="message">The message to execute</param>
            public void ProcessMessage(Message message) {                
                using (Synchronizer.Lock(this)) {
                    if(this.m_Waiting == null) {
                        this.m_Waiting = message;
                        this.Post(delegate() {
                            Message waited;
                            using (Synchronizer.Lock(this)) {
                                if (this.m_Waiting != null) {
                                    waited = this.m_Waiting;
                                    this.m_Waiting = null;
                                } else {
                                    return;
                                }
                            }

                            waited.OnReceive(this.m_Receiver.m_Context);
                        });
                    } else {
                        message.ZipInto(ref this.m_Waiting);
                    }
                }
            }
        }

        private void HandleAssemblerNack(DisjointSet missingSequences) {
            this.m_Sender.NackManager.Nack(this.m_RemoteSSRC, missingSequences);
        }
    }
}
#endif
