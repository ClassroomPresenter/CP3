#if WEBSERVER
using System;
using System.Collections.Generic;
using System.Text;

using UW.ClassroomPresenter.Model.Presentation;
using UW.ClassroomPresenter.Network.Messages;
using UW.ClassroomPresenter.Network.Messages.Presentation;

namespace UW.ClassroomPresenter.Web.Network {
    /// <summary>
    /// This class is a generic class that is a listener for a sheet on a slide
    /// Currently, this class shouldn't do too much
    /// </summary>
    public class SheetWebService : IDisposable {
        #region Members

        /// <summary>
        /// The queue to be used for handling events
        /// </summary>
        private readonly SendingQueue m_Sender;
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
        /// The sheet model
        /// </summary>
        private readonly SheetModel m_Sheet;
        /// <summary>
        /// The collection of sheets that this belongs to, either annotation sheets or content sheets
        /// </summary>
        private readonly SheetMessage.SheetCollection m_Selector;
        /// <summary>
        /// Event handler for the bounds of the sheet changing
        /// </summary>        
//        private readonly EventQueue.PropertyEventDispatcher m_BoundsChangedDispatcher;

        /// <summary>
        /// Keep track of whether this object is disposed of or not
        /// </summary>
        private bool m_Disposed;

        #endregion

        #region Constructors

        /// <summary>
        /// Construct a listener for a generic sheet
        /// </summary>
        /// <param name="sender">The event queue</param>
        /// <param name="presentation">The presentation</param>
        /// <param name="deck">The deck</param>
        /// <param name="slide">The slide</param>
        /// <param name="sheet">The sheet</param>
        /// <param name="selector">The collection that this is part of</param>
        public SheetWebService(SendingQueue sender, PresentationModel presentation, DeckModel deck, SlideModel slide, SheetModel sheet, SheetMessage.SheetCollection selector) {
            this.m_Sender = sender;
            this.m_Presentation = presentation;
            this.m_Deck = deck;
            this.m_Slide = slide;
            this.m_Sheet = sheet;
            this.m_Selector = selector;

//            this.m_BoundsChangedDispatcher = new EventQueue.PropertyEventDispatcher(this.Sender, new PropertyEventHandler(this.HandleBoundsChanged));
//            this.m_Sheet.Changed["Bounds"].Add(this.m_BoundsChangedDispatcher.Dispatcher);
        }

        #endregion

        #region IDisposable Members

        /// <summary>
        /// Finalizer
        /// </summary>
        ~SheetWebService() {
            this.Dispose(false);
        }

        /// <summary>
        /// Public dispose method
        /// </summary>
        public void Dispose() {
            this.Dispose(true);
            System.GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Protected dispose method
        /// </summary>
        /// <param name="disposing">True when user initiated</param>
        protected virtual void Dispose(bool disposing) {
            if(this.m_Disposed) return;
            if(disposing) {
//                this.m_Sheet.Changed["Bounds"].Remove(this.m_BoundsChangedDispatcher.Dispatcher);
            }
            this.m_Disposed = true;
        }

        #endregion IDisposable Members

        #region Accessors

        /// <summary>
        /// The sending queue
        /// </summary>
        protected SendingQueue Sender {
            get { return this.m_Sender; }
        }

        /// <summary>
        /// The presentation model
        /// </summary>
        protected PresentationModel Presentation {
            get { return this.m_Presentation; }
        }

        /// <summary>
        /// The deck model
        /// </summary>
        protected DeckModel Deck {
            get { return this.m_Deck; }
        }

        /// <summary>
        /// The slide model
        /// </summary>
        protected SlideModel Slide {
            get { return this.m_Slide; }
        }

        /// <summary>
        /// The sheet model
        /// </summary>
        public SheetModel Sheet {
            get { return this.m_Sheet; }
        }

        /// <summary>
        /// The sheet collection type
        /// </summary>
        protected SheetMessage.SheetCollection SheetCollectionSelector {
            get { return this.m_Selector; }
        }

        #endregion

        #region Event Handlers
/*
        protected virtual void HandleBoundsChanged(object sender, PropertyEventArgs args) {
            // Send a generic update with information about the sheet (including the new bounds).
            Message message, deck, slide, sheet;
            message = new PresentationInformationMessage(this.Presentation);
            message.InsertChild(deck = new DeckInformationMessage(this.Deck));
            deck.InsertChild(slide = new SlideInformationMessage(this.Slide));
            slide.InsertChild( sheet = SheetMessage.RemoteForSheet( this.Sheet, this.SheetCollectionSelector ) );
            using (Synchronizer.Lock(this.m_Slide.SyncRoot)) {
                message.Tags = new MessageTags();
                message.Tags.SlideID = this.m_Slide.Id;
            }
            this.Sender.Send(message);
        }
*/
        #endregion

        #region SheetWebService Factory

        /// <summary>
        /// Construct the appropriate sheet web service
        /// </summary>
        /// <param name="sender">The queue</param>
        /// <param name="presentation">The presentation</param>
        /// <param name="deck">The deck</param>
        /// <param name="slide">The slide</param>
        /// <param name="sheet">The sheet</param>
        /// <param name="selector">The sheet collection type</param>
        /// <returns>A sheet web service appropriate for the sheet model</returns>
        public static SheetWebService ForSheet(SendingQueue sender, PresentationModel presentation, DeckModel deck, SlideModel slide, SheetModel sheet, SheetMessage.SheetCollection selector) {
            if (sheet is InkSheetModel) {
                return new InkSheetWebService( sender, presentation, deck, slide, sheet, selector );
            } else {
                return new SheetWebService( sender, presentation, deck, slide, sheet, selector );
            }
        }

        #endregion
    }
}
#endif