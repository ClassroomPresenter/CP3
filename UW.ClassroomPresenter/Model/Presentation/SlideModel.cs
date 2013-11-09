// $Id: SlideModel.cs 1934 2009-07-21 18:04:51Z jing $

using System;
using System.Diagnostics;
using System.Drawing;

using UW.ClassroomPresenter.Model.Background;

namespace UW.ClassroomPresenter.Model.Presentation {
    [Serializable]
    public class SlideModel : PropertyPublisher {

        public enum StudentSubmissionStyle {
            Normal = 0,
            Shared = 1,
            Realtime = 2
        }

        private readonly Guid m_Id;
        private readonly LocalId m_LocalId;
        private readonly SheetCollection m_ContentSheets;
        private readonly SheetCollection m_AnnotationSheets;

        private readonly Guid m_AssociationId = Guid.Empty;
        private readonly int m_AssociationSlideIndex = -1;
        private readonly Guid m_AssociationDeckId = Guid.Empty;
        private readonly DeckDisposition m_AssociationDeckDispostion = DeckDisposition.Empty;

        // Published properties:
        private Rectangle m_Bounds;
        private string m_Title;
        private float m_Zoom;
        private Color m_BackgroundColor;

        private BackgroundTemplate m_BackgroundTemplate;

        // For student navigation feature
        [NonSerialized]
        private bool m_Visited = false;

        public SlideModel(Guid id, LocalId localId, SlideDisposition disposition) : this(id, localId, disposition, Rectangle.Empty) {}

        public SlideModel(Guid id, LocalId localId, SlideDisposition disposition, Rectangle bounds) {
            this.m_Id = id;
            this.m_LocalId = localId;
            this.m_Disposition = disposition;
            this.m_ContentSheets = new SheetCollection(this, "ContentSheets");
            this.m_AnnotationSheets = new SheetCollection(this, "AnnotationSheets");
            this.m_Bounds = bounds;
            this.m_Title = "";
            this.m_Zoom = 1f;
            this.m_BackgroundColor = Color.Empty;
            this.m_SubmissionSlideGuid = Guid.Empty;
            this.m_SubmissionStyle = StudentSubmissionStyle.Normal;
            this.m_Visited = false;
        }

        public SlideModel(Guid id, LocalId localId, SlideDisposition disposition, Rectangle bounds, Guid associationId)
            : this(id, localId, disposition, bounds) {
            this.m_AssociationId = associationId;

            //If this is a student submission, the associationID should be set.  In this case we
            //gather some extra information about the association slide and deck.
            if (!m_AssociationId.Equals(Guid.Empty)) {
                PresentationModel pres = PresentationModel.CurrentPresentation;
                if (pres != null) {
                    PresentationModel.DeckTraversalCollection traversals;
                    traversals = pres.DeckTraversals;
                    foreach (DeckTraversalModel dtm in traversals) {
                        DeckModel deck;
                        using (Synchronizer.Lock(dtm.SyncRoot)) {
                            deck = dtm.Deck;
                        }
                        Guid deckId;
                        DeckDisposition deckDisp;
                        TableOfContentsModel toc;
                        using (Synchronizer.Lock(deck.SyncRoot)) {
                            deckId = deck.Id;
                            deckDisp = deck.Disposition;
                            toc = deck.TableOfContents;
                        }
                        TableOfContentsModel.Entry tocEntry = toc.GetEntryBySlideId(m_AssociationId);
                        if (tocEntry != null) {
                            using (Synchronizer.Lock(tocEntry.SyncRoot)) {
                                m_AssociationSlideIndex = tocEntry.IndexInParent;
                            }
                            m_AssociationDeckDispostion = deckDisp;
                            m_AssociationDeckId = deckId;
                            break;
                        }
                    }
                }
            }
        }

        [Published] public string Title {
            get { return this.GetPublishedProperty("Title", ref this.m_Title); }
            set { this.SetPublishedProperty("Title", ref this.m_Title, value); }
        }

        public Guid Id {
            get { return this.m_Id; }
        }

        /// <summary>
        /// If this is a Student Submission slide, the AssociationX properties 
        /// contain information about the original slide and deck
        /// upon which the student submission is based.
        /// This is only used for integration with CXP archiving.
        /// </summary>
        public Guid AssociationId {
            get { return this.m_AssociationId;  }
        }

        public Guid AssociationDeckId {
            get { return this.m_AssociationDeckId;  }
        }

        public int AssociationSlideIndex {
            get { return this.m_AssociationSlideIndex; }
        }

        public DeckDisposition AssociationDeckDisposition {
            get { return this.m_AssociationDeckDispostion; }
        }

        public LocalId LocalId {
            get { return this.m_LocalId; }
        }

        private readonly SlideDisposition m_Disposition;
        public SlideDisposition Disposition {
            get { return this.m_Disposition; }
        }

        [Published] public Rectangle Bounds {
            get { return this.GetPublishedProperty("Bounds", ref this.m_Bounds); }
            set { this.SetPublishedProperty("Bounds", ref this.m_Bounds, value); }
        }

