// $Id: ImageResourcesModel.cs 795 2005-09-26 23:42:00Z shoat $
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Collections;
using System.Threading;
using System.Runtime.Serialization;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.IO;
using UW.ClassroomPresenter.Network;

namespace UW.ClassroomPresenter.Model.Presentation {
    /// <summary>
    /// Represents a pool of images that belong to a set of slide decks.
    /// This is currently used to keep references to all of the images in all
    /// the decks in order to allow multiple decks to reference the same image 
    /// without needing two copies of the images, in a sense this makes every image
    /// a shared global object.
    /// 
    /// This helps with student submissions because they can be placed in a new deck
    /// yet still reference the same image objects from the source deck.
    /// 
    /// FIXME: Ensure that student submission on matched decks works??
    /// </summary>
    public class ImageResourcesModel {
        #region Public Members
        
        /// <summary>
        /// The collection of keys belonging to this hashtable
        /// </summary>
        public ICollection Keys {
            get {
                return this.ht.Keys;
            }
        }

        #endregion

        #region Private Members

        /// <summary>
        /// Internal hashtable used to store the (key,image) pairs
        /// </summary>
/*        [NonSerialized]*/
        private Hashtable ht = new Hashtable();

        #endregion

        #region Construction

        /// <summary>
        /// Default constructor
        /// </summary>
        public ImageResourcesModel() {
            this.ht = new Hashtable();
        }

        #endregion

        #region ImageWeakReference class

        /// <summary>
        /// Wrapper for the WeakReference to ensure that references to the same image
        /// object have the same hash code
        /// </summary>
        public class ImageWeakReference : WeakReference {
            /// <summary>
            /// Cache of the hashcode for performance reasons
            /// </summary>
            private readonly int m_HashCode;

            /// <summary>
            /// Constructor
            /// </summary>
            /// <param name="o">Image to create a weak reference to</param>
            public ImageWeakReference(object o) : base(o) {
                this.m_HashCode = o.GetHashCode();
            }

            /// <summary>
            /// Compares to image weak references to see if the images they reference are
            /// equal.
            /// FIXME: Does this compare the weak refernce objects or the targets??
            /// </summary>
            /// <param name="obj">The object to compare against</param>
            /// <returns>True if the objects are the same, false otherwise</returns>
            public override bool Equals(object obj) {
                return (this.IsAlive && object.ReferenceEquals(this.Target, obj))
                    || object.ReferenceEquals(this, obj);
            }

            /// <summary>
            /// Returns the hashcode of the image referenced by this weak reference
            /// </summary>
            /// <returns></returns>
            public override int GetHashCode() {
                Debug.Assert(!this.IsAlive || this.m_HashCode == this.Target.GetHashCode(), "The referent's HashCode has changed.");
                return this.m_HashCode;
            }
        }    
    
        #endregion

        #region Methods

        /// <summary>
        /// Add a value to the hashtable as a weak reference.
        /// NOTE: The image will be collected if there is not a strong 
        /// reference somewhere else
        /// </summary>
        /// <param name="key">The MD5 of the image</param>
        /// <param name="image">The image object itself</param>
        public void Add(ByteArray key, Image image) {
            this.ht.Add(key, new ImageWeakReference( image ) );
        }

        /// <summary>
        /// Indexer for the hash table
        /// </summary>
        public Image this[ByteArray key] {
            get { return (Image)( ((ImageWeakReference)this.ht[key]).Target ); }
        }

        /// <summary>
        /// Remove a (MD5,image) pair from the hashtable
        /// </summary>
        /// <param name="key">The MD5 of the image to remove</param>
        public void Remove(ByteArray key) {
            this.ht.Remove(key);
        }

        /// <summary>
        /// Check if the table contains an image with the given MD5
        /// </summary>
        /// <param name="key">The MD5 to look for</param>
        /// <returns>True if the key exists, false otherwise</returns>
        public bool Contains(ByteArray key) {
            return this.ht.Contains(key);
        }

        /// <summary>
        /// Check if the table contains an image with the given MD5
        /// </summary>
        /// <param name="key">The MD5 to look for</param>
        /// <returns>True if the key exists, false otherwise</returns>
        public bool ContainsKey(ByteArray key) {
            return this.ht.ContainsKey(key);
        }

        /// <summary>
        /// Check if the table contains the given image
        /// </summary>
        /// <param name="key">The image to look for</param>
        /// <returns>True if the value exists, false otherwise</returns>
        public bool ContainsValue(Image image) {
            return this.ht.ContainsValue( new ImageWeakReference( image ) );
        }

        #endregion
    }

    /// <summary>
    /// A 16 byte wide value used to represent MD5 hash values
    /// </summary>
    [Serializable]
    public struct ByteArray {
        #region Private Members

        /// <summary>
        /// Internal representation of the 16 byte values
        /// </summary>
        private readonly ulong l1, l2;

        #endregion

        #region Construction

        public ByteArray(Guid g) {
            byte[] bytes = g.ToByteArray();
            this.l1 = BitConverter.ToUInt64(bytes, 0);
            this.l2 = BitConverter.ToUInt64(bytes, 8);
        }

        /// <summary>
        /// Constructor taking two long values
        /// </summary>
        /// <param name="l1">The first 8 bytes</param>
        /// <param name="l2">The second 8 bytes</param>
        private ByteArray(ulong l1, ulong l2) 
        {
            this.l1 = l1; this.l2 = l2;
        }

