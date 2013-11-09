// $Id: FrameBuffer.cs 1112 2006-08-11 00:40:16Z pediddle $

using System;
using System.Collections.Generic;
using System.Diagnostics;

using UW.ClassroomPresenter.Model;
using UW.ClassroomPresenter.Model.Network;
using UW.ClassroomPresenter.Model.Presentation;

using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading;

namespace UW.ClassroomPresenter.Network.Chunking {
    /// <summary>
    /// A fixed-size rotating buffer for message chunks.
    /// </summary>
    /// <remarks>
    /// <para>
    /// When a message is sent by an <see cref="RTPMessageSender"/>, it is first broken
    /// into <see cref="Chunk"/>s by a <see cref="Chunk.ChunkEncoder"/>.  Each chunk is
    /// assigned <see cref="Chunk.ChunkSequence"><i>chunk</i> sequence</see> and
    /// <see cref="Chunk.FrameSequence"><i>frame</i> sequence</see> numbers.
    /// </para>
    /// <para>
    /// The chunk sequence is assigned when the chunk is first created, and is used by the receiver
    /// to reassemble many chunks into a complete message.  The frame sequence is assigned
    /// when the chunk is actually <i>sent</i>, and its name refers to the fact that the
    /// chunk comprises a single RTP message <i>frame</i>.
    /// </para>
    /// <para>
    /// Chunks are <see cref="Insert">inserted</see> into the <see cref="FrameBuffer"/>
    /// when they are sent; thus the <see cref="FrameBuffer"/>'s table is indexed by the chunk's 
    /// frame sequence number instead of its chunk sequence number.  This is because message 
    /// prioritization may cause chunks to be broadcast in a different order than they were created.
    /// </para>
    /// <para>
    /// If an RTP frame is dropped by an unreliable network, receivers detect this by noticing
    /// a <i>gap</i> in what should be consecutive frame sequence numbers.  The receivers
    /// then schedule a NACK message to be sent, requesting that the original sender rebroadcast
    /// the missing frames.
    /// </para>
    /// <para>
    /// When the <see cref="RTPMessageSender"/> receives a NACK, it invokes <see cref="FrameBuffer.Take"/>
    /// to see if the requested chunks are still available.  If so, <see cref="Take"/> temporarily
    /// removes the frames from the buffer and they are enqueued to be rebroadcast.  Once they are actually
    /// sent again, they are reinserted into the queue so that they can be re-NACKed.
    /// </para>
    /// </remarks>
    public class FrameBuffer {
        private ulong m_First;
        private ulong m_Oldest;
        private ulong m_Newest;

        private readonly Chunk[] m_Buffer;
        private readonly ulong m_BufferSize;

        /// <summary>
        /// FIXME: There must be a more efficient data structure
        /// that can keep track of the oldest sequence number in
        /// parallel with the oldest frame number.
        /// </summary>
        private readonly SortedList<ulong, ulong> m_MessageSequences;

        /// <summary>
        /// Creates a new <see cref="FrameBuffer"/>.
        /// </summary>
        /// <param name="size">
        /// The maximum number of frames that the buffer will
        /// hold before discarding old frames.
        /// </param>
        public FrameBuffer(ulong size) {
            this.m_Oldest = ulong.MaxValue;
            this.m_First = ulong.MaxValue;
            this.m_Newest = ulong.MinValue;

            this.m_BufferSize = size;
            this.m_Buffer = new Chunk[size];

            this.m_MessageSequences = new SortedList<ulong, ulong>();
        }

        /// <summary>
        /// Gets the <see cref="Chunk.FrameSequence">frame sequence number</see> of the oldest
        /// chunk which <i>might</i> still be in the queue.  This does not account for the fact
        /// that a chunk might have been temporarily removed from the buffer by <see cref="Take"/>,
        /// or that no chunk with a given frame sequence number might ever have been inserted.
        /// </summary>
        public ulong OldestRecoverableFrame {
            get { return this.m_Oldest; }
        }

        /// <summary>
        /// Gets the greatest <see cref="Chunk.FrameSequence">frame sequence number</see> of any
        /// chunk so far inserted into the buffer.
        /// </summary>
        public ulong NewestRecoverableFrame {
            get { return this.m_Newest; }
        }

        /// <summary>
        /// Gets smallest <see cref="Chunk.MessageSequence">message sequence number</see> that
        /// <i>might</i> still be in the queue.  This does not account for the fact that
        /// messages can be sent out of order (depending on their priority), or
        /// that a chunk might have been temporarily removed from the buffer by <see cref="Take"/>,
        /// or that no chunk with a given message sequence number might ever have been inserted.
        /// </summary>
        public ulong OldestRecoverableMessage {
            get {
                return this.m_MessageSequences.Count > 0
                    ? this.m_MessageSequences.Values[0]
                    : ulong.MinValue;
            }
        }

