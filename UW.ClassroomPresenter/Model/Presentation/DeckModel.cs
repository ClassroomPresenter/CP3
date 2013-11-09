// $Id: DeckModel.cs 2233 2013-09-27 22:17:28Z fred $

using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Collections;
using System.Threading;
using System.Runtime.Serialization;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.IO;

using UW.ClassroomPresenter.Model.Background;
using UW.ClassroomPresenter.Network.Groups;

namespace UW.ClassroomPresenter.Model.Presentation {
    /// <summary>
    /// The DeckModel class encapsulates the information contained within a deck. This
    /// includes the slides and images in the deck (including all the slide layers); 
    /// however, this does not include all of the links between the slides, only a 
    /// bag of the slides themselves.
    /// </summary>
    [Serializable]
    public class DeckModel : PropertyPublisher {
        #region Public Members

        /// <summary>
        /// The table of contents for this deck.
        /// </summary>
        private readonly TableOfContentsModel m_TableOfContents;
        public TableOfContentsModel TableOfContents {
            get { return this.m_TableOfContents; }
        }

        /// <summary>
        /// A friendly name for this deck, this value is not guaranteed to be unique 
        /// and should only be used for display to the user
        /// </summary>
        private string m_HumanName; // String
        [Published] public string HumanName {
            get { return this.GetPublishedProperty("HumanName", ref this.m_HumanName); }
            set { this.SetPublishedProperty("HumanName", ref this.m_HumanName, value); }
        }

        /// <summary>
        /// Whether to prompt to save decks before closing. - 
        /// depending on whether they've manipulated decks
        /// </summary>
        /// 
        private bool m_Dirty;
        [Published]
        public bool Dirty {
            get { return this.GetPublishedProperty("Dirty", ref this.m_Dirty); }
            set { this.SetPublishedProperty("Dirty", ref this.m_Dirty, value); }
        }


        /// <summary>
        /// The background color of the deck to display where there is no content.
        /// </summary>
        private Color m_DeckBackgroundColor; //Color
        [Published] public Color DeckBackgroundColor {
            get { return this.GetPublishedProperty("DeckBackgroundColor", ref this.m_DeckBackgroundColor); }
            set { this.SetPublishedProperty("DeckBackgroundColor", ref this.m_DeckBackgroundColor, value); }
        }

        private BackgroundTemplate m_BackgroundTemplate;
        [Published] public BackgroundTemplate DeckBackgroundTemplate {
            get { return this.GetPublishedProperty("DeckBackgroundTemplate", ref this.m_BackgroundTemplate); }
            set { this.SetPublishedProperty("DeckBackgroundTemplate", ref this.m_BackgroundTemplate, value); }
        }


        /// <summary>
        /// A globally unique ID for this deck, this should be globally unique
        /// NOTE: Is this generated each session or saved with the file?
        /// </summary>
        private readonly Guid m_Id;
        public Guid Id {
            get { return this.m_Id; }
        }

        /// <summary>
        /// This is the disposition of the deck, since decks can be of different types.
        /// </summary>
        private DeckDisposition m_Disposition;
        public DeckDisposition Disposition {
            get { return this.m_Disposition; }
            set { this.m_Disposition = value; }
        }


        public bool current_subs = false;
        public bool current_poll = false;

        #region Tab background color
        ///Author: Julia Schwarz
        /// <summary>
        /// This is basically a color-based encoding for the deck disposition.
        /// depending on what type of deck this is, we will assign it a different color.
        /// primarily used in setting the background colors of deck tabs.
        /// </summary>
        /// <returns>Color that the particular disposition represents</returns>
        public Color GetDispositionColor() {
            if (this.m_Disposition == DeckDisposition.Empty) {
                return Color.LightCyan;
            } 
            else if ((this.m_Disposition & DeckDisposition.StudentSubmission) != 0) {
                return Color.LightSteelBlue;
            } 
            else if ((this.m_Disposition & DeckDisposition.Remote) != 0) {
                return Color.PaleTurquoise;
            } 
            else if ((this.m_Disposition & DeckDisposition.Whiteboard) != 0) {
                return Color.White;
            }
            else if ((this.m_Disposition & DeckDisposition.PublicSubmission) != 0) {
                return Color.LightGreen;
            } 
            else {
                return Color.White;
            }
        }
        #endregion


