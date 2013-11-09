#if WEBSERVER
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

using UW.ClassroomPresenter.Model;
using UW.ClassroomPresenter.Model.Presentation;
using UW.ClassroomPresenter.Network.Groups;
using UW.ClassroomPresenter.Network.Messages;
using UW.ClassroomPresenter.Web.Model;

namespace UW.ClassroomPresenter.Web.Network {
    /// <summary>
    /// Handles listening for changes to the presentation and marshals them to the web service
    /// </summary>
    public class PresentationWebService : IDisposable {
        #region Members
        /// <summary>
        /// Queue for handling messages
        /// </summary>
        private readonly SendingQueue m_Sender;
        /// <summary>
        /// The model that we are listening to changes to
        /// </summary>
        private readonly PresentationModel m_Presentation;
        /// <summary>
        /// Helper collection to handle adding and removing deck collections to the presentation
        /// </summary>
        private readonly DeckTraversalsCollectionHelper m_DeckTraversalsCollectionHelper;

        private WebService.SSEventHandler handler = null;
        private WebService.QPEventHandler qpHandler = null;

        /// <summary>
        /// Helper for when the quick poll changes
        /// </summary>
        private readonly EventQueue.PropertyEventDispatcher m_QuickPollChangedDispatcher;
        /// <summary>
        /// Network service for the quick poll
        /// </summary>
//        private QuickPollWebService m_QuickPollWebService;

        /// <summary>
        /// True then this service is disposed, false otherwise.
        /// </summary>
        private bool m_Disposed;

        #endregion

        #region Constructors

        /// <summary>
        /// Build the network service
        /// </summary>
        /// <param name="sender">The message queue to use for these messages</param>
        /// <param name="presentation">The presentation to listen to changes to</param>
        public PresentationWebService( SendingQueue sender, PresentationModel presentation )
        {
            handler = new WebService.SSEventHandler(HandleStudentSubmission);
            WebService.Instance.SubmissionReceived += handler;
            qpHandler = new WebService.QPEventHandler(HandleQuickPollReceived);
            WebService.Instance.QuickPollReceived += qpHandler;

            this.m_Sender = sender;
            this.m_Presentation = presentation;

            this.m_DeckTraversalsCollectionHelper = new DeckTraversalsCollectionHelper(this);

//            this.m_QuickPollWebService = new QuickPollWebService(this.m_Sender, this.m_Presentation);

            this.m_QuickPollChangedDispatcher = new EventQueue.PropertyEventDispatcher(this.m_Sender, new PropertyEventHandler(this.HandleQuickPollChanged));
            this.m_Presentation.Changed["QuickPoll"].Add(this.m_QuickPollChangedDispatcher.Dispatcher);
        }

        #endregion

        #region IDisposable Members

        /// <summary>
        /// Finalizer for the service
        /// </summary>
        ~PresentationWebService()
        {
            this.Dispose(false);
        }

        /// <summary>
        /// Dispose method to destroy all resources
        /// </summary>
        public void Dispose()
        {
            this.Dispose(true);
            System.GC.SuppressFinalize(this);
        }

        /// <summary>
        /// The inner dispose method for cleaning up resources
        /// </summary>
        /// <param name="disposing">True when user initiated</param>
        protected virtual void Dispose(bool disposing)
        {
            if (this.m_Disposed) return;
            if (disposing)
            {
                WebService.Instance.SubmissionReceived -= handler;
                WebService.Instance.QuickPollReceived -= qpHandler;
                this.m_DeckTraversalsCollectionHelper.Dispose();
                this.m_Presentation.Changed["QuickPoll"].Remove(this.m_QuickPollChangedDispatcher.Dispatcher);
//                this.m_QuickPollWebService.Dispose();
            }
            this.m_Disposed = true;
        }

        #endregion

        #region Event Handlers

        /// <summary>
        /// Handle the quick poll model changing
        /// </summary>
        /// <param name="sender">The object sending this event</param>
        /// <param name="args">The arguments for this event</param>
        private void HandleQuickPollChanged(object sender, PropertyEventArgs args)
        {
            QuickPollModel.QuickPollStyle pollStyle = QuickPollModel.QuickPollStyle.ABCD;

            if (this.m_Presentation != null)
            {
                using (Synchronizer.Lock(this.m_Presentation.SyncRoot))
                {
                    if (this.m_Presentation.QuickPoll != null)
                    {
                        using (Synchronizer.Lock(this.m_Presentation.QuickPoll.SyncRoot))
                        {
                            pollStyle = this.m_Presentation.QuickPoll.PollStyle;
                        }
                    }
                }
            }

            lock (WebService.Instance.GlobalModel) {
                WebService.Instance.GlobalModel.PollStyle = (int)pollStyle;
            }
            WebService.Instance.UpdateModel();
        }

