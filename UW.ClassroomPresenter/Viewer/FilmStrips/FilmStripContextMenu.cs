// $Id: FilmStripContextMenu.cs 1824 2009-03-10 23:47:34Z lining $

using System;
using System.Diagnostics;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;

using UW.ClassroomPresenter.Model;
using UW.ClassroomPresenter.Model.Presentation;
using System.IO;

namespace UW.ClassroomPresenter.Viewer.FilmStrips {

    /// <summary>
    /// Operations for manipulating decks associated with the film strip.  These are the commands accessible from the context menu.
    /// </summary>
    /// 

    // RJA (9/08),  this seems like the wrong place to hide all of this deck manipulation code.  I am also not sure why the individual menu items are inside the context menu class
    // since we will be using them from other menus as well.
    public class FilmStripContextMenu : ContextMenu {
        private readonly PresenterModel m_Model;
        private readonly DeckTraversalModel m_DeckTraversal;

        public FilmStripContextMenu(DeckTraversalModel traversal, PresenterModel model) {
            this.m_Model = model;
            this.m_DeckTraversal = traversal;

            this.MenuItems.Add(new InsertSlideMenuItem(m_DeckTraversal, this.m_Model));
            this.MenuItems.Add(new DuplicateSlideMenuItem(m_DeckTraversal, this.m_Model));
            this.MenuItems.Add(new PublicSlideMenuItem(m_DeckTraversal, this.m_Model));
            this.MenuItems.Add(new InsertSlidesFromFileMenuItem(m_DeckTraversal, this.m_Model));
            this.MenuItems.Add(new RemoveSlideMenuItem(m_DeckTraversal, this.m_Model));
        }

        #region InsertSlidesFromFileMenuItem

        /// <summary>
        /// Insert a file into a slide deck.  The file may be either CP3 or PPT
        /// </summary>
        /// 

        // There is a known issue with SlideInsertion from a file - if the slide deck is large
        // the deck may not show up properly - we discovered the problem is that if we have n slides in the 
        // deck, and add m slide,  there are nm events generated, and if n and m are large, this can overflow an event queue
        // causing strange behavior.  Fixing the problem would be a significant amount of work.  
        // One thing we have not done is to give a warning (or maybe even truncate the deck)
        public class InsertSlidesFromFileMenuItem : MenuItem {
            private readonly DeckModel deck_;
            private DeckTraversalModel deck_traversal_;
            private readonly PresenterModel m_Model;

            // Allows the new slide to be added as a child of another TOC entry, or to the main TOC.
            private readonly TableOfContentsModel.EntryCollection m_WhereTheEntriesGo;

            public InsertSlidesFromFileMenuItem(DeckTraversalModel traversal, PresenterModel model)
                : base(Strings.InsertSlidesFromDeck) {
                this.deck_ = traversal.Deck;
                this.deck_traversal_ = traversal;
                this.m_WhereTheEntriesGo = traversal.Deck.TableOfContents.Entries;
                this.m_Model = model;
            }