        [NonSerialized]
        public Group Group;

        /// <summary>
        /// The filename from which this deck was loaded, this is only a property of a 
        /// deck when it loaded in an application.
        /// </summary>
        [NonSerialized]
        public string Filename;

      
        /// <summary>
        /// The set of slides located in this slide deck
        /// </summary>
        public ICollection Slides {
            get {
                Synchronizer.AssertLockIsHeld(this.SyncRoot);
                return this.m_Slides.Values;
            }
        }

        /// <summary>
        /// The set of content keys for this slide deck (namely image MD5 hashes)
        /// </summary>
        public ICollection SlideContentKeys {
            get {
                Synchronizer.AssertLockIsHeld(this.SyncRoot);
                return this.m_SlideContent.Keys;
            }
        }

        private bool m_IsXpsDeck;
        public bool IsXpsDeck{
            set { this.m_IsXpsDeck = value; }
            get { return this.m_IsXpsDeck; }
        }
        #endregion

        #region Private Members
        
        /// <summary>
        /// The (ID,slide) pairs contained in this deck
        /// </summary>
        private Hashtable m_Slides;
        /// <summary>
        /// A hashtable that contains (key,image) containing the slides and 
        /// images used in this deck. All entries in this table should also be in the
        /// ImagePool object (see below).
        /// </summary>
        private ImageHashtable m_SlideContent;
        /// <summary>
        /// A static object that allows us to have a single global pool of images for 
        /// our slide decks to share
        /// </summary>
        [NonSerialized]
        internal static readonly ImageResourcesModel ImagePool = new ImageResourcesModel();
        /// <summary>
        /// Key is content MD5 hash, value is SlideModel or null.
        /// Facilitate message tagging by tracking the slide where a content hash is referenced.
        /// We only care about the cases where the content is referenced by exactly one slide.  Otherwise
        /// the value should be null.
        /// </summary>
        [NonSerialized]
        private Hashtable m_ContentToSlideLookup;

        #endregion

        #region Construction
        
        /// <summary>
        /// Constructor for this object
        /// </summary>
        /// <param name="id">The globally unique identifier for this deck</param>
        /// <param name="disposition">The disposition of this deck</param>
        public DeckModel(Guid id, DeckDisposition disposition) : this(id, disposition, null, null, null, null, null) {}
        public DeckModel(Guid id, DeckDisposition disposition, string humanName) : this(id, disposition, humanName, null, null, null, null) { }

        /// <summary>
        /// Constructor for this object
        /// </summary>
        /// <param name="id">The globally unique identifier for this deck</param>
        /// <param name="disposition"></param>
        /// <param name="humanName"></param>
        public DeckModel(Guid id, DeckDisposition disposition, string humanName, TableOfContentsModel toc, Hashtable slides, ImageHashtable sc, Hashtable ctsl) {
            this.m_Id = id;
            this.m_Disposition = disposition;
            this.m_HumanName = humanName;
            this.m_DeckBackgroundColor = Color.White;
            this.Filename = "";
            this.Group = Group.AllParticipant;
            if (this.Disposition == DeckDisposition.StudentSubmission || this.Disposition == DeckDisposition.QuickPoll) {
                this.m_Dirty = true;
            }
            else {
                this.m_Dirty = false;
            }
            if (toc == null) {
                this.m_TableOfContents = new TableOfContentsModel();
            }
            else {
                this.m_TableOfContents = toc;
            }
            if (slides == null) {
                this.m_Slides = new Hashtable();
            }
            else {
                this.m_Slides = slides;
            }
            if (sc == null) {
                this.m_SlideContent = new ImageHashtable();
            }
            else {
                this.m_SlideContent = sc;
            }
            if (ctsl != null) {
                this.m_ContentToSlideLookup = ctsl;
            }
        }
        
