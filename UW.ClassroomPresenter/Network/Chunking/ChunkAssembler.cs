// $Id: ChunkAssembler.cs 1871 2009-05-26 19:29:56Z cmprince $

using System;
using System.Collections.Generic;
using System.Diagnostics;

using UW.ClassroomPresenter.Model;
using UW.ClassroomPresenter.Model.Network;
using UW.ClassroomPresenter.Model.Presentation;
using UW.ClassroomPresenter.Network.Messages;

using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;

namespace UW.ClassroomPresenter.Network.Chunking {
    /// <summary>
    /// Assembles chunked messages and sends NACKs when dropped chunks are detected.
    /// </summary>
    public class ChunkAssembler {
        private readonly BinaryFormatter m_Formatter = new BinaryFormatter();
        private MessageAssembler NewestMessageAssember;
        private ulong m_GreatestFrameSequence = ulong.MinValue;
        private DisjointSet m_ReceivedFrameSequences;
        private DisjointSet m_ReceivedMessages;
        private Dictionary<ulong, List<Chunk>> m_ChunksWaitingForDependencies = new Dictionary<ulong, List<Chunk>>();

        /// <summary>
        /// Default constructor.
        /// </summary>
        public ChunkAssembler() { }

        /// <summary>
        /// Event called whenever missing frames are detected.
        /// The range of sequence numbers of the missing frames
        /// are passed as a parameter, indicating that a NACK
        /// should be sent requesting retransmission of those frames.
        /// </summary>
        public event NackDelegate Nack;

        /// <summary>
        /// Attempts to assemble a chunk from a chunked messages,
        /// and sends a NACK when dropped chunks are detected.
        /// </summary>
        /// <param name="chunk">The received chunk to process</param>
        /// <returns>If the chunk was the last remaining chunk needed to complete a message,
        /// returns the deserialized message; otherwise, <code>null</code></returns>
        public IEnumerable<object> Add(Chunk chunk) {
            if (chunk.FrameSequence > 0) {
                // Check if we have seen this packet before. If so, do nothing.
                if (DisjointSet.Contains(ref this.m_ReceivedFrameSequences, chunk.FrameSequence))
                    yield break;
                DisjointSet.Add(ref this.m_ReceivedFrameSequences, chunk.FrameSequence);

                // Save space in the ReceivedSequenceNumbers queue by adding all chunks
                // that we can't expect to receive anymore.
                if (chunk.OldestRecoverableFrame > ulong.MinValue)
                    DisjointSet.Add(ref this.m_ReceivedFrameSequences, new Range(ulong.MinValue, chunk.OldestRecoverableFrame - 1));

                // Check the frame sequence number to see if any frames were skipped.
                // If so, then it is likely that the frame has been dropped, so it is
                // necessary to send a NACK.
                if (chunk.FrameSequence > this.m_GreatestFrameSequence + 1ul) {
                    //Debug.WriteLine(string.Format("Frames #{0}-{1} were dropped ({2} total)! Requesting replacements (oldest recoverable is #{3})...",
                    //    this.m_GreatestFrameSequence + 1ul, chunk.FrameSequence - 1ul, chunk.FrameSequence - this.m_GreatestFrameSequence - 1,
                    //    chunk.OldestRecoverableFrame), this.GetType().ToString());
                    if (this.Nack != null) {
                        // Send a NACK which requests rebroadcast of all the skipped frames.
                        Range range = new Range(
                            Math.Max(this.m_GreatestFrameSequence + 1ul, chunk.OldestRecoverableFrame),
                            chunk.FrameSequence - 1ul);
                        this.Nack(range);
                    }
                }

                // Otherwise, don't do anything special.  These two branches are for debugging.
                else if (chunk.FrameSequence == this.m_GreatestFrameSequence) {
                    // Duplicate chunk received, or else the first chunk ever received.
                } else if (chunk.FrameSequence < this.m_GreatestFrameSequence && chunk.FrameSequence > ulong.MinValue) {
                    Debug.WriteLine(string.Format("%%% Frame #{0} (message #{1}, chunk #{2} of {3}, {4} bytes) received out of order (off by {5})",
                        chunk.FrameSequence,
                        chunk.MessageSequence,
                        chunk.ChunkSequenceInMessage + 1,
                        chunk.NumberOfChunksInMessage,
                        chunk.Data.Length,
                        this.m_GreatestFrameSequence - chunk.FrameSequence),
                        this.GetType().ToString());
                }

                // Assuming the chunk has not been duplicated or received out of order,
                // update our variable containing the highest sequence number seen
                // so far so we know which future frames have been dropped.
                if (chunk.FrameSequence > this.m_GreatestFrameSequence)
                    this.m_GreatestFrameSequence = chunk.FrameSequence;
            }

            // If the message depends on a message that has not yet been received,
            // add it to the queue of chunks waiting for that message.
            // Then, STOP PROCESSING the message.
            // Assemble(chunk) will get called by FlushWaitingChunks after
            // the dependencies are satisfied.
            if (chunk.MessageDependency > ulong.MinValue && !DisjointSet.Contains(ref this.m_ReceivedMessages, chunk.MessageDependency)) {
                List<Chunk> waiters;
                if (!this.m_ChunksWaitingForDependencies.TryGetValue(chunk.MessageDependency, out waiters))
                    this.m_ChunksWaitingForDependencies[chunk.MessageDependency] = waiters = new List<Chunk>();
                Debug.WriteLine(string.Format("@@@ Unsatisfied dependency: message #{0} (chunk #{1} of {2}) depends on #{3}; {4} chunks are already waiting.",
                    chunk.MessageSequence,
                    chunk.ChunkSequenceInMessage + 1,
                    chunk.NumberOfChunksInMessage,
                    chunk.MessageDependency, waiters.Count));
                waiters.Add(chunk);
                yield break;
            }

            // Flush any dependencies of messages we can no longer expect to recover.
            // Do this by finding the difference between the range of all unrecoverable
            // message sequence numbers and the sequence numbers that have been received.
            // FlushWaitingChunks is called on each of the unsatisfiable sequence numbers.
            if (chunk.OldestRecoverableMessage > ulong.MinValue) {
                Range unrecoverable = new Range(ulong.MinValue, chunk.OldestRecoverableMessage - 1);
                DisjointSet unsatisfiable = unrecoverable;
                DisjointSet.Remove(ref unsatisfiable, this.m_ReceivedMessages);
                if(unsatisfiable != null)
                    foreach (ulong unsatisfied in unsatisfiable)
                        this.FlushWaitingChunks(unsatisfied);
                DisjointSet.Add(ref this.m_ReceivedMessages, unrecoverable);
            }

            // If the dependency is already satisfied (or not specified),
            // attempt to assemble the chunk into a completed message.
            foreach (object message in this.Assemble(chunk))
                yield return message;
        }

