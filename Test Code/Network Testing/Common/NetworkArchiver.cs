// $Id: NetworkArchiver.cs 774 2005-09-21 20:22:33Z pediddle $

using System;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;


namespace UW.ClassroomPresenter.Test.Network.Common {
    /// <summary>
    /// NetworkArchiver stores a stream of events in a file for later review
    /// </summary>
    public class NetworkArchiver {
        // The file containing the ink archive
        private FileStream archive = null;
        private long PrevEventOffset = -1;

        // Access Mode
        public enum ArchiveMode {
            ReadingMode,
            WritingMode,
            None
        };
        private ArchiveMode mode = ArchiveMode.None;
        public ArchiveMode Mode {
            get { return mode; }
            set { mode = value; }
        }

        // Constructor
        public NetworkArchiver() {
        }

        ~NetworkArchiver() {
            CloseArchive();
        }

        // ------------------ Reading/Writing ----------------------
        // Closes the currently opened archive files
        public void CloseArchive() {
            if( archive != null )
                archive.Close();
            archive = null;
            mode = ArchiveMode.None;
        }

        // ------------------ Reading ----------------------
        // Open an existing archive for reading
        public bool OpenArchive( string fullpath ) {
            archive = new FileStream( fullpath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite );
            mode = ArchiveMode.ReadingMode;
            return true;
        }

        // Get the Next event from the archive and advance the File pointer
        public NetworkEvent GetNextEvent() {
            NetworkEvent e = null;

            if( mode == ArchiveMode.ReadingMode ) {
                // Read the previous offset and current length
                long tempPrevOffset = (long)ReadLong( archive );
                long tempLength = (long)ReadLong( archive );
                
                // Read the object from the file
                byte[] byteArray = new byte[tempLength];
                archive.Read( byteArray, 0, (int)tempLength );

                // Deserialize the ArchiveEvent
                try {
                    BinaryFormatter binaryFormatter = new BinaryFormatter();
                    MemoryStream memoryStream = new MemoryStream( byteArray, 0, (int)tempLength );
                    e = (NetworkEvent)binaryFormatter.Deserialize(memoryStream);
                } 
                catch (System.Runtime.Serialization.SerializationException) {
                    return null;
                }
            }

            return e;
        }

        // Get the Prev event from the archive and place the the File pointer at the beginning of that event
        public NetworkEvent GetPrevEvent() {
            PrevEvent();
            return PeekNextEvent();
        }

        // Advances the File pointer to the beginning of the previous event
        public void PrevEvent() {
            if( mode == ArchiveMode.ReadingMode ) {
                // Read the previous event from the file
                long tempPrevOffset = (long)ReadLong( archive );
                if( tempPrevOffset == -1 ) {
                    archive.Seek( -8, SeekOrigin.Current );
                    return;
                }

                // Go to the previous event
                archive.Seek( tempPrevOffset, SeekOrigin.Begin );
            }
        }

        // Advances the File pointer to the beginning of the next event
        public void NextEvent() {
            if( mode == ArchiveMode.ReadingMode ) {
                // Read the previous event from the file
                long tempPrevOffset = (long)ReadLong( archive );
                long tempLength = (long)ReadLong( archive );

                // Go to the next event
                archive.Seek( tempLength, SeekOrigin.Current );
            }
        }

        // Gets the next event but does not advance the File pointer
        public NetworkEvent PeekNextEvent() {
            NetworkEvent e = GetNextEvent();
            PrevEvent();
            return e;
        }

        // Returns whether the archive has more events in it
        public bool HasMoreEvents() {
            return ( archive.Position < archive.Length );
        }

        // ------------------ Writing ----------------------
        // Create a new archive for writing
        public void NewArchive( string fullpath ) {
            archive = new FileStream( fullpath, FileMode.Create, FileAccess.Write, FileShare.None );
            mode = ArchiveMode.WritingMode;
        }

        // Add an event to the currently opened archive
        public void AppendEvent( NetworkEvent e ) {
            if( mode == ArchiveMode.WritingMode ) {
                // Save the current file offset
                long tempPrevOffset = this.PrevEventOffset;
                this.PrevEventOffset = archive.Position;

                // Serialize the Object
                MemoryStream memoryStream = new MemoryStream(); 
                BinaryFormatter binaryFormatter = new BinaryFormatter();
                binaryFormatter.Serialize(memoryStream, e);

                // Write to a byte array
                byte[] byteArray = new byte[memoryStream.Length];
                memoryStream.Position = 0;
                memoryStream.Read(byteArray, 0, (int) memoryStream.Length);

                // Write to the file
                WriteLong( archive, (ulong)tempPrevOffset );                // Previous Event File Offset
                WriteLong( archive, (ulong)memoryStream.Length );           // Current Event Length
                archive.Write( byteArray, 0, (int)memoryStream.Length );    // Current Event Data
            }
        }

        // Writes a Long to the file
        private void WriteLong( FileStream s, ulong i ) {
            s.WriteByte( (byte)(i >> 0) );
            s.WriteByte( (byte)(i >> 8) );
            s.WriteByte( (byte)(i >> 16) );
            s.WriteByte( (byte)(i >> 24) );
            s.WriteByte( (byte)(i >> 32) );
            s.WriteByte( (byte)(i >> 40) );
            s.WriteByte( (byte)(i >> 48) );
            s.WriteByte( (byte)(i >> 56) );
        }

        // Reads a long from the file
        private ulong ReadLong( FileStream s ) {
            ulong result = 0;
            result += (ulong)s.ReadByte();
            result += (ulong)s.ReadByte() << 8;
            result += (ulong)s.ReadByte() << 16;
            result += (ulong)s.ReadByte() << 24;
            result += (ulong)s.ReadByte() << 32;
            result += (ulong)s.ReadByte() << 40;
            result += (ulong)s.ReadByte() << 48;
            result += (ulong)s.ReadByte() << 56;
            return result;
        }

    }
}
// -- END ADDED