        #endregion

        #region Events and Delegates

        /// <summary>
        /// This event is triggered when a slide is added
        /// </summary>
        [NonSerialized] private PropertyEventHandler m_SlideAddedDelegate;
        public event PropertyEventHandler SlideAdded {
            add { using(Synchronizer.Lock(this)) { this.m_SlideAddedDelegate = (PropertyEventHandler) MulticastDelegate.Combine(this.m_SlideAddedDelegate, value); } }
            remove { using(Synchronizer.Lock(this)) { this.m_SlideAddedDelegate = (PropertyEventHandler) MulticastDelegate.Remove(this.m_SlideAddedDelegate, value); } }
        }

        /// <summary>
        /// This event is triggered when a slide is removed
        /// </summary>
        [NonSerialized] private PropertyEventHandler m_SlideRemovedDelegate;
        public event PropertyEventHandler SlideRemoved {
            add { using(Synchronizer.Lock(this)) { this.m_SlideRemovedDelegate = (PropertyEventHandler) MulticastDelegate.Combine(this.m_SlideRemovedDelegate, value); } }
            remove { using(Synchronizer.Lock(this)) { this.m_SlideRemovedDelegate = (PropertyEventHandler) MulticastDelegate.Remove(this.m_SlideRemovedDelegate, value); } }
        }

        /// <summary>
        /// This event is triggered when slide content (namely an image) is added
        /// </summary>
        [NonSerialized] private PropertyEventHandler m_SlideContentAddedDelegate;
        public event PropertyEventHandler SlideContentAdded {
            add { using(Synchronizer.Lock(this)) { this.m_SlideContentAddedDelegate = (PropertyEventHandler) MulticastDelegate.Combine(this.m_SlideContentAddedDelegate, value); } }
            remove { using(Synchronizer.Lock(this)) { this.m_SlideContentAddedDelegate = (PropertyEventHandler) MulticastDelegate.Remove(this.m_SlideContentAddedDelegate, value); } }
        }

        // There is no event for SlideContentDeleted.  Since content is only removed
        // when slides are removed, the SlideRemoved event is all that's necessary.

        #endregion

        #region Slide/Content Editing

        /// <summary>
        /// Retrieves a slide with a specific ID
        /// </summary>
        /// <param name="slideHash">The ID of the slide to get</param>
        /// <returns>A slide model object for the slide with the given ID</returns>
        public SlideModel GetSlide(LocalId slideHash) {
            Synchronizer.AssertLockIsHeld(this.SyncRoot);

            if (this.m_Slides.Contains(slideHash)) {
                return (SlideModel)this.m_Slides[slideHash];
            } else {
                //TODO: Do we want to return null here?
                return null;
            }
        }

        private void BuildContentToSlideLookup() {       
            m_ContentToSlideLookup = new Hashtable();
            foreach (SlideModel slide in this.m_Slides.Values) {
                using (Synchronizer.Lock(slide.SyncRoot)) {
                    foreach (SheetModel sm in slide.ContentSheets) {
                        if (!(sm is ImageSheetModel)) continue;
                        ImageSheetModel ism = ((ImageSheetModel)sm);
                        if (m_ContentToSlideLookup.ContainsKey(ism.MD5)) {
                            m_ContentToSlideLookup[ism.MD5] = null;
                        }
                        else {
                            m_ContentToSlideLookup.Add(ism.MD5, slide);
                        }
                    }
                }
            }      
        }

        /// <summary>
        /// If there is a single unique slide that references this hash, return it.
        /// Return null if there are none or multiple.
        /// </summary>
        /// <param name="contentHash"></param>
        /// <returns></returns>
        public SlideModel GetSlideFromContent(ByteArray contentHash) {

            //If the DeckModel came from a CP3 file, we will need to build the lookup table for the deck on the first access.
            if (m_ContentToSlideLookup == null) {
                BuildContentToSlideLookup();
            }

            if (m_ContentToSlideLookup.ContainsKey(contentHash))
                return (SlideModel)m_ContentToSlideLookup[contentHash];
            return null;

        }