            protected override void OnClick(EventArgs e) {
                base.OnClick(e);
                InsertDeck();

            }
            private void InsertDeck() {
                OpenFileDialog open_file = new OpenFileDialog();
                open_file.Filter = "CP3, PPT, PPTX, XPS files (*.cp3; *.ppt; *.pptx; *.xps)|*.cp3;*.ppt;*.pptx;*.xps";

                if (open_file.ShowDialog() == DialogResult.OK) {
                    using (Synchronizer.Lock(this.deck_.SyncRoot)) {
                        this.deck_.Dirty = true;
                    }
                    InsertDeck(new FileInfo(open_file.FileName));
                }
            }
            private void InsertDeck(FileInfo file) {

                ///get the index of the current slide that's selected,
                ///so that we can insert our deck there
                int current_slide_index;
                using (Synchronizer.Lock(deck_traversal_.SyncRoot)) {
                    current_slide_index = deck_traversal_.AbsoluteCurrentSlideIndex;
                }

                ///if current_slide_index == -1, this means there are no slides in current deck.
                ///This new slide we try to insert is the first slide.
                if (current_slide_index < 0) {
                    current_slide_index = 0;
                }

                /// get the deck of slides to insert
                DeckModel deck = null;
                if (file.Extension == ".cp3") {
                    deck = Decks.PPTDeckIO.OpenCP3(file);

                }
                else if (file.Extension == ".ppt") {
                    deck = Decks.PPTDeckIO.OpenPPT(file);
                }
                else if (file.Extension == ".pptx") {
                    deck = Decks.PPTDeckIO.OpenPPT(file);
                }
                else if (file.Extension == ".xps") {
                    deck = Decks.XPSDeckIO.OpenXPS(file);
                }
                if (deck == null) {     // This should never happen,  since we have filtered on extension
                    throw new Exception("Wrong file extension");
                }
                else {
                    deck_.InsertDeck(deck);
                }

                using (Synchronizer.Lock(deck.TableOfContents.SyncRoot)) {
                    using (Synchronizer.Lock(deck_.TableOfContents.SyncRoot)) {
                        ///insert each slide from the deck into our current deck.
                        for (int i = 0; i < deck.TableOfContents.Entries.Count; i++) {
                            ///create our TOC entry
                            TableOfContentsModel.Entry entry = new TableOfContentsModel.Entry(Guid.NewGuid(), deck_.TableOfContents, deck.TableOfContents.Entries[i].Slide);
                            ///insert it into our TOC
                            this.m_WhereTheEntriesGo.Insert(current_slide_index + i, entry);//Natalie - should it be +1?
                        }
                    }
                }

            }
        }
        #endregion

        #region InsertSlideMenuItem

        /// <summary>
        /// Inserts a blank slide in the current deck at the selected index.  If the index is not selected,  nothing is done
        /// </summary>
        public class InsertSlideMenuItem : MenuItem {

            private readonly DeckModel m_Deck;

            private DeckTraversalModel traversal_;
            private readonly PresenterModel m_Model;

            // Allows the new slide to be added as a child of another TOC entry, or to the main TOC.
            private readonly TableOfContentsModel.EntryCollection m_WhereTheEntriesGo;

            public InsertSlideMenuItem(DeckTraversalModel traversal, PresenterModel model) : this(traversal.Deck, traversal.Deck.TableOfContents.Entries, traversal, model) { }

            public InsertSlideMenuItem(DeckModel deck, TableOfContentsModel.EntryCollection bucket, DeckTraversalModel traversal, PresenterModel model)
                : base(Strings.NewSlide) {
                this.m_Deck = deck;
                this.m_WhereTheEntriesGo = bucket;
                this.traversal_ = traversal;
                this.m_Model = model;
                // TODO: Disable this menu item if the deck is immutable (requires defining what sorts of decks are mutable or not).
            }

            protected override void OnClick(EventArgs e) {
                base.OnClick(e);
                InsertSlide();
            }

            private void InsertSlide() {


                ///get the index of the current slide that's selected,
                ///so that we can insert our blank slide there
                int current_slide_index;
                using (Synchronizer.Lock(traversal_.SyncRoot)) {
                    current_slide_index = traversal_.AbsoluteCurrentSlideIndex;
                }

                ///if current_slide_index == -1, this means there are no slides in current deck.
                ///This new slide we try to insert is the first slide.
                if (current_slide_index < 0) {
                    current_slide_index = 0;
                }

                // Create a blank slide
                SlideModel slide;
                using (Synchronizer.Lock(this.m_Deck.SyncRoot)) {
                    slide = new SlideModel(Guid.NewGuid(), new LocalId(), SlideDisposition.Empty, UW.ClassroomPresenter.Viewer.ViewerForm.DEFAULT_SLIDE_BOUNDS);
                    this.m_Deck.Dirty = true;
                    this.m_Deck.InsertSlide(slide);
                }

                ///Insert our blank slide after the current index. This is modeled after the powerpoint
                ///UI
                using (Synchronizer.Lock(this.m_Deck.TableOfContents.SyncRoot)) {
                    TableOfContentsModel.Entry entry = new TableOfContentsModel.Entry(Guid.NewGuid(), this.m_Deck.TableOfContents, slide);
                    this.m_WhereTheEntriesGo.Insert(current_slide_index, entry);
                }
            }
        }
        #endregion