        #endregion

        #region Student Submissions

        /// <summary>
        /// 
        /// </summary>
        /// <param name="server"></param>
        /// <param name="deckIndex"></param>
        /// <param name="slideIndex"></param>
        /// <param name="strokes">SimpleWebInk objects that make up the strokes.</param>
        public void HandleStudentSubmission( object server, int deckIndex, int slideIndex, ArrayList strokes ) {
            SlideModel slide = null;
            DeckModel deck = null;
            using (Synchronizer.Lock(this.m_Presentation.SyncRoot))
            {
                using (Synchronizer.Lock(this.m_Presentation.DeckTraversals[deckIndex].SyncRoot))
                {
                    using( Synchronizer.Lock(this.m_Presentation.DeckTraversals[deckIndex].Current.SyncRoot) ) {
                        // Get the slide model
                        slide = this.m_Presentation.DeckTraversals[deckIndex].Current.Slide;
                    }

                    deck = this.m_Presentation.DeckTraversals[deckIndex].Deck;
                }
            }

            // Get the student submissions deck or create it if it doesn't exist
            DeckModel ssDeck = this.m_Presentation.GetStudentSubmissionDeck();
            if (ssDeck == null) {
                // Create the student submissions deck
                Guid ssGuid = new Guid("{78696D29-AA11-4c5b-BCF8-8E6406077FD4}");
                Guid ssTraversalGuid = new Guid("{4884044B-DAE1-4249-AEF2-3A2304F52E97}");
                ssDeck = new DeckModel(ssGuid, DeckDisposition.StudentSubmission, "Student Submissions");
                ssDeck.Group = Group.Submissions;
                ssDeck.current_subs = true;
//                AddLocalRef(ssGuid, ssDeck);
                DeckTraversalModel traversal = new SlideDeckTraversalModel(ssTraversalGuid, ssDeck);
//                AddLocalRef(ssTraversalGuid, traversal);

                // Add the new student submission deck to the presentation
                using (Synchronizer.Lock(this.m_Presentation.SyncRoot)) {
                   this.m_Presentation.DeckTraversals.Add(traversal);
                }
            }

            // Create the new slide to add
            SlideModel newSlide = new SlideModel( new Guid(), new LocalId(), SlideDisposition.Remote | SlideDisposition.StudentSubmission );

            // Make a list of image content sheets that need to be added to the deck.
            List<ImageSheetModel> images = new List<ImageSheetModel>();

            // Update the fields of the slide
            using (Synchronizer.Lock(newSlide.SyncRoot))
            {
                using (Synchronizer.Lock(slide.SyncRoot))
                {
                    newSlide.Title = slide.Title;
                    newSlide.Bounds = slide.Bounds;
                    newSlide.Zoom = slide.Zoom;
                    newSlide.BackgroundColor = slide.BackgroundColor;
                    newSlide.BackgroundTemplate = slide.BackgroundTemplate;
                    newSlide.SubmissionSlideGuid = slide.SubmissionSlideGuid;
                    newSlide.SubmissionStyle = slide.SubmissionStyle;

                    //If the slide background is null, then update the slide background with deck setting
                    using (Synchronizer.Lock(deck.SyncRoot))
                    {
                        if (slide.BackgroundColor == System.Drawing.Color.Empty)
                        {
                            newSlide.BackgroundColor = deck.DeckBackgroundColor;
                        }
                        if (slide.BackgroundTemplate == null)
                        {
                            newSlide.BackgroundTemplate = deck.DeckBackgroundTemplate;
                        }
                    }

                    // Copy all of the content sheets.
                    // Because ContentSheets do not change, there is no 
                    // need to do a deep copy (special case for ImageSheetModels).
                    foreach (SheetModel s in slide.ContentSheets)
                    {
                        newSlide.ContentSheets.Add(s);

                        // Queue up any image content to be added the deck below.
                        ImageSheetModel ism = s as ImageSheetModel;
                        if (ism != null)
                            images.Add(ism);
                    }

                    // Make a deep copy of all the ink sheets
                    foreach (SheetModel s in slide.AnnotationSheets)
                    {
                        SheetModel newSheet = UW.ClassroomPresenter.Model.Presentation.SheetModel.SheetDeepRemoteCopyHelper(s);
                        newSlide.AnnotationSheets.Add(newSheet);

                        // Queue up any image content to be added the deck below.
                        ImageSheetModel ism = s as ImageSheetModel;
                        if (ism != null)
                            images.Add(ism);
                    }
                }
            }
            
            // Add the slide content to the deck.
            using (Synchronizer.Lock(ssDeck.SyncRoot)) {
                foreach (ImageSheetModel ism in images) {
                    System.Drawing.Image image = ism.Image;
                    if (image == null)
                        using (Synchronizer.Lock(ism.Deck.SyncRoot))
                        using (Synchronizer.Lock(ism.SyncRoot))
                            image = ism.Deck.GetSlideContent(ism.MD5);
                    if (image != null)
                        ssDeck.AddSlideContent(ism.MD5, image);
                }

                // Add the slide to the deck.
                ssDeck.InsertSlide(newSlide);
            }

            // Add an entry to the deck traversal so that we can navigate to the slide
            using (Synchronizer.Lock(ssDeck.TableOfContents.SyncRoot)) {
                TableOfContentsModel.Entry e = new TableOfContentsModel.Entry( new Guid(), ssDeck.TableOfContents, newSlide);
                ssDeck.TableOfContents.Entries.Add(e);
            }


            // Add the ink to the slide now
            using (Synchronizer.Lock(newSlide.SyncRoot))
            {
                RealTimeInkSheetModel sheet = new RealTimeInkSheetModel(new Guid(), SheetDisposition.All | SheetDisposition.Remote, newSlide.Bounds);

                // Add the sheet
                newSlide.AnnotationSheets.Add(sheet);

                // Now add the ink
                using (Synchronizer.Lock(sheet.Ink.Strokes.SyncRoot))
                {
                    foreach (SimpleWebInk stroke in strokes) {
                        Microsoft.Ink.Stroke s = sheet.Ink.CreateStroke(stroke.Pts);
                        s.DrawingAttributes.Color =
                            System.Drawing.Color.FromArgb(stroke.R, stroke.G, stroke.B);
                        s.DrawingAttributes.RasterOperation =
                            (stroke.Opacity < 255) ?
                            Microsoft.Ink.RasterOperation.MaskPen :
                            Microsoft.Ink.RasterOperation.CopyPen;
                        s.DrawingAttributes.Width = stroke.Width * 30.00f;
                    }
                }
            }
        }