        /// <summary>
        /// Checks for the existance of a slide in the deck
        /// </summary>
        /// <param name="slide">The slide to check for</param>
        /// <returns>True if the given slide exists, false otherwise</returns>
        public bool ContainsSlide(SlideModel slide) {
            Synchronizer.AssertLockIsHeld(this.SyncRoot);
            return this.m_Slides.ContainsKey(slide.LocalId);
        }

        /// <summary>
        /// Adds a slide to the deck. 
        /// NOTE: slides have no natural ordering
        /// </summary>
        /// <param name="slide">The slide to insert</param>
        public void InsertSlide(SlideModel slide) {
            Synchronizer.AssertLockIsHeld(this.SyncRoot);
            if (m_ContentToSlideLookup == null) {
                BuildContentToSlideLookup();
            }
            using(Synchronizer.Lock(this)) { // Prevent events from being registered or unregistered.
                if( this.m_Slides[slide.LocalId] == null ||
                    ((SlideModel)this.m_Slides[slide.LocalId]).Id != slide.Id ) {
                    this.m_Slides[slide.LocalId] = slide;

                    using (Synchronizer.Lock(slide.SyncRoot)) {
                        foreach (SheetModel sm in slide.ContentSheets) {
                            if (!(sm is ImageSheetModel)) continue;
                            ImageSheetModel ism = ((ImageSheetModel)sm);

                            Image image = ism.Image;
                            if (image == null)
                                using (Synchronizer.Lock(ism.Deck.SyncRoot))
                                    image = ism.Deck.GetSlideContent(ism.MD5);
                            if (image != null)
                                this.AddSlideContent(ism.MD5, image);

                            if (!m_ContentToSlideLookup.ContainsKey(ism.MD5)) {
                                m_ContentToSlideLookup.Add(ism.MD5, slide);
                            }
                        }
                    }
                    
                    if(this.m_SlideAddedDelegate != null) {
                        this.m_SlideAddedDelegate(this, new PropertyCollectionEventArgs("Slides", -1, null, slide));
                    }
                }
            }
        }
        /// <summary>
        /// Inserts a deck into the current deck by adding all the slides in the
        /// deck. Does so by calling insertSlide method.
        /// </summary>
        /// <param name="deck">the original deck to be inserted into this deck</param>
        public void InsertDeck(DeckModel deck) {
            using (Synchronizer.Lock(deck.SyncRoot)) {
                using (Synchronizer.Lock(this.SyncRoot)) {
                    foreach (SlideModel s in deck.Slides) {
                        this.InsertSlide(s);
                    }
                }
            }
        }
        /// <summary>
        /// Adds slide content (namely an image) to the deck
        /// </summary>
        /// <param name="hash">The MD5 hash of the image</param>
        /// <param name="image">The image to add</param>
        public void AddSlideContent(ByteArray hash, Image image) {
            Synchronizer.AssertLockIsHeld(this.SyncRoot);
            using(Synchronizer.Lock(this)) { // Prevent events from being registered or unregistered.
                if (!this.m_SlideContent.ContainsKey(hash)) {
                    this.m_SlideContent.Add(hash, image);

                    if (this.m_SlideContentAddedDelegate != null)
                        this.m_SlideContentAddedDelegate(this, new PropertyCollectionEventArgs("SlideContent", -1, null, hash));
                }
            }
        }

        /// <summary>
        /// Get the slide content (namely the image) with the given MD5 hash
        /// </summary>
        /// <param name="hash">The MD5 corresponding to the desired image</param>
        /// <returns></returns>
        public Image GetSlideContent(ByteArray hash) {
            Synchronizer.AssertLockIsHeld(this.SyncRoot);
            return ((Image) this.m_SlideContent[hash]);
        }

