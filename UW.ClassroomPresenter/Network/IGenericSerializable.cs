using System;
using System.Collections;
using System.Text;
using System.IO;
using System.Diagnostics;

namespace UW.ClassroomPresenter.Network {

    /// <summary>
    /// Helper class for encapsulating the network packet
    /// </summary>
    public class SerializedPacket {
        #region Constants

        /// <summary>
        /// The number of bytes in an interger
        /// </summary>
        public const int INTSIZE = 4;

        #endregion

        #region PublicMembers

        /// <summary>
        /// 
        /// </summary>
        public int Type = -1;

        #endregion

        #region Protected Members

        /// <summary>
        /// The Size of the Packet in Bytes
        /// </summary>
        protected int CachedSize = -1;
        /// <summary>
        /// The Number of Children
        /// </summary>
        protected int NumChildren = -1;

        protected int Counter = 0;
        /// <summary>
        /// Array of either ALL SerializedPackets, or ALL bytes
        /// </summary>
        protected ArrayList /* Of SerializedPacket -or- Byte */ Data = new ArrayList();

        #endregion

        #region Add

        /// <summary>
        /// Add a byte to the object
        /// </summary>
        /// <param name="b"></param>
        public void Add( byte b ) {
            if( this.NumChildren != -1 )
                throw new Exception( "Can't Add Both Binary and Object Data" );
            this.Data.Add( b );
            this.CachedSize = -1;
        }

        public void Add( byte[] data ) {
            if( this.NumChildren != -1 )
                throw new Exception( "Can't Add Both Binary and Object Data" );
            this.Data.AddRange( data );
            this.CachedSize = -1;
        }

        public void Add( SerializedPacket p ) {
            if( this.NumChildren == -1 && this.Data.Count != 0 )
                throw new Exception( "Can't Add Both Binary and Object Data" );
            this.Data.Add( p );
            this.NumChildren = this.Data.Count;
            this.CachedSize = -1;
        }

        #endregion

        #region Get

        public SerializedPacket GetPart( int i ) {
            if( this.NumChildren < 0 )
                throw new Exception( "Value Type, function invalid" );
            return (SerializedPacket)this.Data[i];
        }

        public SerializedPacket PeekNextPart() {
            if( this.NumChildren < 0 )
                throw new Exception( "Value Type, function invalid" );
            return (SerializedPacket)this.Data[this.Counter];

        }

        public SerializedPacket GetNextPart() {
            if( this.NumChildren < 0 )
                throw new Exception( "Value Type, function invalid" );
            return (SerializedPacket)this.Data[this.Counter++];
        }

        #endregion

        #region Constructors

        /// <summary>
        /// Construct a new SerializedPacket structure
        /// </summary>
        public SerializedPacket() {
        }

        /// <summary>
        /// Construct a new SerializedPacket structure
        /// </summary>
        public SerializedPacket( int type ) {
            this.Type = type;
        }

        /// <summary>
        /// Construct a new SerializedPacket from a stream
        /// </summary>
        /// <param name="s">The stream to read from</param>
        public SerializedPacket( Stream s ) {
            this.ReadFromStream( s );
        }

        #endregion

        #region Stream Methods

        /// <summary>
        /// Write this object to a stream
        /// </summary>
        /// <param name="s">The output stream</param>
        public void WriteToStream( Stream s ) {
            this.CalculateSize();

            // Write the header
            WriteIntToStream( (int)0x3A37DB82, s );
            WriteIntToStream( this.Type, s );
            WriteIntToStream( this.CachedSize, s );
            WriteIntToStream( this.NumChildren, s );

            // Write the data
            if( this.NumChildren == -1 ) {
#if DEBUG
                foreach( object o in Data ) {
                    if( !(o is byte) ) {
                        throw new Exception( "Only arrays of bytes or SerializedPackets are allowed!" );
                    }
                }
#endif
                byte[] data = (byte[])this.Data.ToArray( typeof(byte) );
                Debug.Assert( data.Length == this.CachedSize );
                s.Write( data, 0, data.Length );
            } else {
                foreach( SerializedPacket p in this.Data ) {
                    p.WriteToStream( s );
                }
            }
        }