        #endregion

        #region QuickPoll

        public void HandleQuickPollReceived( object server, Guid ownerId, string val )
        {
            QuickPollResultModel result = new QuickPollResultModel( ownerId, val );
            this.m_Presentation.QuickPoll.AddResult( result );
        }

        #endregion

        #region DeckTraversalsCollectionHelper

        /// <summary>
        /// Helper class that handles when decks are added or removed from the presentation
        /// </summary>
        private class DeckTraversalsCollectionHelper : PropertyCollectionHelper
        {
            private readonly PresentationWebService m_Service;

            public DeckTraversalsCollectionHelper(PresentationWebService service)
                : base(service.m_Sender, service.m_Presentation, "DeckTraversals")
            {
                this.m_Service = service;
                base.Initialize();
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                    foreach (IDisposable disposable in this.Tags)
                        if (disposable != null)
                            disposable.Dispose();
                base.Dispose(disposing);
            }

            protected override object SetUpMember(int index, object member)
            {
                DeckTraversalModel traversal = ((DeckTraversalModel)member);

                // Don't need a service for remote decks
                if ((traversal.Deck.Disposition & DeckDisposition.Remote) != 0)
                    return null;

                return new DeckTraversalWebService( this.m_Service.m_Sender, this.m_Service.m_Presentation, traversal );
            }

            protected override void TearDownMember(int index, object member, object tag)
            {
                DeckTraversalWebService service = ((DeckTraversalWebService)tag);

                if (service != null)
                {
                    // Dispose of the service
                    DeckTraversalModel traversal = service.DeckTraversal;
                    service.Dispose();
                }
            }
        }

        #endregion
    }
}
#endif