        #region DuplicateSlideMenuItem

        /// <summary>
        /// Adds a copy of a slide at the current location in the deck.  If the index is not selected,  nothing is done
        /// </summary>
        public class DuplicateSlideMenuItem : MenuItem {

            private readonly DeckModel m_Deck;

            private DeckTraversalModel traversal_;
            private readonly PresenterModel m_Model;

            // Allows the new slide to be added as a child of another TOC entry, or to the main TOC.
            private readonly TableOfContentsModel.EntryCollection m_WhereTheEntriesGo;

            public DuplicateSlideMenuItem(DeckTraversalModel traversal, PresenterModel model) : this(traversal.Deck, traversal.Deck.TableOfContents.Entries, traversal, model) { }

            public DuplicateSlideMenuItem(DeckModel deck, TableOfContentsModel.EntryCollection bucket, DeckTraversalModel traversal, PresenterModel model)
                : base(Strings.DuplicateSlide) {
                this.m_Deck = deck;
                this.m_WhereTheEntriesGo = bucket;
                this.traversal_ = traversal;
                this.m_Model = model;
                // TODO: Disable this menu item if the deck is immutable (requires defining what sorts of decks are mutable or not).
            }

            protected override void OnClick(EventArgs e) {
                base.OnClick(e);
                DuplicateSlide();
            }

            private void DuplicateSlide() {


                ///get the index of the current slide that's selected,
                ///so that we can insert our blank slide there
                int current_slide_index;
                using (Synchronizer.Lock(traversal_.SyncRoot)) {
                    current_slide_index = traversal_.AbsoluteCurrentSlideIndex - 1;
                }

                ///don't do anything if no object is selected
                if (current_slide_index < 0) {
                    return;
                }

                // Copy slide
                SlideModel newSlide;
                using (Synchronizer.Lock(this.m_Deck.SyncRoot)) {
                    SlideModel currentSlide = this.m_Deck.TableOfContents.Entries[current_slide_index].Slide;
                    newSlide = CloneSlideAndSheets(currentSlide);
                    //remove remote disposition for student submission slide; The removal may be not necessary for non-student submission slide.
                    //If current role is non-instructor,the sheet models in slide should contain remote disposition 
                    if ((currentSlide.Disposition & SlideDisposition.StudentSubmission)!=0) {
                        newSlide.RemoveRemoteDisposition();
                    }
                    this.m_Deck.Dirty = true;
                    this.m_Deck.InsertSlide(newSlide);
                }

                ///Insert our current slide after the current index. This is modeled after the powerpoint
                ///UI
                using (Synchronizer.Lock(this.m_Deck.TableOfContents.SyncRoot)) {
                    TableOfContentsModel.Entry entry = new TableOfContentsModel.Entry(Guid.NewGuid(), this.m_Deck.TableOfContents, newSlide);
                    this.m_WhereTheEntriesGo.Insert(current_slide_index + 1, entry);
                }
            }