        /// <summary>
        /// Removes a slide and its content (if used nowhere else) from the deck
        /// </summary>
        /// <param name="slide">The slide to remove</param>
        public void DeleteSlide(SlideModel slide) {
            Synchronizer.AssertLockIsHeld(this.SyncRoot);

            if (!this.m_Slides.Contains(slide.LocalId)) {
                throw new ArgumentException("The specified slide is not a member of this deck.", "slide");
            }

            else {
                //Remove the slide from the deck
                this.m_Slides.Remove(slide.LocalId);

                //Lock the slide
                using(Synchronizer.Lock(slide.SyncRoot)) {
                    //Get all the ImageSheets
                    // FIXME: Also get ImageSheetModels from the slide's AnnotationSheets
                    foreach(SheetModel sm in slide.ContentSheets) {
                        if(!(sm is ImageSheetModel)) continue;
                        ImageSheetModel ism = ((ImageSheetModel) sm);

                        //Get the MD5 and remove it from the SlideContent if it is not used anywhere else
                        int slideCount = 0; SlideModel uniqueSlide = null;
                        foreach (SlideModel sdm in this.m_Slides.Values) {
                            using(Synchronizer.Lock(sdm.SyncRoot)) {
                                if (sdm.HasHash(ism.MD5)) {
                                    //don't remove this sheet
                                    slideCount++;
                                    uniqueSlide = sdm;
                                }
                            }
                        }

                        if (slideCount == 0) {
                            this.m_SlideContent.Remove(ism.MD5);
                        }

                        if (m_ContentToSlideLookup != null) {
                            if (slideCount == 0) {
                                this.m_ContentToSlideLookup.Remove(ism.MD5);
                            }
                            if (slideCount == 1) {
                                this.m_ContentToSlideLookup[ism.MD5] = uniqueSlide;
                            }
                        }
                    }
                }

                using(Synchronizer.Lock(this)) { // Prevent event listeners from being registered or unregistered.
                    if(this.m_SlideRemovedDelegate != null)
                        this.m_SlideRemovedDelegate(this, new PropertyCollectionEventArgs("Slides", -1, slide, null));
                }
            }
        }

        #endregion

        public DeckModel Copy() {
            //should it have a new id?
            string hn = "";
            using (Synchronizer.Lock(this)) { hn = this.HumanName; }
             DeckModel deck = new DeckModel(this.Id, this.Disposition, hn, this.TableOfContents, this.m_Slides, this.m_SlideContent, this.m_ContentToSlideLookup);
            using (Synchronizer.Lock(deck)) {
                using (Synchronizer.Lock(this)) {
                    deck.Dirty = this.Dirty;
                    deck.DeckBackgroundColor = this.DeckBackgroundColor;
                    deck.DeckBackgroundTemplate = this.DeckBackgroundTemplate;
                }
            }
            deck.current_subs = this.current_subs;
            deck.current_poll = this.current_poll;
            deck.Group = this.Group;
            deck.Filename = this.Filename;
            return deck;
        }
    }

    /// <summary>
    /// Serializable wrapper around the ImageResourcesModel that allows us to save only 
    /// the images for a particular slide deck in that deck. This helper class keeps 
    /// track of all the images used by this deck and maintains a strong reference to 
    /// those images.
    /// </summary>
    [Serializable]
    public class ImageHashtable : ISerializable {
        #region Public Members

        /// <summary>
        /// The set of keys in the hashtable
        /// </summary>
        public ICollection Keys {
            get {
                return this.ht.Keys;
            }
        }

        #endregion

        #region Private Members

        /// <summary>
        /// A hashtable of (MD5,image) pairs used
        /// </summary>
        [NonSerialized]
        private Hashtable ht = new Hashtable();
        /// <summary>
        /// The global array of all (MD5,image) pairs used in the application
        /// </summary>
        [NonSerialized]
        private readonly ImageResourcesModel m_ImageResourcesModel;

        #endregion

        #region Construction

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="m">The ImageResourcesModel we should stay in sync with</param>
        public ImageHashtable() {
            this.m_ImageResourcesModel = DeckModel.ImagePool;
        }

