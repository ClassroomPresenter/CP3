// $Id: Chunk.cs 1871 2009-05-26 19:29:56Z cmprince $

using System;
using System.Collections;
using System.Diagnostics;
using System.Threading;

using UW.ClassroomPresenter.Model;
using UW.ClassroomPresenter.Model.Network;
using UW.ClassroomPresenter.Model.Presentation;
using UW.ClassroomPresenter.Network.Messages;

using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;

namespace UW.ClassroomPresenter.Network.Chunking {
    /// <summary>
    /// A class which encapsulates a part of a serialized network message.
    /// A group of consecutive chunks given to a <see cref="ChunkAssembler"/>
    /// can reassemble the original serialized data.
    /// </summary>
    /// <remarks>
    /// Chunks are 
    /// </remarks>
    [Serializable]
    public class Chunk : IGenericSerializable {
        public readonly byte[] Data;

        /// <summary>
        /// The sequence number of the message of which this chunk is a part.
        /// </summary>
        public readonly ulong MessageSequence;

        /// <summary>
        /// The index of this chunk within a message.
        /// </summary>
        public readonly ulong ChunkSequenceInMessage;

        /// <summary>
        /// The total number of chunks comprising the message
        /// (a positive integer).
        /// </summary>
        public readonly ulong NumberOfChunksInMessage;

        /// <summary>
        /// The <see cref="MessageSequence"/> of another chunk upon which
        /// the message contained in this chunk depends.
        /// The chunk won't be processed by the <see cref="ChunkAssembler"/>
        /// until the chunk identified by this field has been received
        /// and all dependencies have been satisfied.
        /// </summary>
        public ulong MessageDependency;

        /// <summary>
        /// The sequence number of this individual chunk as it is sent over the network.
        /// Chunks may not be sent in the same order they are created,
        /// and chunks from different messages may be interspersed,
        /// so this field exists to support detection of dropped chunks in real time
        /// in order to send NACKs.
        /// </summary>
        /// <remarks>
        /// The FrameSequence is set after the message is sent,
        /// so is not a parameter to the constructor.
        /// </remarks>
        public ulong FrameSequence;

        /// <summary>
        /// The oldest <see cref="MessageSequence"/> that can be recovered
        /// from the sender with a NACK.
        /// </summary>
        /// <remarks>
        /// This number changes each time a chunk is resent due to a NACK,
        /// and depends on the current state of the <see cref="FrameBuffer"/>.
        /// </remarks>
        /// <seealso cref="FrameBuffer"/>
        public ulong OldestRecoverableMessage;

        /// <summary>
        /// The oldest <see cref="FrameSequence"/> that can be recovered
        /// from the sender with a NACK.
        /// </summary>
        /// <remarks>
        /// This number changes each time a chunk is resent due to a NACK,
        /// and depends on the current state of the <see cref="FrameBuffer"/>.
        /// </remarks>
        /// <seealso cref="FrameBuffer"/>
        public ulong OldestRecoverableFrame;

        /// <summary>
        /// Creates a new chunk with the specified fields.
        /// </summary>
        private Chunk(byte[] data, ulong message, ulong chunk, ulong count) {
            this.Data = data;
            this.MessageSequence = message;
            this.ChunkSequenceInMessage = chunk;
            this.NumberOfChunksInMessage = count;
        }

        /// <summary>
        /// Serializes chunks and updates the message sequence number.
        /// </summary>
        public class ChunkEncoder {
            private readonly BinaryFormatter m_Formatter = new BinaryFormatter();

            /// <summary>
            /// The value returned by <see cref="MaximumChunkSize"/>.
            /// </summary>
            private uint m_MaximumChunkSize = 16 * 1024;

            /// <summary>
            /// The maximum size, in bytes, of the data payload of each chunk.
            /// Serialized messages which are larger than this are split
            /// into multiple chunks.
            /// </summary>
            /// <remarks>
            /// Note that the serialized size of a chunk will still
            /// be larger than this number due to overhead.
            /// </remarks>
            public uint MaximumChunkSize {
                get { return this.m_MaximumChunkSize; }
                set { this.m_MaximumChunkSize = value; }
            }