            /*
            private SlideModel CloneSlideAndSheets(SlideModel slide) {

                // Create the new slide to add
                SlideModel newSlide = new SlideModel(Guid.NewGuid(), new LocalId(), SlideDisposition.Empty, UW.ClassroomPresenter.Viewer.ViewerForm.DEFAULT_SLIDE_BOUNDS);


                // Update the fields of the slide
                using (Synchronizer.Lock(newSlide.SyncRoot)) {
                    using (Synchronizer.Lock(slide.SyncRoot)) {
                        newSlide.Title = slide.Title;
                        newSlide.Bounds = slide.Bounds;
                        newSlide.Zoom = slide.Zoom;
                        newSlide.BackgroundColor = slide.BackgroundColor;
                        newSlide.BackgroundTemplate = slide.BackgroundTemplate;
                        newSlide.SubmissionSlideGuid = slide.SubmissionSlideGuid;
                        newSlide.SubmissionStyle = slide.SubmissionStyle;

                        // Copy all of the content sheets.
                        // Because ContentSheets do not change, there is no 
                        // need to do a deep copy (special case for ImageSheetModels).
                        foreach (SheetModel s in slide.ContentSheets) {
                            newSlide.ContentSheets.Add(s);
                        }

                        // Make a deep copy of all the ink sheets
                        foreach (SheetModel s in slide.AnnotationSheets) {
                            SheetModel newSheet = UW.ClassroomPresenter.Model.Presentation.SheetModel.SheetDeepCopyHelper(s);
                            newSlide.AnnotationSheets.Add(newSheet);

                        }
                    }
                }

                return newSlide;

            }
              */



        }
        #endregion

        #region PublicSlideMenuItem

        /// <summary>
        /// Transfer a slide from the current deck to the public deck - this allows for redistribution of student submissions
        /// </summary>
        public class PublicSlideMenuItem : MenuItem {

            private readonly DeckModel m_Deck;

            private DeckTraversalModel traversal_;
            private readonly PresenterModel m_Model;

            // Allows the new slide to be added as a child of another TOC entry, or to the main TOC.
            private readonly TableOfContentsModel.EntryCollection m_WhereTheEntriesGo;

            public PublicSlideMenuItem(DeckTraversalModel traversal, PresenterModel model) : this(traversal.Deck, traversal.Deck.TableOfContents.Entries, traversal, model) { }

            public PublicSlideMenuItem(DeckModel deck, TableOfContentsModel.EntryCollection bucket, DeckTraversalModel traversal, PresenterModel model)
                : base(Strings.CopyToPublic) {
                this.m_Deck = deck;
                this.m_WhereTheEntriesGo = bucket;
                this.traversal_ = traversal;
                this.m_Model = model;
                if ((deck.Disposition & DeckDisposition.StudentSubmission) == 0)          // Only enable for student submissions
                    this.Enabled = false;
            }

            protected override void OnClick(EventArgs e) {
                base.OnClick(e);
                CopySlideToPublicDeck();
            }

            private void CopySlideToPublicDeck() {


                ///get the index of the current slide that's selected,
                ///so that we can insert our blank slide there
                int current_slide_index;
                using (Synchronizer.Lock(traversal_.SyncRoot)) {
                    current_slide_index = traversal_.AbsoluteCurrentSlideIndex - 1;
                }

                ///don't do anything if no object is selected
                if (current_slide_index < 0) {
                    return;
                }

                DeckModel publicDeck = GetPublicSubmissionDeck();

                // Copy slide
                SlideModel newSlide;
                using (Synchronizer.Lock(this.m_Deck.SyncRoot)) {
                    SlideModel currentSlide = this.m_Deck.TableOfContents.Entries[current_slide_index].Slide;
                    newSlide = CloneSlideAndSheets(currentSlide);
                    using (Synchronizer.Lock(newSlide.SyncRoot)) {
                        using (Synchronizer.Lock(currentSlide.SyncRoot)) {
                            if (currentSlide.BackgroundTemplate == null)
                                newSlide.BackgroundTemplate = this.m_Deck.DeckBackgroundTemplate;
                            if (currentSlide.BackgroundColor == Color.Empty)
                                newSlide.BackgroundColor = this.m_Deck.DeckBackgroundColor;
                        }
                    }
                    newSlide.RemoveRemoteDisposition();
                }

                using (Synchronizer.Lock(publicDeck.SyncRoot)) {
                    publicDeck.Dirty = true;
                    publicDeck.InsertSlide(newSlide);
                    UW.ClassroomPresenter.Network.Messages.Message.AddLocalRef(newSlide.Id, newSlide);

                }

                using (Synchronizer.Lock(publicDeck.TableOfContents.SyncRoot)) {
                    TableOfContentsModel.Entry e = new TableOfContentsModel.Entry(Guid.NewGuid(), publicDeck.TableOfContents, newSlide);
                    publicDeck.TableOfContents.Entries.Add(e);
                    UW.ClassroomPresenter.Network.Messages.Message.AddLocalRef(e.Id, e);
                }

                ///Insert our current slide after the current index. This is modeled after the powerpoint
                ///UI
                //         using (Synchronizer.Lock(this.m_Deck.TableOfContents.SyncRoot)) {
                //             TableOfContentsModel.Entry entry = new TableOfContentsModel.Entry(Guid.NewGuid(), this.m_Deck.TableOfContents, newSlide);
                //              this.m_WhereTheEntriesGo.Insert(current_slide_index + 1, entry);
                //         }
            }

   