        /// <summary>
        /// Build this object from a stream
        /// </summary>
        /// <param name="s">The input stream</param>
        public void ReadFromStream( Stream s ) {
            int header = ReadIntFromStream( s );
            if( header != (int)0x3A37DB82 ) {
                return;
            }
            this.Type = ReadIntFromStream( s );
            this.CachedSize = ReadIntFromStream( s );
            this.NumChildren = ReadIntFromStream( s );
            if( this.NumChildren == -1 ) {
                // Read the byte data
                byte[] data;
                try {
                    if( this.CachedSize >= 0 )
                        data = new byte[this.CachedSize];
                    else
                        throw new Exception();
                } catch {
                    throw new Exception();
                }
                int result = s.Read( data, 0, this.CachedSize );
                if( result != this.CachedSize ) {
                    throw new Exception( "End of Stream Found Early" );
                } else {
                    this.Data.AddRange( data );
                }
            } else {
                // Read the Serialized Packet Data
                for( int i = 0; i < this.NumChildren; i++ ) {
                    this.Data.Add( new SerializedPacket( s ) );
                }
            }
        }

        /// <summary>
        /// Write an integer to a stream
        /// </summary>
        /// <param name="i">The integer to write</param>
        /// <param name="s">The output stream</param>
        public static void WriteIntToStream( int i, Stream s ) {
            s.WriteByte( (byte)((i >> 0) & 0xFF) );
            s.WriteByte( (byte)((i >> 8) & 0xFF) );
            s.WriteByte( (byte)((i >> 16) & 0xFF) );
            s.WriteByte( (byte)((i >> 24) & 0xFF) );
        }
        /// <summary>
        /// Read an integer from a stream
        /// </summary>
        /// <param name="s">The input stream</param>
        /// <returns>The integer read</returns>
        public static int ReadIntFromStream( Stream s ) {
            byte[] buf = new byte[4];
            s.Read( buf, 0, 4 );
            int a = ((int)buf[0] << 0);
            int b = ((int)buf[1] << 8);
            int c = ((int)buf[2] << 16);
            int d = ((int)buf[3] << 24);

            return (int)(a | b | c | d);
        }

        #endregion

        #region Helper Functions

        /// <summary>
        /// Calculate the size of the serialized packet including all children
        /// </summary>
        /// <returns>The size of this packet</returns>
        protected int CalculateSize() {
            if( this.CachedSize < 0 ) {
                int childsize = 0;

                if( NumChildren == -1 ) {       // Bytes
                    // Get the Data Size in bytes
                    childsize = Data.Count;
                } else {                        // SerializedPackets
                    // Get the size of each child
                    foreach( SerializedPacket p in this.Data ) {
                        childsize += p.CalculateSize();
                    }
                }
                this.CachedSize = childsize;
            }
            return this.CachedSize + (3 * INTSIZE);
        }

        #endregion

        #region Static Helpers

        /// <summary>
        /// Verify that the given SerializedPacket is of the given type
        /// </summary>
        /// <param name="s">The packet</param>
        /// <param name="type">The type to check for</param>
        public static void VerifyPacket( SerializedPacket s, int type ) {
            if( s.Type != type && s.Type != -type ) {
                throw new Exception( "This packet type is not appropriate for this function" );
            }
        }

        public static SerializedPacket NullPacket( int type ) {
            SerializedPacket packet = new SerializedPacket();
            packet.Type = -type;
            return packet;
        }

        public static bool IsNullPacket( SerializedPacket p ) {
            return ( p.Type < 0 ) ? true : false;
        }

        #region Bool
        /// <summary>
        /// Serialize a Boolean Value
        /// </summary>
        /// <param name="i"></param>
        /// <returns></returns>
        public static SerializedPacket SerializeBool( bool i ) {
            SerializedPacket packet = new SerializedPacket();
            packet.Type = PacketTypes.BoolId;
            packet.Add( (i) ? (byte)1 : (byte)0 );
            return packet;
        }
        /// <summary>
        /// Deserialize a Boolean Value
        /// </summary>
        /// <param name="s"></param>
        /// <returns></returns>
        public static bool DeserializeBool( SerializedPacket s ) {
            VerifyPacket( s, PacketTypes.BoolId );
            if( s.Data.Count != 1 )
                throw new Exception( "Incorrect Data Length" );
            return ((byte)s.Data[0] == 1) ? true : false;
        }
        #endregion