        /// <summary>
        /// Attempts to assemble the chunk into a completed message.
        /// If this is the last chunk of a message, the completed message
        /// is returned, and any unsatisfied dependencies that are now
        /// satisfied by the completed message are recursively assembled.
        /// All such assembled message are also returned via the enumeration.
        /// </summary>
        /// <remarks>
        /// This method is called by <see cref="Add"/> and <see cref="FlushWaitingChunks"/>,
        /// after all chunk dependencies are satisfied.
        /// </remarks>
        /// <param name="chunk">The chunk to assemble.</param>
        /// <returns>
        /// The enumeration of decoded messages.
        /// The enumeration may be empty or may
        /// have arbitrarily many members if dependencies were
        /// (recursively) satisfied by this chunk.
        /// </returns>
        protected IEnumerable<object> Assemble(Chunk chunk) {
            // Process single-chunk messages immediately.
            if (chunk.NumberOfChunksInMessage <= 1) {
                // Don't create a MessageAssembler for singleton chunks.
                // Instead, just return the message immediately.
                using (MemoryStream ms = new MemoryStream(chunk.Data)) {
#if GENERIC_SERIALIZATION
                    yield return PacketTypes.DecodeMessage( null, new SerializedPacket( ms ) );
#else
                    yield return this.m_Formatter.Deserialize(ms);
#endif

                    // The message has been completed, so flush any chunks which were waiting for it.
                    foreach (object dependent in this.FlushWaitingChunks(chunk.MessageSequence))
                        yield return dependent;
                    yield break;
                }
            }

            // For multi-chunk messages, we first attempt to find an existing MessageAssembler
            // instance for the message to which the chunk belongs (based on the range of chunk
            // sequence numbers the message spans).
            MessageAssembler assembler = this.NewestMessageAssember, previous = null;
            object message;
            for (; ; ) {
                bool done, remove, complete;

                // If there does not exist any assembler for which IsInRange(chunk) returned true,
                // create one to hold the chunk.
                if (assembler == null) {
                    Debug.WriteLine(string.Format("Creating a new MessageAssembler to manage multipart message (message #{0}, chunks #{1}-{2})",
                        chunk.MessageSequence,
                        chunk.ChunkSequenceInMessage + 1,
                        chunk.NumberOfChunksInMessage),
                        this.GetType().ToString());

                    assembler = new MessageAssembler(chunk.MessageSequence,
                        chunk.NumberOfChunksInMessage, this.m_Formatter);

                    // Insert the assembler as the first entry in our linked list,
                    // since it is most likely to be used by subsequent chunks.
                    assembler.NextOldestAssembler = this.NewestMessageAssember;
                    this.NewestMessageAssember = assembler;
                }

                // See if the chunk belongs to the current assembler.
                if (assembler.MessageSequence == chunk.MessageSequence) {
                    // If so, add the chunk to it, and we can stop searching.
                    assembler.Add(chunk);
                    done = true;

                    // If the message has been fully assembled, process it
                    // and remove the no-longer-needed assembler.
                    complete = assembler.IsComplete;
                    if (complete) {
                        message = assembler.DeserializeMessage();
                        remove = true;
                    } else {
                        message = null;
                        remove = false;
                    }
                }

                else if (assembler.MessageSequence < chunk.OldestRecoverableMessage) {
                    // For each message assembler that is waiting for more chunks (and to which the current
                    // chunk does not belong), make sure it will be possible to complete the message in 
                    // the future.  If the sender reports that its OldestRecoverableFrame is greater than
                    // the sequence number of any frame yet needed to complete the message, then no
                    // NACK we send can ever satisfy our needs, so we discard the message completely
                    // (removing the assembler from the linked list).
                    Debug.WriteLine(string.Format("### Giving up on message #{0} (chunks #{0}-{1}): the oldest available chunk is {2}!",
                        chunk.MessageSequence,
                        chunk.ChunkSequenceInMessage + 1,
                        chunk.NumberOfChunksInMessage,
                        chunk.OldestRecoverableMessage), this.GetType().ToString());
                    remove = true;
                    message = null;
                    done = false;
                    complete = false;
                }
                else {
                    remove = false;
                    message = null;
                    done = false;
                    complete = false;
                }

                // If the assembler is no longer useful, remove it from the linked list.
                // (There are a couple of conditions, above, under which this might happen.)
                if (remove) {
                    if (previous == null) {
                        this.NewestMessageAssember = assembler.NextOldestAssembler;
                    } else {
                        previous.NextOldestAssembler = assembler.NextOldestAssembler;
                    }
                }

                // If an assembler was found which accepted the chunk, we're done.
                // (There are a couple of conditions, above, under which this might happen.)
                if (done) {
                    if (complete) {
                        yield return message;

                        // The message has been completed, so flush any chunks which were waiting for it.
                        foreach (object dependent in this.FlushWaitingChunks(chunk.MessageSequence))
                            yield return dependent;
                    }
                    yield break;
                } else {
                    // Get the next assembler.  Do not break from the loop if there
                    // is no "next" assembler, since one will be created.
                    previous = assembler;
                    assembler = assembler.NextOldestAssembler;
                }
            }
        }