            /// <summary>
            /// Creates the student submissions deck and deck traversal if one doesn't already exist.
            /// Otherwise returns the existing student submission deck
            /// </summary>
            /// <returns>The student submissions deck for this presentation, if one does not 
            /// already exist this function creates one</returns>
            protected DeckModel GetPublicSubmissionDeck() {
                PresentationModel pm;
                using (Synchronizer.Lock(this.m_Model.Workspace.CurrentPresentation.SyncRoot)) {
                    pm = this.m_Model.Workspace.CurrentPresentation;
                }

                if (pm != null) {
                    DeckModel deck = pm.GetPublicSubmissionDeck();

                    if (deck != null)
                        return deck;
                    else {
                        // Create the public submissions deck - copy from student sub code. 
                        // TODO CMPRINCE: We currently hardcode values for the student submissions deck and
                        // deck traversal. Is there a better way to do this?
                        // Natalie - Apparently the reason that these are hardcoded has to do with the 
                        //public display of student submissions - when I switched these guids to normally 
                        //generated guids the public display lost the ability to display student submissions.
                        Guid psGuid = new Guid("{EFC46492-5865-430d-962F-8D39038E2C6F}");
                        Guid psTraversalGuid = new Guid("{18C3CD05-EDCE-4018-AC8B-64E618EB9804}");
                        deck = new DeckModel(psGuid, DeckDisposition.PublicSubmission, "Public Submissions");
                        deck.Group = UW.ClassroomPresenter.Network.Groups.Group.AllParticipant;

                        UW.ClassroomPresenter.Network.Messages.Message.AddLocalRef(psGuid, deck);
                        DeckTraversalModel traversal = new SlideDeckTraversalModel(psTraversalGuid, deck);
                        UW.ClassroomPresenter.Network.Messages.Message.AddLocalRef(psTraversalGuid, traversal);

                        // Add the new student submission deck to the presentation
                        using (Synchronizer.Lock(pm.SyncRoot)) {
                            pm.DeckTraversals.Add(traversal);
                        }

                        return deck;
                    }
                }
                return null;
            }






        }
        #endregion

        #region RemoveSlideMenuItem

        /// <summary>
        /// Removes a slide from the current deck at the current index.
        /// If no index is selected, does nothing.  
        /// </summary>
        public class RemoveSlideMenuItem : MenuItem {

            private readonly DeckModel m_Deck;

            private DeckTraversalModel traversal_;
            private readonly PresenterModel m_Model;

            // Allows the new slide to be deleted from a child of another TOC entry, or to the main TOC.
            private readonly TableOfContentsModel.EntryCollection m_WhereTheEntriesGo;