        /// <summary>
        /// Finds and removes the chunk with the given 
        /// <see cref="Chunk.FrameSequence">frame sequence number</see> from the buffer.
        /// <remarks>
        /// Note that this does NOT update <see cref="OldestRecoverableFrame"/>
        /// or <see cref="OldestRecoverableMessage"/>.  It is expected that the
        /// chunk will be reinserted into the buffer in the future.
        /// </remarks>
        /// </summary>
        /// <param name="frameSequence">
        /// The <see cref="Chunk.FrameSequence">frame sequence number</see> to extract
        /// from the buffer.
        /// </param>
        /// <param name="chunk">The chunk extracted from the buffer, if available.</param>
        /// <returns>
        /// <c>true</c> if the given <paramref name="frameSequence"/> was in the range
        /// <see cref="OldestRecoverableFrame"/> to <see cref="NewestRecoverableFrame"/>;
        /// <c>false</c> otherwise.  (A <c>true</c> return value does not imply that
        /// <paramref name="chunk"/> was set to non-null, since the chunk with the given
        /// <paramref name="frameSequence"/> might have been previously been removed from
        /// the buffer by <see cref="Take"/>, or might never have been inserted in the
        /// first place.)
        /// </returns>
        public bool Take(ulong frameSequence, out Chunk chunk) {
            if (this.m_Oldest <= frameSequence && frameSequence <= this.m_Newest) {
                ulong index = frameSequence % this.m_BufferSize;
                // FIXME: This shouldn't update OldestMessageSequence.
                // But if we don't, then Insert() will cause the count to get unbalanced.
                chunk = this.m_Buffer[index];
                this.Set(index, null);
                Debug.Assert(chunk == null || chunk.FrameSequence == frameSequence,
                    "FrameBuffer table entry has mismatched frame sequence number.");
                return true;
            } else {
                chunk = null;
                return false;
            }
        }

        /// <summary>
        /// Inserts the given chunk into the buffer.
        /// </summary>
        /// <remarks>
        /// <para>
        /// If the chunk's <see cref="Chunk.FrameSequence">frame sequence number</see> is
        /// higher than any previously inserted chunk, then <see cref="NewestRecoverableFrame"/>
        /// and <see cref="OldestRecoverableFrame"/> are updated to reflect the size of the buffer.
        /// Any chunks whose frame sequence numbers fall outside of that range are removed, and
        /// will no longer be recoverable under any circumnstances.
        /// </para>
        /// <para>
        /// On the other hand, if the chunk is older than <see cref="NewestRecoverableFrame"/>,
        /// then it is simply inserted into the buffer, unless it is also older than
        /// <see cref="OldestRecoverableFrame"/>, in which case it is discarded.
        /// </para>
        /// </remarks>
        /// <param name="value">The chunk to insert.</param>
        public void Insert(Chunk value) {
            if (value == null)
                throw new ArgumentNullException("value", "The data inserted into the buffer cannot be null.");

            // Compute the index into the buffer table where the new data will go.
            ulong index = value.FrameSequence % this.m_BufferSize;

            // If the chunk is not inserted in ascending order, then all we have to
            // do is insert it into the same index in the table as Take() would access.
            if (this.m_Newest >= value.FrameSequence && value.FrameSequence != 0) {
                if (this.m_Oldest <= value.FrameSequence && value.FrameSequence <= this.m_Newest)
                    this.Set(index, value);
                return;
            }

            // Store the new chunk in the buffer.
            this.Set(index, value);

            // We must special-case the first time a chunk is buffered.
            if (this.m_First == ulong.MaxValue) {
                this.m_Oldest = this.m_First = this.m_Newest = value.FrameSequence;
            } else {
                // If the frame sequences are not sequential, we need to clean up invalid entries
                // that were not already overwritten.  We use "++expired" to skip the most recent
                // valid entry.  The "failsafe" ensures that no more than m_BufferSize iterations
                // are made (more wouldn't hurt but would be wasteful).  The index is hashed into
                // the table each time in case it wraps around to the beginning.
                for (ulong failsafe = 0, expired = this.m_Newest; ++expired < value.FrameSequence && failsafe++ < this.m_BufferSize; )
                    this.Set(expired % this.m_BufferSize, null);

                // Regardless, we need to set the new m_Newest.
                Debug.Assert(value.FrameSequence > this.m_Newest);
                this.m_Newest = value.FrameSequence;

                // We need to find the new oldest entry.  Until the first m_BufferSize entries
                // are added, the first entry cannot have been overwritten and it is the oldest.
                Debug.Assert(value.FrameSequence > this.m_First);
                if (value.FrameSequence - this.m_First < this.m_BufferSize) {
                    this.m_Oldest = this.m_First;
                }

                    // Otherwise the oldest entry must increase.  If sequence numbers were skipped,
                    // it may increase by more than one if additional entries are null.
                    // The loop must terminate because the new, non-null entry was set above.
                else
                    while (this.m_Buffer[++this.m_Oldest % this.m_BufferSize] == null) ;
            }
        }

        /// <summary>
        /// Updates <see cref="m_Buffer"/> with the specified chunk at the specified index,
        /// and also keeps the <see cref="OldestMessageSequence"/> value up-to-date.
        /// </summary>
        /// <param name="index">The index at which to place the chunk.</param>
        /// <param name="chunk">
        /// The chunk to insert, or <c>null</c> if the chunk is being removed from the buffer.
        /// </param>
        private void Set(ulong index, Chunk chunk) {
            ulong count;
            ulong message;

            // First decrement the count for the OLD chunk's message sequence number.
            Chunk old = this.m_Buffer[index];
            if (old != null) {
                message = old.MessageSequence;
                if (message > ulong.MinValue)
                    if (this.m_MessageSequences.TryGetValue(message, out count) && count > 0)
                        if (count <= 1)
                            this.m_MessageSequences.Remove(message);
                        else
                            this.m_MessageSequences[message] = count - 1;
                    else throw new ApplicationException("Unmatched insertions and removals into the FrameBuffer.");
            }

            // Now insert the chunk in the buffer.
            this.m_Buffer[index] = chunk;

            // Finally increment the count for the NEW chunk's message sequence number.
            if (chunk != null) {
                message = chunk.MessageSequence;
                if (message > ulong.MinValue)
                    if (this.m_MessageSequences.TryGetValue(message, out count))
                        this.m_MessageSequences[message] = count + 1;
                    else this.m_MessageSequences[message] = 1;
            }
        }
    }
}
