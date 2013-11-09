#if WEBSERVER
using System;
using System.Collections;
using System.Text;

using UW.ClassroomPresenter.Model;
using UW.ClassroomPresenter.Model.Presentation;
using UW.ClassroomPresenter.Network.Groups;
using UW.ClassroomPresenter.Network.Messages;
using UW.ClassroomPresenter.Network.Messages.Presentation;

namespace UW.ClassroomPresenter.Web.Network {
    /// <summary>
    /// A class that is for keeping track of changes to slides
    /// </summary>
    public class SlideWebService : IDisposable {
        #region Members

        /// <summary>
        /// The queue for handling events
        /// </summary>
        private readonly SendingQueue m_Sender;
        /// <summary>
        /// Event handler for a field changing
        /// </summary>
        private readonly EventQueue.PropertyEventDispatcher m_ChangeDispatcher;
        /// <summary>
        /// The presentation model
        /// </summary>
        private readonly PresentationModel m_Presentation;
        /// <summary>
        /// The deck model
        /// </summary>
        private readonly DeckModel m_Deck;
        /// <summary>
        /// The slide model
        /// </summary>
        private readonly SlideModel m_Slide;

        /// <summary>
        /// Keeps track of the content sheets
        /// </summary>
        private readonly SheetsCollectionHelper m_ContentSheetsCollectionHelper;
        /// <summary>
        /// Keeps track of the annotation sheets
        /// </summary>
        private readonly SheetsCollectionHelper m_AnnotationSheetsCollectionHelper;

        /// <summary>
        /// Keeps track of whether this is disposed of
        /// </summary>
        private bool m_Disposed;

        #endregion

        #region Constructors

        /// <summary>
        /// Construct the service
        /// </summary>
        /// <param name="sender">The event queue</param>
        /// <param name="presentation">The presentation</param>
        /// <param name="deck">The deck</param>
        /// <param name="slide">The slide</param>
        public SlideWebService(SendingQueue sender, PresentationModel presentation, DeckModel deck, SlideModel slide) {
            this.m_Sender = sender;
            this.m_Presentation = presentation;
            this.m_Deck = deck;
            this.m_Slide = slide;

            this.m_ChangeDispatcher = new EventQueue.PropertyEventDispatcher(this.m_Sender, new PropertyEventHandler(this.HandleChange));
            this.m_Slide.Changed["Bounds"].Add(this.m_ChangeDispatcher.Dispatcher);
            this.m_Slide.Changed["Zoom"].Add(this.m_ChangeDispatcher.Dispatcher);
            this.m_Slide.Changed["BackgroundColor"].Add(this.m_ChangeDispatcher.Dispatcher);
            this.m_Slide.Changed["BackgroundTemplate"].Add(this.m_ChangeDispatcher.Dispatcher);

            this.m_ContentSheetsCollectionHelper = new SheetsCollectionHelper(this, "ContentSheets", SheetMessage.SheetCollection.ContentSheets);
            this.m_AnnotationSheetsCollectionHelper = new SheetsCollectionHelper(this, "AnnotationSheets", SheetMessage.SheetCollection.AnnotationSheets);
        }

        #endregion

        #region IDisposable Members

        /// <summary>
        /// Finalizer
        /// </summary>
        ~SlideWebService()
        {
            this.Dispose(false);
        }

        /// <summary>
        /// Public dispose method
        /// </summary>
        public void Dispose()
        {
            this.Dispose(true);
            System.GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Protected dispose method
        /// </summary>
        /// <param name="disposing">True when user initiated</param>
        protected virtual void Dispose(bool disposing)
        {
            if (this.m_Disposed) return;
            if (disposing)
            {
                this.m_Slide.Changed["Bounds"].Remove(this.m_ChangeDispatcher.Dispatcher);
                this.m_Slide.Changed["Zoom"].Remove(this.m_ChangeDispatcher.Dispatcher);
                this.m_Slide.Changed["BackgroundColor"].Remove(this.m_ChangeDispatcher.Dispatcher);
                this.m_Slide.Changed["BackgroundTemplate"].Remove(this.m_ChangeDispatcher.Dispatcher);

                this.m_ContentSheetsCollectionHelper.Dispose();
                this.m_AnnotationSheetsCollectionHelper.Dispose();
            }
            this.m_Disposed = true;
        }

        #endregion IDisposable Members

        #region Handle Changes

        private void HandleChange(object sender, PropertyEventArgs args) {
            this.SendGenericChange(Group.AllParticipant);
        }

        private void SendGenericChange(Group receivers) {
        }

        public void SendSheetInformation(SheetModel sheet, SheetMessage.SheetCollection selector, Group receivers)
        {
/*
            // TODO: Also pass the index so the sheet can be inserted into the correct index on the remote side.
            Message message, deck, slide;
            message = new PresentationInformationMessage(this.m_Presentation);
            message.Group = receivers;
            message.InsertChild(deck = new DeckInformationMessage(this.m_Deck));
            deck.InsertChild(slide = new SlideInformationMessage(this.m_Slide));
            Message sm = SheetMessage.RemoteForSheet(sheet, selector);
            //if sm is null that means that the sheet is an instructor note and shouldn't be sent.
            if (sm != null)
            {
                slide.InsertChild(sm);
            }
            using (Synchronizer.Lock(this.m_Slide.SyncRoot))
            {
                message.Tags = new MessageTags();
                message.Tags.SlideID = this.m_Slide.Id;
            }
            if (sheet is ImageSheetModel && ((ImageSheetModel)sheet).Image != null)
            {
                using (Synchronizer.Lock(sheet.SyncRoot))
                {
                    this.m_Sender.Send(message);
                }
            }
            else
                this.m_Sender.Send(message);
*/
        }

        #endregion

        #region SheetsCollection

        private class SheetsCollectionHelper : PropertyCollectionHelper {
            private readonly SlideWebService m_Service;
            private readonly SheetMessage.SheetCollection m_Selector;

            public SheetsCollectionHelper(SlideWebService service, string property, SheetMessage.SheetCollection selector)
                : base(service.m_Sender, service.m_Slide, property)
            {
                this.m_Service = service;
                this.m_Selector = selector;

                base.Initialize();
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                    foreach (IDisposable disposable in this.Tags)
                        // The entry might be null if SheetNetworkService.ForSheet returned null.
                        if (disposable != null)
                            disposable.Dispose();
                base.Dispose(disposing);
            }

            protected override object SetUpMember(int index, object member)
            {
                SheetModel sheet = ((SheetModel)member);

                using (Synchronizer.Lock(sheet.SyncRoot))
                {
                    // Don't create a network service for the sheet if it is remote,
                    // UNLESS this is a student submission slide in which case we're responsible for rebroadcasting its ink.
                    if ((sheet.Disposition & SheetDisposition.Remote) != 0 && (this.m_Service.m_Slide.Disposition & SlideDisposition.StudentSubmission) == 0)
                        return null;
                }

//                this.m_Service.SendSheetInformation(sheet, this.m_Selector, receivers);
                SheetWebService service = SheetWebService.ForSheet( this.m_Service.m_Sender, this.m_Service.m_Presentation, this.m_Service.m_Deck, this.m_Service.m_Slide, sheet, this.m_Selector);
//                return service;
                return null;
            }

            protected override void TearDownMember(int index, object member, object tag)
            {
                if (tag == null) return;

                SheetModel sheet = ((SheetModel)member);
                ((SheetNetworkService)tag).Dispose();
            }
        }

        #endregion
    }
}
#endif