            public RemoveSlideMenuItem(DeckTraversalModel traversal, PresenterModel model)
                : base(Strings.DeleteSlide) {
                this.m_Deck = traversal.Deck;
                this.m_WhereTheEntriesGo = traversal.Deck.TableOfContents.Entries;
                traversal_ = traversal;
                this.m_Model = model;
            }

            /// <summary>
            /// Remove the currently selected slide
            /// </summary>
            /// <param name="e"></param>
            protected override void OnClick(EventArgs e) {
                base.OnClick(e);
                RemoveSlide();
            }

            /// <summary>
            /// Removes a slide from the current deck at the current index.
            /// If no index is selected, does nothing. 
            /// </summary>
            private void RemoveSlide() {
                ///get the index of the current slide that's selected,
                ///so that we can remove our blank slide
                int current_slide_index;
                using (Synchronizer.Lock(traversal_.SyncRoot)) {
                    ///-1 because of zero based indexing?
                    current_slide_index = traversal_.AbsoluteCurrentSlideIndex - 1;
                }

                ///don't do anything if no object is selected
                if (current_slide_index < 0) {
                    return;
                }
                //Update the current slide
                using (Synchronizer.Lock(this.traversal_.SyncRoot)) {
                    if (current_slide_index + 1 == this.m_WhereTheEntriesGo.Count) {
                        this.traversal_.Current = this.traversal_.Previous;
                    }
                    else {
                        this.traversal_.Current = this.traversal_.Next;
                    }
                }
                ///Remove slide from the Deck
                using (Synchronizer.Lock(m_Deck.SyncRoot)) {
                    this.m_Deck.Dirty = true;
                    m_Deck.DeleteSlide(m_WhereTheEntriesGo[current_slide_index].Slide);
                }

                ///Remove slide at current index from the TOC
                using (Synchronizer.Lock(this.m_Deck.TableOfContents.SyncRoot)) {
                    this.m_WhereTheEntriesGo.RemoveAt(current_slide_index);
                }

            }
        }
        #endregion

        #region Utility Functions

        /// <summary>
        /// Clone slide and sheets - copy a slide with all of the sheets.  Care must be taken in setting the SlideDisposition
        /// </summary>
        /// <param name="slide">Slide to Copy</param>
        /// <returns></returns>
        protected static SlideModel CloneSlideAndSheets(SlideModel slide) {

            // Create the new slide to add
            SlideModel newSlide = new SlideModel(Guid.NewGuid(), new LocalId(), SlideDisposition.Empty, UW.ClassroomPresenter.Viewer.ViewerForm.DEFAULT_SLIDE_BOUNDS);


            // Update the fields of the slide
            using (Synchronizer.Lock(newSlide.SyncRoot)) {
                using (Synchronizer.Lock(slide.SyncRoot)) {
                    newSlide.Title = slide.Title;
                    newSlide.Bounds = slide.Bounds;
                    newSlide.Zoom = slide.Zoom;
                    newSlide.BackgroundColor = slide.BackgroundColor;
                    newSlide.BackgroundTemplate = slide.BackgroundTemplate;
                    newSlide.SubmissionSlideGuid = slide.SubmissionSlideGuid;
                    newSlide.SubmissionStyle = slide.SubmissionStyle;

                    // Copy all of the content sheets.
                    // Because ContentSheets do not change, there is no 
                    // need to do a deep copy (special case for ImageSheetModels).
                    foreach (SheetModel s in slide.ContentSheets) {
                        newSlide.ContentSheets.Add(s);
                    }

                    // Make a deep copy of all the ink sheets
                    foreach (SheetModel s in slide.AnnotationSheets) {
                        SheetModel newSheet = UW.ClassroomPresenter.Model.Presentation.SheetModel.SheetDeepCopyHelper(s);
                        newSlide.AnnotationSheets.Add(newSheet);

                    }
                }
            }

            return newSlide;

        }



        

    
        #endregion

    }




}