        #endregion

        #region Serialization

        /// <summary>
        /// Deserializes the hash table from a stream.
        /// NOTE: We want to deserialize to both the local and global HashTables
        /// FIXME: Need to put a check to ensure we don't add items twice?
        /// </summary>
        /// <param name="info">The info about the params</param>
        /// <param name="context">The serialized stream</param>
        protected ImageHashtable(SerializationInfo info, StreamingContext context) : this() {
            ImageHashTableCollection toDeserialize = (ImageHashTableCollection)info.GetValue("items", typeof(ImageHashTableCollection));
            for (int i = 0; i < toDeserialize.items.Length; i++) {
                this.Add( toDeserialize.items[i].key, toDeserialize.items[i].image );
            }
        }

        /// <summary>
        /// Serialize the hashtable of local images.
        /// </summary>
        /// <param name="info">The info about the params</param>
        /// <param name="context">The serialized stream</param>
        public void GetObjectData(SerializationInfo info, StreamingContext context) {
            ArrayList itemsToSerialize = new ArrayList();
            foreach (ByteArray currentKey in this.ht.Keys) {
                itemsToSerialize.Add(new ImageHashtable.ImageHashTableItem(currentKey, (Image)this.ht[currentKey]));
            }
            ImageHashTableCollection toSerialize = new ImageHashTableCollection();
            toSerialize.items = (ImageHashtable.ImageHashTableItem[])itemsToSerialize.ToArray(typeof(ImageHashtable.ImageHashTableItem));
            info.AddValue("items", toSerialize);
        }

        #endregion

        #region ImageHashTableCollection class

        /// <summary>
        /// A collection of hash table items that can be serialized to a stream
        /// FIXME: Does this work to serialize weak references?
        /// </summary>
        [Serializable]
        public class ImageHashTableCollection : ISerializable {
            /// <summary>
            /// The items in the collection
            /// </summary>
            public ImageHashTableItem[] items;

            /// <summary>
            /// Constructor
            /// </summary>
            public ImageHashTableCollection() {}

            /// <summary>
            /// Deserializes the collection
            /// </summary>
            /// <param name="info">The info about the params</param>
            /// <param name="context">The stream to serialize</param>
            protected ImageHashTableCollection(SerializationInfo info, StreamingContext context) {
                this.items = (ImageHashtable.ImageHashTableItem[])info.GetValue("items", typeof(ImageHashtable.ImageHashTableItem[]));
            }

            /// <summary>
            /// Serializes the collection
            /// </summary>
            /// <param name="info">The info about the params</param>
            /// <param name="context">The stream to serialize</param>
            public void GetObjectData(SerializationInfo info, StreamingContext context) {
                info.AddValue("items", this.items);
            }
        }

        #endregion

        #region ImageHashTableItem class

        /// <summary>
        /// Represents an item in the hashtable
        /// </summary>
        [Serializable]
        public class ImageHashTableItem : ISerializable {
            #region Public Members

            /// <summary>
            /// The MD5 hash of the image
            /// </summary>
            public ByteArray key;

            /// <summary>
            /// The image itself
            /// </summary>
            private Image m_image;
            public Image image { 
                get{ return m_image; }
            }

            #endregion

            #region Construction

            /// <summary>
            /// Constructor
            /// </summary>
            /// <param name="key">The MD5 hash of the image</param>
            /// <param name="image">The image corresponding to the given key</param>
            public ImageHashTableItem(ByteArray key, Image image) {
                this.key = key;
                this.m_image = image;
            }

            #endregion

            #region Serialization