        #region Byte
        /// <summary>
        /// Serialize a Byte Value
        /// </summary>
        /// <param name="i">The Byte</param>
        /// <returns>The Packet</returns>
        public static SerializedPacket SerializeByte( byte i ) {
            SerializedPacket packet = new SerializedPacket();
            packet.Type = PacketTypes.ByteId;
            packet.Data.Add( i );
            return packet;
        }
        /// <summary>
        /// Deserialize a Byte Value
        /// </summary>
        /// <param name="s">The Packet</param>
        /// <returns>The byte</returns>
        public static byte DeserializeByte( SerializedPacket s ) {
            VerifyPacket( s, PacketTypes.ByteId );
            if( s.Data.Count != 1 )
                throw new Exception( "Incorrect Data Length" );
            return (byte)s.Data[0];
        }
        #endregion

        #region Integer32
        /// <summary>
        /// Serialize an Int32
        /// </summary>
        /// <param name="i">The Int</param>
        /// <returns>The Packet</returns>
        public static SerializedPacket SerializeInt( int i ) {
            SerializedPacket packet = new SerializedPacket();
            packet.Type = PacketTypes.Integer32Id;
            packet.Add( (byte)((i >> 0) & 0xFF) );
            packet.Add( (byte)((i >> 8) & 0xFF) );
            packet.Add( (byte)((i >> 16) & 0xFF) );
            packet.Add( (byte)((i >> 24) & 0xFF) );
            return packet;
        }
        /// <summary>
        /// Deserialize an Int32
        /// </summary>
        /// <param name="s">The Packet</param>
        /// <returns>The int</returns>
        public static int DeserializeInt( SerializedPacket s ) {
            VerifyPacket( s, PacketTypes.Integer32Id );
            if( s.Data.Count != 4 )
                throw new Exception( "Incorrect Data Length" );
            return (int)( ((byte)s.Data[0] << 0 ) |
                          ((byte)s.Data[1] << 8 ) |
                          ((byte)s.Data[2] << 16) |
                          ((byte)s.Data[3] << 24) );
        }
        #endregion

        #region Float
        /// <summary>
        /// Serialize a Float
        /// </summary>
        /// <param name="f">The float</param>
        /// <returns>The Packet</returns>
        public static SerializedPacket SerializeFloat( float f ) {
            SerializedPacket packet = new SerializedPacket();
            packet.Type = PacketTypes.FloatId;
            packet.Add( BitConverter.GetBytes( f ) );
            return packet;
        }
        /// <summary>
        /// Deserialize a Float
        /// </summary>
        /// <param name="s">The packet</param>
        /// <returns>The float</returns>
        public static float DeserializeFloat( SerializedPacket s ) {
            VerifyPacket( s, PacketTypes.FloatId );
            return BitConverter.ToSingle( (byte[])s.Data.ToArray( typeof(byte) ), 0 );
        }
        #endregion

        #region Integer64
        /// <summary>
        /// Serialize an Int64
        /// </summary>
        /// <param name="i">The Int64</param>
        /// <returns>The Packet</returns>
        public static SerializedPacket SerializeInt64( Int64 i ) {
            SerializedPacket packet = new SerializedPacket();
            packet.Type = PacketTypes.Integer64Id;
            packet.Add( (byte)((i >> 0) & 0xFF) );
            packet.Add( (byte)((i >> 8) & 0xFF) );
            packet.Add( (byte)((i >> 16) & 0xFF) );
            packet.Add( (byte)((i >> 24) & 0xFF) );
            packet.Add( (byte)((i >> 32) & 0xFF) );
            packet.Add( (byte)((i >> 40) & 0xFF) );
            packet.Add( (byte)((i >> 48) & 0xFF) );
            packet.Add( (byte)((i >> 56) & 0xFF) );
            return packet;
        }
        /// <summary>
        /// Deserialize an Int64
        /// </summary>
        /// <param name="s">The Packet</param>
        /// <returns>The Int64</returns>
        public static Int64 DeserializeInt64( SerializedPacket s ) {
            VerifyPacket( s, PacketTypes.Integer64Id );
            if( s.Data.Count != 8 )
                throw new Exception( "Incorrect Data Length" );
            return (Int64)( (((UInt64)((byte)s.Data[0]) << 0 )) |
                            (((UInt64)((byte)s.Data[1]) << 8 )) |
                            (((UInt64)((byte)s.Data[2]) << 16)) |
                            (((UInt64)((byte)s.Data[3]) << 24)) |
                            (((UInt64)((byte)s.Data[4]) << 32)) |
                            (((UInt64)((byte)s.Data[5]) << 40)) |
                            (((UInt64)((byte)s.Data[6]) << 48)) |
                            (((UInt64)((byte)s.Data[7]) << 56)) );
        }
        #endregion

