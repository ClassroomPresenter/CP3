
#if RTP_BUILD
using System;
using System.Collections.Generic;
using System.Text;
using UW.ClassroomPresenter.Model;
using UW.ClassroomPresenter.Network.Messages;
using System.Threading;
using UW.ClassroomPresenter.Network.Chunking;
using System.IO;
using UW.ClassroomPresenter.Network.Messages.Presentation;
using UW.ClassroomPresenter.Network.Messages.Network;
using System.Diagnostics;

namespace UW.ClassroomPresenter.Network.TCP {
    /// <summary>
    /// Adds some message taxonomy to the UnicastToMulticastBridge
    /// </summary>
    /// <remarks>
    /// When bridging is done on a client (public node), we need to do a little bit
    /// of extra work to tag inbound messages so that the bridge will prioritize them
    /// correctly.
    /// </remarks>
    class ClientUnicastToMulticastBridge: UnicastToMulticastBridge {
        private bool m_Disposing;
        private Queue<ChunksAndPriority> m_SendQueue;
        private Thread m_SendThread;
        private EventWaitHandle m_SendQueueWait;
        private readonly Chunk.ChunkEncoder m_Encoder;
        private ulong m_ChunkSequence;


        public ClientUnicastToMulticastBridge(PresenterModel model): base(model) { 
            m_Disposing = false;
            this.m_ChunkSequence = 0;
            this.m_Encoder = new Chunk.ChunkEncoder();

            //make a queue and a thread
            m_SendQueueWait = new EventWaitHandle(false, EventResetMode.AutoReset);
            m_SendQueue = new Queue<ChunksAndPriority>();
            m_SendThread = new Thread(new ThreadStart(SendThread));
            m_SendThread.Start();
        }

        /// <summary>
        /// Since the message objects get modified during processing, we do the chunking synchronously here.  
        /// Otherwise we would end up sending a bunch of extra stuff to the bridge.
        /// </summary>
        /// <param name="message"></param>
        public void SendToBridge(Message message) {
            MessageTags tags = new MessageTags();
            tags.BridgePriority = GetBridgePriority(message);

            if (tags.BridgePriority == MessagePriority.Lowest) {
                //These are DeckSlideContentMessages which are big and are unused in the current implementation of the streaming/archiving system.
                Trace.WriteLine("Client U2M Bridge dropping message:" + message.ToString());
                return;
            }

            // Serialize the message and split it into chunks with the chunk encoder.
            // The encoder updates the current message- and chunk sequence numbers.
            Chunk[] chunks;
            using (Synchronizer.Lock(this)) {
                chunks = this.m_Encoder.MakeChunks(message, ref this.m_ChunkSequence);
                Trace.WriteLine("Client U2M Bridge MakeChunks returned " + chunks.Length.ToString() + " chunks for message " + message.ToString());
            }

            lock (m_SendQueue) {
                m_SendQueue.Enqueue(new ChunksAndPriority(chunks, tags));
            }
            m_SendQueueWait.Set();

        }


        private void SendThread() {
            while (!m_Disposing) {
                ChunksAndPriority chunks = null;
                lock (m_SendQueue) {
                    if (m_SendQueue.Count > 0) {
                        chunks = m_SendQueue.Dequeue();
                    }
                }
                if (chunks != null) {
                    foreach (Chunk c in chunks.Chunks) {
                        using (MemoryStream stream = new MemoryStream((int)(this.m_Encoder.MaximumChunkSize * 2))) {
                            stream.Position = 0;
                            this.m_Encoder.EncodeChunk(stream, c);
                            byte[] buffer = stream.GetBuffer();
                            base.Send(buffer, (int)stream.Length, chunks.Tags);
                        }
                    }
                }
                else {
                    m_SendQueueWait.WaitOne();
                }
            }

        
        }

        private MessagePriority GetBridgePriority(Message message) {
            if (message != null) {
                if (message is PresentationInformationMessage) {
                    if ((message.Child != null) && (message.Child is DeckInformationMessage)) {
                        if (message.Child.Child != null) {
                            if (message.Child.Child is DeckSlideContentMessage) {
                                return MessagePriority.Lowest;
                            }
                            else if (message.Child.Child is DeckTraversalMessage) {
                                return MessagePriority.Higher;
                            }
                        }
                    }
                }
                else if ((message is RealTimeInkSheetStylusDownMessage) ||
                    (message is RealTimeInkSheetStylusUpMessage) ||
                    (message is RealTimeInkSheetPacketsMessage)) {
                    return MessagePriority.RealTime;
                }
                else if ((message is InkSheetStrokesAddedMessage) || (message is InkSheetStrokesDeletingMessage)) {
                    return MessagePriority.Higher;
                }
                else if ((message is TextSheetMessage) || (message is SheetRemovedMessage)) {
                    return MessagePriority.Higher;
                }
                else if (message is InstructorMessage) {
                    if ((message.Child != null) && (message.Child is InstructorCurrentDeckTraversalChangedMessage)) {
                        return MessagePriority.Higher;
                    }
                }
            }
            return MessagePriority.Default;
        }

        public new void Dispose(bool disposing) {
            if (disposing) {
                m_Disposing = true;
                m_SendQueueWait.Set();
                if (!m_SendThread.Join(5000)) {
                    m_SendThread.Abort();
                }
            }
            base.Dispose(disposing);
        }

        private class ChunksAndPriority {
            public Chunk[] Chunks;
            public MessageTags Tags;

            public ChunksAndPriority(Chunk[] chunks, MessageTags tags) {
                this.Chunks = chunks;
                this.Tags = tags;
            }
        }

    }
}
#endif