        /// <summary>
        /// Copy constructor taking an array of bytes
        /// </summary>
        /// <param name="input">The array of bytes</param>
        /// <returns>The ByteArray object representing the input</returns>
        public static explicit operator ByteArray(byte[] input) {
            if (input.Length != 16)
                throw new InvalidCastException("The array length must be exactly 16.");

            ulong l1, l2;
            l1 =  ((((ulong) input[0])  & 0xFF) << 56)
                | ((((ulong) input[1])  & 0xFF) << 48)
                | ((((ulong) input[2])  & 0xFF) << 40)
                | ((((ulong) input[3])  & 0xFF) << 32)
                | ((((ulong) input[4])  & 0xFF) << 24)
                | ((((ulong) input[5])  & 0xFF) << 16)
                | ((((ulong) input[6])  & 0xFF) <<  8)
                | ((((ulong) input[7])  & 0xFF) <<  0);

            l2 =  ((((ulong) input[8])  & 0xFF) << 56)
                | ((((ulong) input[9])  & 0xFF) << 48)
                | ((((ulong) input[10]) & 0xFF) << 40)
                | ((((ulong) input[11]) & 0xFF) << 32)
                | ((((ulong) input[12]) & 0xFF) << 24)
                | ((((ulong) input[13]) & 0xFF) << 16)
                | ((((ulong) input[14]) & 0xFF) <<  8)
                | ((((ulong) input[15]) & 0xFF) <<  0);

            return new ByteArray(l1, l2);
        }

        /// <summary>
        /// Copy Consturctor taking a ByteArray object
        /// </summary>
        /// <param name="input">The ByteArray object</param>
        /// <returns>The array of bytes representing the input</returns>
        public static explicit operator byte[](ByteArray input) {
            byte[] output = new byte[16];

            output[0]  = (byte) (input.l1 >> 56);
            output[1]  = (byte) (input.l1 >> 48);
            output[2]  = (byte) (input.l1 >> 40);
            output[3]  = (byte) (input.l1 >> 32);
            output[4]  = (byte) (input.l1 >> 24);
            output[5]  = (byte) (input.l1 >> 16);
            output[6]  = (byte) (input.l1 >> 8);
            output[7]  = (byte) (input.l1 >> 0);

            output[8]  = (byte) (input.l2 >> 56);
            output[9]  = (byte) (input.l2 >> 48);
            output[10] = (byte) (input.l2 >> 40);
            output[11] = (byte) (input.l2 >> 32);
            output[12] = (byte) (input.l2 >> 24);
            output[13] = (byte) (input.l2 >> 16);
            output[14] = (byte) (input.l2 >> 8);
            output[15] = (byte) (input.l2 >> 0);

            return output;
        }

        #endregion

        #region Methods

        /// <summary>
        /// Returns whether two byte arrays are equal
        /// </summary>
        /// <param name="b">The ByteArray to compare to</param>
        /// <returns>True if the values are all equal, false otherwise</returns>
        public bool Equals(ByteArray b) {
            return b.l1 == this.l1
                && b.l2 == this.l2;
        }

        /// <summary>
        /// Returns whether two byte arrays are equal
        /// </summary>
        /// <param name="obj">The ByteArray to compare to</param>
        /// <returns>True if the values are all equal, false otherwise</returns>
        public override bool Equals(object obj) {
            if (obj is ByteArray) {
                return this.Equals((ByteArray) obj);
            } else {
                return false;
            }
        }

        /// <summary>
        /// Get a hash value that is unique for this object
        /// </summary>
        /// <returns>The hash code for the object</returns>
        public override int GetHashCode() {
            return unchecked((l1 * l2).GetHashCode());
        }

        /// <summary>
        /// Equality operator
        /// </summary>
        /// <param name="one">The first ByteArray</param>
        /// <param name="two">The second ByteArray</param>
        /// <returns>True if the arrays are equal, false otherwise</returns>
        public static bool operator == (ByteArray one, ByteArray two) {
            return (one.l1 == two.l1) & (one.l2 == two.l2);
        }

        /// <summary>
        /// Inequality operator
        /// </summary>
        /// <param name="one">The first ByteArray</param>
        /// <param name="two">The second ByteArray</param>
        /// <returns>True if the arrays are not equal, false otherwise</returns>
        public static bool operator != (ByteArray one, ByteArray two) {
            return (one.l1 != two.l1) | (one.l2 != two.l2);
        }

        /// <summary>
        /// Converts the MD5 hash to a string
        /// </summary>
        /// <returns>Friendly string representing the array</returns>
        public override string ToString() {
            return String.Format("{0,16:x}{1,16:x}", this.l1, this.l2);
        }

        /// <summary>
        /// Converts the MD5 hash to a Guid
        /// </summary>
        /// <returns>The Guid containing the MD5 hash</returns>
        public Guid ToGuid()
        {
            byte[] first = BitConverter.GetBytes( this.l1 );
            byte[] second = BitConverter.GetBytes( this.l2 );
            byte[] sum = new byte[first.Length+second.Length];

            first.CopyTo( sum, 0 );
            second.CopyTo( sum, first.Length );
            
            return new Guid( sum );
        }

        #endregion
    }
}