        /// <summary>
        /// Called when the given dependency has been satisfied,
        /// to recursively call <see cref="Assemble"/> on all chunks
        /// which were waiting for that dependency.
        /// </summary>
        /// <param name="dependency">The dependency which has been satisfied.</param>
        /// <returns>
        /// The enumeration of decoded messages returned by <see cref="Assemble"/>,
        /// for each completed dependency.  The enumeration may be empty or may
        /// have arbitrarily many members.
        /// </returns>
        protected IEnumerable<object> FlushWaitingChunks(ulong dependency) {
            DisjointSet.Add(ref this.m_ReceivedMessages, dependency);

            List<Chunk> waiting;
            if (this.m_ChunksWaitingForDependencies.TryGetValue(dependency, out waiting)) {
                Debug.WriteLine(string.Format("@@@ Satisfied dependency: {0} chunks were waiting for message #{1}.",
                    waiting.Count, dependency));
                this.m_ChunksWaitingForDependencies.Remove(dependency);
                foreach (Chunk chunk in waiting)
                    foreach (object message in this.Assemble(chunk))
                        yield return message;
            }
        }

        /// <summary>
        /// Assembles a single message from its chunks, and keeps track of which
        /// chunks are still needed.
        /// </summary>
        /// <remarks>
        /// Also forms a linked list with other <see cref="MessageAssembler">MessageAssemblers</see>.
        /// </remarks>
        protected class MessageAssembler {
            private readonly BinaryFormatter m_Formatter;

            private readonly ulong m_MessageSequence;
            private readonly byte[][] m_Data;
            private ulong m_Remaining;