            /// <summary>
            /// Serializes the given message and constructs a number of
            /// chunks containing the serialized data.
            /// </summary>
            /// <param name="message">The message to serialize.</param>
            /// <param name="messageSequence">
            /// A reference to a counter which will be incremented.
            /// This becomes the value of each chunk's
            /// <see cref="Chunk.MessageSequence"/> field.
            /// </param>
            /// <returns>
            /// A non-empty array containing the created chunks.
            /// </returns>
            public Chunk[] MakeChunks(Message message, ref ulong messageSequence) {
                ++messageSequence;
                ulong currentChunkSequence = 0;
                ulong maxSize = this.m_MaximumChunkSize;

                using(MemoryStream stream = new MemoryStream()) {
                    // Serialize the message.
#if GENERIC_SERIALIZATION
                    message.Serialize().WriteToStream( stream );
#else
                    this.m_Formatter.Serialize( stream, message );
#endif

                    // Calculate the number of chunks needed to span the message and allocate an array to hold them.
                    ulong length = ((ulong) stream.Length);
                    ulong count = (length / maxSize) + (length % maxSize == 0ul ? 0ul : 1ul);
                    Chunk[] chunks = new Chunk[count];

                    stream.Position = 0;
                    int index = 0;
                    for(ulong amountLeft; (amountLeft = length - ((ulong) stream.Position)) > 0; index++) {
                        // Read the deserialized data into a small array.
                        byte[] chunk = new byte[amountLeft < maxSize ? amountLeft : maxSize];
                        int read = stream.Read(chunk, 0, chunk.Length);
                        if(read != chunk.Length)
                            throw new ApplicationException("Couldn't read enough data from MemoryStream.");

                        // Create a chunk, updating the sequence numbers.
                        chunks[index] = new Chunk(chunk, messageSequence, currentChunkSequence++, count);
                    }

                    return chunks;
                }
            }

            /// <summary>
            /// Writes a chunk to a data stream.
            /// </summary>
            public void EncodeChunk(Stream stream, Chunk chunk) {
                // FIXME: More efficient encoding of chunks might be possible here.
                //   Custom encoding would require a Decode method to be used by RTPMessageReceiver.
#if GENERIC_SERIALIZATION
                chunk.Serialize().WriteToStream( stream );
#else
                this.m_Formatter.Serialize( stream, chunk );
#endif
            }
        }

        #region IGenericSerialization

        public SerializedPacket Serialize() {
            SerializedPacket p = new SerializedPacket( this.GetClassId() );
            p.Add( (this.Data != null) ? 
                SerializedPacket.SerializeByteArray( this.Data ) : SerializedPacket.NullPacket( PacketTypes.ByteArrayId ) );
            p.Add( SerializedPacket.SerializeULong( this.MessageSequence ) );
            p.Add( SerializedPacket.SerializeULong( this.ChunkSequenceInMessage ) );
            p.Add( SerializedPacket.SerializeULong( this.NumberOfChunksInMessage ) );
            p.Add( SerializedPacket.SerializeULong( this.MessageDependency ) );
            p.Add( SerializedPacket.SerializeULong( this.FrameSequence ) );
            p.Add( SerializedPacket.SerializeULong( this.OldestRecoverableMessage ) );
            p.Add( SerializedPacket.SerializeULong( this.OldestRecoverableFrame ) );
            return p;
        }

        public Chunk( SerializedPacket p ) {
            SerializedPacket.VerifyPacket( p, this.GetClassId() );
            this.Data = (!SerializedPacket.IsNullPacket( p.PeekNextPart() )) ?
                SerializedPacket.DeserializeByteArray( p.PeekNextPart() ) : null; p.GetNextPart();
            this.MessageSequence = SerializedPacket.DeserializeULong( p.GetNextPart() );
            this.ChunkSequenceInMessage = SerializedPacket.DeserializeULong( p.GetNextPart() );
            this.NumberOfChunksInMessage = SerializedPacket.DeserializeULong( p.GetNextPart() );
            this.MessageDependency = SerializedPacket.DeserializeULong( p.GetNextPart() );
            this.FrameSequence = SerializedPacket.DeserializeULong( p.GetNextPart() );
            this.OldestRecoverableMessage = SerializedPacket.DeserializeULong( p.GetNextPart() );
            this.OldestRecoverableFrame = SerializedPacket.DeserializeULong( p.GetNextPart() );
        }

        public int GetClassId() {
            return PacketTypes.ChunkId;
        }

        #endregion
    }
}