            /// <summary>
            /// Constructs a hashtable item from the given serialization context
            /// </summary>
            /// <param name="info">The field information for the class</param>
            /// <param name="context">The stream containing the serialized object</param>
            protected ImageHashTableItem(SerializationInfo info, StreamingContext context) {
                //Get the key
                this.key = (ByteArray)info.GetValue("key", typeof(ByteArray));
                //Get the image
                byte[] bits = (byte[])info.GetValue("image", typeof(byte[]));
                string type = info.GetString("type");
                if (type == "emf") {
                    IntPtr ptr = SetEnhMetaFileBits( (uint)bits.Length, bits );
                    Debug.Assert( ptr != IntPtr.Zero, "Failed deserialization of Image!" );
                    this.m_image = new Metafile( ptr, false );
                } 
                else if(type == "wmf") {
                    IntPtr ptr = SetWinMetaFileBits( (uint)bits.Length, bits, IntPtr.Zero, IntPtr.Zero );
                    Debug.Assert( ptr != IntPtr.Zero, "Failed deserialization of Image!" );
                    this.m_image = new Metafile( ptr, false );
                } 
                else {
                    MemoryStream ms = new MemoryStream(bits);
                    this.m_image = Image.FromStream(ms);
                }
            }

            /// <summary>
            /// Fills the given serialization context with this object
            /// </summary>
            /// <param name="info">The field information for the class</param>
            /// <param name="context">The stream containing the serialized object</param>
            public void GetObjectData(SerializationInfo info, StreamingContext context) {
                //Add key
                info.AddValue("key", this.key);
                //Add image in a special way
                byte[] bits = null;

                // The Image class is not capable of multithreaded access.
                // Simultaneous access will throw a
                //    "InvalidOperationException: The object is currently in use elsewhere."
                // So it needs to be locked, and also locked anywhere else the Image is accessed
                // (such as in ImageSheetRenderer.Paint).
                using(Synchronizer.Lock(this.m_image)) {
                    if( this.m_image.RawFormat.Guid == ImageFormat.Emf.Guid ) {
                        info.AddValue( "type", "emf");
                        Metafile mf = (Metafile)((Metafile)this.m_image).Clone();
                        IntPtr ptr = mf.GetHenhmetafile();
                        Debug.Assert(ptr != IntPtr.Zero, "Failed to get pointer to image." );
                        uint size = GetEnhMetaFileBits( ptr, 0, null );
                        bits = new byte[size];
                        uint numBits = GetEnhMetaFileBits( ptr, size, bits );
                        mf.Dispose();
                        Debug.Assert( size == numBits, "Improper serialization of metafile!" );
                    } else if( this.m_image.RawFormat.Guid == ImageFormat.Wmf.Guid ) {
                        info.AddValue( "type", "wmf");
                        Metafile mf = (Metafile)((Metafile)this.m_image).Clone();
                        IntPtr ptr = mf.GetHenhmetafile();
                        Debug.Assert(ptr != IntPtr.Zero, "Failed to get pointer to image." );
                        uint size = GetMetaFileBitsEx( ptr, 0, null );
                        bits = new byte[size];
                        uint numBits = GetMetaFileBitsEx( ptr, size, bits );
                        mf.Dispose();
                        Debug.Assert( size == numBits, "Improper serialization of metafile!" );
                    } else if( this.m_image.RawFormat.Guid == ImageFormat.Png.Guid ) {
                        info.AddValue( "type", "png");
                        System.IO.MemoryStream ms = new System.IO.MemoryStream(100000);
                        this.m_image.Save(ms, ImageFormat.Png);
                        bits = ms.ToArray();
                    } else if( this.m_image.RawFormat.Guid == ImageFormat.Jpeg.Guid ) {
                        info.AddValue( "type", "Jpeg");
                        System.IO.MemoryStream ms = new System.IO.MemoryStream();
                        long[] quality = new long[1];
                        quality[0] = 100;
                        System.Drawing.Imaging.EncoderParameters encoderParams = new System.Drawing.Imaging.EncoderParameters();
                        System.Drawing.Imaging.EncoderParameter encoderParam = new System.Drawing.Imaging.EncoderParameter(System.Drawing.Imaging.Encoder.Quality, quality);
                        encoderParams.Param[0] = encoderParam;
                        ImageCodecInfo[] arrayICI = ImageCodecInfo.GetImageEncoders();
                        ImageCodecInfo jpegICI = null;
                        for (int x = 0; x < arrayICI.Length; x++) {
                            if (arrayICI[x].FormatDescription.Equals("JPEG")) {
                                jpegICI = arrayICI[x];
                                break;
                            }
                        }
                        if (jpegICI != null) {
                            this.m_image.Save(ms, jpegICI, encoderParams);
                        }

                        bits = ms.ToArray();
                    } else {
                        info.AddValue( "type", "png");
                        System.IO.MemoryStream ms = new System.IO.MemoryStream(100000);
                        this.m_image.Save(ms, ImageFormat.Png);
                        bits = ms.ToArray();
                    }
                }
                info.AddValue("image", bits );
            }