        [Published] public float Zoom {
            get { return this.GetPublishedProperty("Zoom", ref this.m_Zoom); }
            set { this.SetPublishedProperty("Zoom", ref this.m_Zoom, value); }
        }

        [Published] public Color BackgroundColor {
            get { return this.GetPublishedProperty("BackgroundColor", ref this.m_BackgroundColor); }
            set { this.SetPublishedProperty("BackgroundColor", ref this.m_BackgroundColor, value); }
        }

        [Published] public BackgroundTemplate BackgroundTemplate{
            get { return this.GetPublishedProperty("BackgroundTemplate", ref this.m_BackgroundTemplate); }
            set { this.SetPublishedProperty("BackgroundTemplate", ref this.m_BackgroundTemplate, value); }
        }

        [Published] public bool Visited{
            get { return this.GetPublishedProperty("Visited", ref this.m_Visited); }
            set { this.SetPublishedProperty("Visited", ref this.m_Visited, value); }
        }

        /// <summary>
        /// This is the guid used when sending student submission messages about this slide
        /// </summary>
        private System.Guid m_SubmissionSlideGuid;
        [Published] public System.Guid SubmissionSlideGuid {
            get { return this.GetPublishedProperty( "SubmissionSlideGuid", ref this.m_SubmissionSlideGuid ); }
            set { this.SetPublishedProperty( "SubmissionSlideGuid", ref this.m_SubmissionSlideGuid, value ); }
        }

        /// <summary>
        /// This is the style of student submission being used for this slide
        /// </summary>
        private StudentSubmissionStyle m_SubmissionStyle;
        [Published] public StudentSubmissionStyle SubmissionStyle {
            get { return this.GetPublishedProperty( "SubmissionStyle", ref this.m_SubmissionStyle ); }
            set { this.SetPublishedProperty( "SubmissionStyle", ref this.m_SubmissionStyle, value ); }
        }

        public bool HasHash(ByteArray hash) {
            // A lock is not required to get the MD5 property of the image sheets,
            // but we should ensure that the ContentSheets and AnnotationSheets collections
            // don't change while we're iterating.
            Synchronizer.AssertLockIsHeld(this.SyncRoot);

            // Search all of the content sheets and annotation sheets for the specified hash.

            foreach(SheetModel sm in this.m_ContentSheets)
                if(sm is ImageSheetModel && ((ImageSheetModel) sm).MD5 == hash)
                    return true;

            foreach(SheetModel sm in this.m_AnnotationSheets)
                if(sm is ImageSheetModel && ((ImageSheetModel) sm).MD5 == hash)
                    return true;

            return false;
        }

        /// <summary>
        /// Removes the remote component of the sheet dispositions
        /// </summary>
        public void RemoveRemoteDisposition() {

            foreach (SheetModel s in this.AnnotationSheets) {
                SheetDisposition d = s.Disposition & ~SheetDisposition.Remote;
                s.Disposition = d;

            }
        }


        #region Sheets

        [Published] public SheetCollection ContentSheets {
            get { return this.m_ContentSheets; }
        }

        [Published] public SheetCollection AnnotationSheets {
            get { return this.m_AnnotationSheets; }
        }

        [Serializable]
        public class SheetCollection : PropertyCollectionBase {
            internal SheetCollection(PropertyPublisher owner, string property) : base(owner, property) {
            }

            public SheetModel this[int index] {
                get { return ((SheetModel) List[index]); }
                set { List[index] = value; }
            }

            /// <summary>
            /// Adds the sheets to the SheetCollection such 
            /// that ink is always on top of text which is always on top of images
            /// </summary>
            /// <param name="value"></param>
            /// <returns></returns>
            public void Add(SheetModel value) {
                if (List.Count == 0) {
                    List.Add(value);
                } else {
                    int i = 0;
                    SheetModel sheet = (SheetModel)List[i];
                    while (sheet.CompareTo(value) < 1) {
                        i++;
                        if (i < List.Count)
                            sheet = (SheetModel)List[i];
                        else
                            break;
                    }
                    List.Insert(i, value);
                }
            }

            /// <summary>
            /// To move a sheet to the top, all we need to do is remove and add it.
            /// this is due to the virtue of the Add method, which inserts a sheet
            /// in the frontmost position of its allowed ordering, based on the indexing
            /// heirarchy.
            /// </summary>
            /// <param name="value"></param>
            public void MoveToTop(SheetModel value) {
                List.Remove(value);
                this.Add(value);
            }

            public int IndexOf(SheetModel value) {
                return List.IndexOf(value);
            }

            public void Insert(int index, SheetModel value) {
                List.Insert(index, value);
            }

            public void Remove(SheetModel value) {
                List.Remove(value);
            }


            public bool Contains(SheetModel value) {
                return List.Contains(value);
            }

            protected override void OnValidate(Object value) {
                if(!typeof(SheetModel).IsInstanceOfType(value))
                    throw new ArgumentException("Value must be of type SheetModel.", "value");
            }
        }

        #endregion Sheets
    }
}