            /// <summary>
            /// The next entry in the linked list used by <see cref="ChunkAssembler"/>.
            /// </summary>
            public MessageAssembler NextOldestAssembler;

            /// <summary>
            /// Gets the message sequence number of the chunks comprising this message.
            /// </summary>
            public ulong MessageSequence {
                get { return this.m_MessageSequence; }
            }

            /// <summary>
            /// Creates a new <see cref="MessageAssembler"/>
            /// covering the specified range.
            /// </summary>
            /// <param name="message">
            /// The message chunk sequence number of the chunks comprising the full message.
            /// <seealso cref="Chunk.MessageSequence"/>
            /// </param>
            /// <param name="count">
            /// The number of chunks in the full message.
            /// <seealso cref="Chunk.NumberOfChunksInMessage"/>
            /// </param>
            /// <param name="deserializer">
            /// A deserializer which will be used to decode the completed message,
            /// once all chunks have been added.
            /// </param>
            public MessageAssembler(ulong message, ulong count, BinaryFormatter deserializer) {
                this.m_MessageSequence = message;
                this.m_Formatter = deserializer;
                this.m_Data = new byte[count][];
                this.m_Remaining = count;
            }

            /// <summary>
            /// Gets whether all chunks in the message have been received.
            /// </summary>
            public bool IsComplete {
                get {
                    return this.m_Remaining == 0;
                }
            }

            /// <summary>
            /// Gets whether the given chunk is part of the message
            /// being assembled by this <see cref="MessageAssembler"/>.
            /// </summary>
            /// <param name="chunk">
            /// The chunk to test.
            /// </param>
            /// <returns>
            /// Whether the chunk's sequence number is in range.
            /// </returns>
            public bool IsInRange(Chunk chunk) {
                return chunk.MessageSequence == this.m_MessageSequence;
            }

            /// <summary>
            /// Adds a chunk to the message.
            /// The chunk must be part of the message;
            /// that is, <see cref="IsInRange">IsInRange(<paramref name="chunk"/>)</see>
            /// must return <c>true</c>.
            /// </summary>
            /// <param name="chunk">
            /// The chunk to add to the message.
            /// </param>
            public void Add(Chunk chunk) {
                ulong index = chunk.ChunkSequenceInMessage;
                ulong chunks = ((ulong)this.m_Data.LongLength);
                if (index < 0 || index >= chunks)
                    throw new ArgumentException("Chunk is not part of the message being assembled by this MessageAssembler.", "chunk");

                // If the chunk is not a duplicate, insert its contents into the data array.
                // (The chunked message cannot be deserialized until all chunks have been received.)
                if (this.m_Data[index] == null) {
                    Debug.Assert(this.m_Remaining >= 1);

                    // This chunk has not been received before.
                    this.m_Data[index] = chunk.Data;
                    this.m_Remaining--;

                    Debug.WriteLine(string.Format("Received message #{0}, chunk #{0} of {1} ({2} bytes); {3} chunks remaining.",
                        chunk.MessageSequence,
                        chunk.ChunkSequenceInMessage + 1,
                        chunk.NumberOfChunksInMessage,
                        chunk.Data.LongLength,
                        this.m_Remaining),
                        this.GetType().ToString());
                }
            }

            /// <summary>
            /// Once a message is complete, deserializes the message from its chunks.
            /// </summary>
            /// <returns>The deserialized message</returns>
            public object DeserializeMessage() {
                if (!this.IsComplete)
                    throw new InvalidOperationException("Cannot deserialize the chunked message until all chunks have been received.");

                // First count the total size of the concatenated data.
                long total = 0;
                foreach (byte[] chunk in this.m_Data)
                    total += chunk.LongLength;

                // Concatenate all of the data into a single array.
                // TODO: Make a new "MemoryStream" class which can read directly from the jagged 2D array.
                byte[] serialized = new byte[total];
                total = 0;
                foreach (byte[] chunk in this.m_Data) {
                    chunk.CopyTo(serialized, total);
                    total += chunk.LongLength;
                }

                using (MemoryStream ms = new MemoryStream(serialized)) {
#if GENERIC_SERIALIZATION
                    return PacketTypes.DecodeMessage( null, new SerializedPacket( ms ) );
#else
                    return this.m_Formatter.Deserialize(ms);
#endif
                }
            }
        }

        /// <summary>
        /// The delegate used by the <see cref="ChunkAssembler.Nack"/> event.
        /// </summary>
        /// <param name="missingSequences">
        /// The frame sequence numbers which were not received and
        /// which should be NACKed.
        /// </param>
        public delegate void NackDelegate(DisjointSet missingSequences);
    }
}