            [DllImport("Gdi32.dll")]
            private static extern uint GetEnhMetaFileBits( IntPtr hMetaFile, uint size, [In,Out]byte[] pData );

            [DllImport("Gdi32.dll")]
            private static extern IntPtr SetEnhMetaFileBits( uint bufferSize, byte[] pData );

            [DllImport("Gdi32.dll")]
            private static extern bool DeleteEnhMetaFile( IntPtr hMetaFile );

            [DllImport("Gdi32.dll")]
            private static extern uint GetMetaFileBitsEx( IntPtr hMetaFile, uint size, [In,Out]byte[] pData );

            [DllImport("Gdi32.dll")]
            private static extern IntPtr SetWinMetaFileBits( uint bufferSize, byte[] pData, IntPtr hDC, IntPtr lpStruct );

            #endregion
        }

        #endregion

        #region Methods

        /// <summary>
        /// Add an item to the ImageHashtable
        /// NOTE: Don't add anything to the array if it already exists in the array.
        /// </summary>
        /// <param name="key">The MD5 of the image to add</param>
        /// <param name="image">The image to add to the hashtable</param>
        public void Add(ByteArray key, Image image) 
        {
            if (!this.m_ImageResourcesModel.Contains(key)) {
                this.m_ImageResourcesModel.Add(key, image);         
            }
            else {
                if (this.m_ImageResourcesModel[key] == null) {
                    //It's possible for images shared across multiple decks to be garbage collected.
                    //In this case, restore the image.
                    this.m_ImageResourcesModel.Remove(key);
                    this.m_ImageResourcesModel.Add(key, image);
                }
            }
            this.ht.Add(key, this.m_ImageResourcesModel[key] );
        }

        /// <summary>
        /// Indexer for the hashtable
        /// </summary>
        /// <param name="key">The MD5 to look up</param>
        /// <returns>The image associated with the given MD5</returns>
        public Image this[ByteArray key] {
            get { return (Image)this.ht[key]; }
        }

        /// <summary>
        /// Remove an item from the local hash table
        /// NOTE: Only remove the strong reference that we have locally.
        /// </summary>
        /// <param name="key">The MD5 of the item to remove</param>
        public void Remove(ByteArray key) {
            this.ht.Remove(key);
        }

        /// <summary>
        /// Returns whether or not the local hash table contains the given MD5 key
        /// </summary>
        /// <param name="key">The MD5 to lookup</param>
        /// <returns>True if the key exists, false otherwise</returns>
        public bool Contains(ByteArray key) {
            return this.ht.Contains(key);
        }

        /// <summary>
        /// Returns whether or not the local hash table contains the given MD5 key
        /// </summary>
        /// <param name="key">The MD5 to lookup</param>
        /// <returns>True if the key exists, false otherwise</returns>
        public bool ContainsKey(ByteArray key) {
            return this.ht.ContainsKey(key);
        }

        /// <summary>
        /// Returns whether or not the local hash table contains the given image as
        /// a value
        /// </summary>
        /// <param name="key">The image to look for</param>
        /// <returns>True if the image exists, false otherwise</returns>
        public bool ContainsValue(Image image) {
            return this.ht.ContainsValue(image);
        }

        #endregion
    }
}
