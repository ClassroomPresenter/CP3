// $Id: RTPMessageReceiver.cs 1463 2007-09-25 19:30:52Z linnell $
#if RTP_BUILD

using System;
using System.Collections.Generic;
using System.Diagnostics;
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

namespace UW.ClassroomPresenter.Network.RTP {
    /// <summary>
    /// Processes messages received over the RTP network interface, and
    /// attempts to recover dropped messages by sending NACKs back to the original sender.
    /// </summary>
    /// <remarks>
    /// There exists one <see cref="RTPMessageReceiver"/> on each client for every 
    /// participant connected to the classroom.
    /// </remarks>
    public class RTPMessageReceiver : IDisposable {
        private readonly PresenterModel m_Model;
        private readonly RTPMessageSender m_Sender;
        private readonly RtpStream m_RtpStream;

        private readonly ReceiveContext m_Context;
        private readonly object[] m_ContextArgs;
        private readonly IFormatter m_Serializer;
        private readonly MessageProcessingQueue m_Queue;

        private readonly ChunkAssembler m_Assembler;

        private bool m_Disposed;

        public RTPMessageReceiver(RTPMessageSender sender, RtpStream stream, PresenterModel model, ClassroomModel classroom, ParticipantModel participant) {
            this.m_Model = model;
            this.m_Sender = sender;
            this.m_RtpStream = stream;

            this.m_Context = new ReceiveContext(model, classroom, participant);
            this.m_ContextArgs = new object[] { this.m_Context };
            this.m_Queue = new MessageProcessingQueue(this);

            this.m_Serializer = new BinaryFormatter();

            this.m_Assembler = new ChunkAssembler();
            this.m_Assembler.Nack += new ChunkAssembler.NackDelegate(this.HandleAssemblerNack);

            this.m_RtpStream.FrameReceived += new RtpStream.FrameReceivedEventHandler(this.HandleFrameReceived);
        }

        #region IDisposable Members

        ~RTPMessageReceiver() {
            this.Dispose(false);
        }

        public void Dispose() {
            this.Dispose(true);
            System.GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing) {
            if(this.m_Disposed) return;
            if(disposing) {
                this.m_RtpStream.FrameReceived -= new RtpStream.FrameReceivedEventHandler(this.HandleFrameReceived);
                this.m_Sender.NackManager.Discard(this.m_RtpStream.SSRC, Range.UNIVERSE);
                this.m_Queue.Dispose();
            }
            this.m_Disposed = true;
        }

        #endregion

        /// <summary>
        /// Processes a message received over the RTP network interface.
        /// </summary>
        private void HandleFrameReceived(object sender, RtpStream.FrameReceivedEventArgs args) {
            Debug.Assert(args.RtpStream == this.m_RtpStream);

            BufferChunk chunk = args.Frame;

            try {
                // Attempt to deserialize the contents of the frame.
                using (MemoryStream stream = new MemoryStream(chunk.Buffer, chunk.Index, chunk.Length)) {
                    object message = this.m_Serializer.Deserialize(stream);

                    // If the message is a chunked, process it with the Chunk Assembler.
                    // (Messages don't have to be chunked, in which case they are processed
                    // immediately below).  If the chunk is the last remaining chunk composing
                    // a message, then the Assembler returns the completed message.
                    if (message is Chunk) {
                        Chunk chunked = ((Chunk)message);
                        // Ensure that no more NACKs are sent for this chunk, if it was recovered.
                        if (chunked.FrameSequence > ulong.MinValue)
                            this.m_Sender.NackManager.Discard(this.m_RtpStream.SSRC, chunked.FrameSequence);

                        // Ensure that no futile NACKs are sent for unrecoverable chunks.
                        if (chunked.OldestRecoverableFrame - 1 > ulong.MinValue)
                            this.m_Sender.NackManager.Discard(this.m_RtpStream.SSRC,
                                new Range(ulong.MinValue, chunked.OldestRecoverableFrame - 1));

                        message = this.m_Assembler.Add(chunked);

                    } else if (message is RtpNackMessage) {
                        // Otherwise, if the message is a NACK, delegate it to the RTP sender,
                        // which attempts to resend chunks from its buffer.
                        RtpNackMessage nack = ((RtpNackMessage)message);
                        this.m_Sender.ProcessNack(nack);
                        return;
                    }

                    // If we have a valid/complete message, queue it for processing on the separate
                    // message execution thread.
                    if (message is IEnumerable<object>) {
                        foreach (object decoded in (IEnumerable<object>)message) {
                            if (decoded is Message) {
                                // NOTE: Added by CMPRINCE for testing network performance
                                // Save this message
                                this.m_Model.ViewerState.Diagnostic.LogMessageRecv((Message)decoded);

                                Debug.WriteLine(string.Format("Received message: {0} bytes, type {1}",
                                    chunk.Length, decoded.GetType()), this.GetType().ToString());
                                Debug.Indent();
                                try {
                                    this.m_Queue.ProcessMessage((Message)decoded);
                                } finally {
                                    Debug.Unindent();
                                }
                            } else {
                                Trace.WriteLine("Received invalid message: " + decoded != null ? decoded.GetType().ToString() : null,
                                    this.GetType().ToString());
                            }
                        }
                    } else if (message != null) {
                        Trace.WriteLine("Received invalid message: " + message.GetType().ToString(),
                            this.GetType().ToString());
                    }
                }
            }

            catch(Exception e) {
                // The application should not crash on account of malformed messages sent from remote clients.
                // Therefore, we catch all exceptions.  We only want to print an error message.
                Trace.WriteLine("Error deserializing a message: " + e.ToString() + "\r\n" + e.StackTrace, this.GetType().ToString());
            }
        }

        /// <summary>
        /// Executes completed messages on a separate thread from that of the RTP networking thread.
        /// </summary>
        private class MessageProcessingQueue : ThreadEventQueue {
            private readonly RTPMessageReceiver m_Receiver;
            private Message m_Waiting;

            public MessageProcessingQueue(RTPMessageReceiver receiver) {
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
            this.m_Sender.NackManager.Nack(this.m_RtpStream.SSRC, missingSequences);
        }
    }
}

#endif