        #region Long
        /// <summary>
        /// Serialize a long
        /// </summary>
        /// <param name="i">The long</param>
        /// <returns>The Packet</returns>
        public static SerializedPacket SerializeLong( long i ) {
            SerializedPacket packet = new SerializedPacket();
            packet.Type = PacketTypes.LongId;
            packet.Add( (byte)((i >> 0) & 0xFF) );
            packet.Add( (byte)((i >> 8) & 0xFF) );
            packet.Add( (byte)((i >> 16) & 0xFF) );
            packet.Add( (byte)((i >> 24) & 0xFF) );
            packet.Add( (byte)((i >> 32) & 0xFF) );
            packet.Add( (byte)((i >> 40) & 0xFF) );
            packet.Add( (byte)((i >> 48) & 0xFF) );
            packet.Add( (byte)((i >> 56) & 0xFF) );
            return packet;
        }
        /// <summary>
        /// Deserialize a long
        /// </summary>
        /// <param name="s">The Packet</param>
        /// <returns>The long</returns>
        public static long DeserializeLong( SerializedPacket s ) {
            VerifyPacket( s, PacketTypes.LongId );
            if( s.Data.Count != 8 )
                throw new Exception( "Incorrect Data Length" );
            return (long)(  (((UInt64)((byte)s.Data[0]) << 0)) |
                            (((UInt64)((byte)s.Data[1]) << 8 )) |
                            (((UInt64)((byte)s.Data[2]) << 16)) |
                            (((UInt64)((byte)s.Data[3]) << 24)) |
                            (((UInt64)((byte)s.Data[4]) << 32)) |
                            (((UInt64)((byte)s.Data[5]) << 40)) |
                            (((UInt64)((byte)s.Data[6]) << 48)) |
                            (((UInt64)((byte)s.Data[7]) << 56)));
        }
        #endregion

        #region ULong
        /// <summary>
        /// Serialize a ULong
        /// </summary>
        /// <param name="i">The ULong</param>
        /// <returns>The Packet</returns>
        public static SerializedPacket SerializeULong( ulong i ) {
            SerializedPacket packet = new SerializedPacket();
            packet.Type = PacketTypes.ULongId;
            packet.Add( (byte)((i >> 0) & 0xFF) );
            packet.Add( (byte)((i >> 8) & 0xFF) );
            packet.Add( (byte)((i >> 16) & 0xFF) );
            packet.Add( (byte)((i >> 24) & 0xFF) );
            packet.Add( (byte)((i >> 32) & 0xFF) );
            packet.Add( (byte)((i >> 40) & 0xFF) );
            packet.Add( (byte)((i >> 48) & 0xFF) );
            packet.Add( (byte)((i >> 56) & 0xFF) );
            return packet;
        }
        /// <summary>
        /// Deserialize a ULong
        /// </summary>
        /// <param name="s">The Packet</param>
        /// <returns>The ULong</returns>
        public static ulong DeserializeULong( SerializedPacket s ) {
            VerifyPacket( s, PacketTypes.ULongId );
            if( s.Data.Count != 8 )
                throw new Exception( "Incorrect Data Length" );
            return (ulong)( (((UInt64)((byte)s.Data[0]) << 0 )) |
                            (((UInt64)((byte)s.Data[1]) << 8 )) |
                            (((UInt64)((byte)s.Data[2]) << 16)) |
                            (((UInt64)((byte)s.Data[3]) << 24)) |
                            (((UInt64)((byte)s.Data[4]) << 32)) |
                            (((UInt64)((byte)s.Data[5]) << 40)) |
                            (((UInt64)((byte)s.Data[6]) << 48)) |
                            (((UInt64)((byte)s.Data[7]) << 56)));
        }
        #endregion

        #region ByteArray
        /// <summary>
        /// Serialize a ByteArray
        /// </summary>
        /// <param name="i">The array</param>
        /// <returns>The Packet</returns>
        public static SerializedPacket SerializeByteArray( byte[] i ) {
            SerializedPacket packet = new SerializedPacket();
            packet.Type = PacketTypes.ByteArrayId;
            packet.Data.AddRange( i );
            return packet;
        }
        /// <summary>
        /// Deserialize a ByteArray
        /// </summary>
        /// <param name="s">The Packet</param>
        /// <returns>The array</returns>
        public static byte[] DeserializeByteArray( SerializedPacket s ) {
            VerifyPacket( s, PacketTypes.ByteArrayId );
            return (byte[])s.Data.ToArray( typeof(byte) );
        }
        #endregion

        #region IntArray
        /// <summary>
        /// Serialize an IntArray
        /// </summary>
        /// <param name="i">The array</param>
        /// <returns>The Packet</returns>
        public static SerializedPacket SerializeIntArray( int[] i ) {
            SerializedPacket packet = new SerializedPacket();
            packet.Type = PacketTypes.IntArrayId;
            ArrayList list = new ArrayList();
            foreach( int integer in i ) {
                list.Add( (byte)((integer >> 0) & 0xFF) );
                list.Add( (byte)((integer >> 8) & 0xFF) );
                list.Add( (byte)((integer >> 16) & 0xFF) );
                list.Add( (byte)((integer >> 24) & 0xFF) );
            }
            packet.Data.AddRange( (byte[])list.ToArray( typeof(byte) ) );
            return packet;
        }
        /// <summary>
        /// Deserialize a ByteArray
        /// </summary>
        /// <param name="s">The Packet</param>
        /// <returns>The array</returns>
        public static int[] DeserializeIntArray( SerializedPacket s ) {
            VerifyPacket( s, PacketTypes.IntArrayId );
            ArrayList list = new ArrayList();
            for( int i = 0; i < s.Data.Count; i+=4 ) {
                list.Add( (int)( ((byte)s.Data[i + 0] << 0 ) |
                                 ((byte)s.Data[i + 1] << 8 ) |
                                 ((byte)s.Data[i + 2] << 16) |
                                 ((byte)s.Data[i + 3] << 24)) );
            }
            return (int[])list.ToArray( typeof(int) );
        }
        #endregion

        #region String
        /// <summary>
        /// Serialize a String
        /// </summary>
        /// <param name="s">The string</param>
        /// <returns>The packet</returns>
        public static SerializedPacket SerializeString( string s ) {
            if( s == null ) return NullPacket( PacketTypes.StringId );
            SerializedPacket packet = new SerializedPacket();
            packet.Type = PacketTypes.StringId;
            packet.Data.AddRange( System.Text.Encoding.ASCII.GetBytes( s ) );
            return packet;
        }
        /// <summary>
        /// Deserialize a String
        /// </summary>
        /// <param name="s">The packet</param>
        /// <returns>The string</returns>
        public static string DeserializeString( SerializedPacket s ) {
            VerifyPacket( s, PacketTypes.StringId );
            if( s.Type < 0 ) return null;
            return System.Text.Encoding.ASCII.GetString( (byte[])s.Data.ToArray( typeof(byte) ) );
        }
        #endregion

        #region IPEndPoint
        /// <summary>
        /// Serialize an IPEndPoint
        /// </summary>
        /// <param name="ep">The end point</param>
        /// <returns>The packet</returns>
        public static SerializedPacket SerializeIPEndPoint( System.Net.IPEndPoint ep ) {
            SerializedPacket packet = new SerializedPacket();
            packet.Type = PacketTypes.IPEndPointId;
            // Pack the Data
            packet.Add( SerializeInt( (int)ep.AddressFamily ) );
            packet.Add( SerializeInt( ep.Port ) );
//            packet.Add( SerializeLong( ep.Address.ScopeId ) );
            packet.Add( SerializeByteArray( ep.Address.GetAddressBytes() ) );
            return packet;
        }
        /// <summary>
        /// Deserialize an IPEndPoint
        /// </summary>
        /// <param name="s">The packet</param>
        /// <returns>The end point</returns>
        public static System.Net.IPEndPoint DeserializeIPEndPoint( SerializedPacket s ) {
            VerifyPacket( s, PacketTypes.IPEndPointId );
            if( s.Data.Count != 3 )
                throw new Exception( "Incorrect Data Length" );
            System.Net.Sockets.AddressFamily family = (System.Net.Sockets.AddressFamily) DeserializeInt( (SerializedPacket)s.Data[0] );
            int port = DeserializeInt( (SerializedPacket)s.Data[1] );
//            long scopeId = DeserializeLong( (SerializedPacket)s.Data[2] );
            byte[] addr = DeserializeByteArray( (SerializedPacket)s.Data[2] );
//            return new System.Net.IPEndPoint( new System.Net.IPAddress( addr, scopeId ), port );
            return new System.Net.IPEndPoint( new System.Net.IPAddress( addr ), port );
        }
        #endregion

        #region Color
        /// <summary>
        /// Serialize a Color struct
        /// </summary>
        /// <param name="c">The color</param>
        /// <returns>The packet</returns>
        public static SerializedPacket SerializeColor( System.Drawing.Color c ) {
            if( c.IsEmpty ) return NullPacket( PacketTypes.ColorId );
            SerializedPacket packet = new SerializedPacket();
            packet.Type = PacketTypes.ColorId;
            packet.Add( c.A );
            packet.Add( c.R );
            packet.Add( c.G );
            packet.Add( c.B );
            return packet;
        }
        /// <summary>
        /// Deserialize a Color struct
        /// </summary>
        /// <param name="s">The packet</param>
        /// <returns>The color</returns>
        public static System.Drawing.Color DeserializeColor( SerializedPacket s ) {
            VerifyPacket( s, PacketTypes.ColorId );
            if( s.Type < 0 ) return System.Drawing.Color.Empty;
            if( s.Data.Count != 4 )
                throw new Exception( "Incorrect Data Length" );
            return System.Drawing.Color.FromArgb( (byte)s.Data[0], (byte)s.Data[1], (byte)s.Data[2], (byte)s.Data[3] );
        }
        #endregion

        #region Rectangle
        /// <summary>
        /// Serialize a Rectangle
        /// </summary>
        /// <param name="r">The rectangle</param>
        /// <returns>The packet</returns>
        public static SerializedPacket SerializeRectangle( System.Drawing.Rectangle r ) {
            SerializedPacket packet = new SerializedPacket();
            packet.Type = PacketTypes.RectangleId;
            packet.Add( SerializeInt( r.X ) );
            packet.Add( SerializeInt( r.Y ) );
            packet.Add( SerializeInt( r.Width ) );
            packet.Add( SerializeInt( r.Height ) );
            return packet;
        }
        /// <summary>
        /// Deserialize a Rectangle
        /// </summary>
        /// <param name="s">The packet</param>
        /// <returns>The rectangle</returns>
        public static System.Drawing.Rectangle DeserializeRectangle( SerializedPacket s ) {
            VerifyPacket( s, PacketTypes.RectangleId );
            if( s.Data.Count != 4 )
                throw new Exception( "Incorrect Data Length" );
            return new System.Drawing.Rectangle( DeserializeInt( (SerializedPacket)s.Data[0] ),
                                                 DeserializeInt( (SerializedPacket)s.Data[1] ),
                                                 DeserializeInt( (SerializedPacket)s.Data[2] ),
                                                 DeserializeInt( (SerializedPacket)s.Data[3] ) );
        }
        #endregion

        #region GUID
        /// <summary>
        /// Serialize a GUID
        /// </summary>
        /// <param name="g">The guid</param>
        /// <returns>The packet</returns>
        public static SerializedPacket SerializeGuid( Guid g ) {
            SerializedPacket packet = new SerializedPacket();
            packet.Type = PacketTypes.GuidId;
            packet.Add( g.ToByteArray() );
            return packet;
        }
        /// <summary>
        /// Deserialize a GUID
        /// </summary>
        /// <param name="s">The packet</param>
        /// <returns>The guid</returns>
        public static Guid DeserializeGuid( SerializedPacket s ) {
            VerifyPacket( s, PacketTypes.GuidId );
            if( s.Data.Count != 16 )
                throw new Exception( "Incorrect Data Length" );
            return new Guid( (byte[])s.Data.ToArray( typeof(byte) ) );
        }
        #endregion

        #region GUID
        /// <summary>
        /// Serialize a GUID
        /// </summary>
        /// <param name="g">The guid</param>
        /// <returns>The packet</returns>
        public static SerializedPacket SerializeMD5(Guid g)
        {
            SerializedPacket packet = new SerializedPacket();
            packet.Type = PacketTypes.ByteArrayClassId;
            packet.Add(g.ToByteArray());
            return packet;
        }
        /// <summary>
        /// Deserialize a GUID
        /// </summary>
        /// <param name="s">The packet</param>
        /// <returns>The guid</returns>
        public static Guid DeserializeMD5(SerializedPacket s)
        {
            VerifyPacket(s, PacketTypes.ByteArrayClassId);
            if (s.Data.Count != 16)
                throw new Exception("Incorrect Data Length");
            return new Guid((byte[])s.Data.ToArray(typeof(byte)));
        }
        #endregion

        #endregion
    }

    /// <summary>
    /// An interface for all objects that support generic serialization and aren't value types
    /// </summary>
    public interface IGenericSerializable {
        SerializedPacket Serialize();
        int GetClassId();
    }
}
