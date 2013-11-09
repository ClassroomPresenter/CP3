#if WEBSERVER
using System;
using System.Collections.Generic;
using System.Text;

using UW.ClassroomPresenter.Model;
using UW.ClassroomPresenter.Model.Presentation;
using UW.ClassroomPresenter.Network.Messages;

using UW.ClassroomPresenter.Web.Model;

namespace UW.ClassroomPresenter.Web.Network {
    /// <summary>
    /// Web service for checking when there are changes to a deck traversal
    /// </summary>
    public class DeckTraversalWebService : IDisposable {
        #region Members

        /// <summary>
        /// The event queue
        /// </summary>
        private readonly SendingQueue m_Sender;
        /// <summary>
        /// Handler for when there are changes
        /// </summary>
        private readonly EventQueue.PropertyEventDispatcher m_CurrentChangedDispatcher;

        /// <summary>
        /// The presentation model
        /// </summary>
        private readonly PresentationModel m_Presentation;
        /// <summary>
        /// The deck traversal model
        /// </summary>
        private readonly DeckTraversalModel m_DeckTraversal;

        /// <summary>
        /// The web service for the deck associated with this traversal
        /// </summary>
        private readonly DeckWebService m_DeckWebService;

        /// <summary>
        /// True when disposed of
        /// </summary>
        private bool m_Disposed;

        #endregion

        #region Constructors

        /// <summary>
        /// Constructs a listener for changes to the deck traversal
        /// </summary>
        /// <param name="sender">The event queue for handling updates</param>
        /// <param name="presentation">The presentation</param>
        /// <param name="traversal">The deck traversal we care about </param>
        public DeckTraversalWebService(SendingQueue sender, PresentationModel presentation, DeckTraversalModel traversal)
        {
            this.m_Sender = sender;
            this.m_Presentation = presentation;
            this.m_DeckTraversal = traversal;

            // Create the deck object
            string deckName = "Untitled Deck";
            using (Synchronizer.Lock(this.m_DeckTraversal.SyncRoot))
            {
                using (Synchronizer.Lock(this.m_DeckTraversal.Deck))
                {
                    deckName = this.m_DeckTraversal.Deck.HumanName;
                }
            }
            SimpleWebDeck deck = new SimpleWebDeck();
            deck.Name = deckName;
            lock (WebService.Instance.GlobalModel) {
                WebService.Instance.GlobalModel.Decks.Add(deck);
            }
            WebService.Instance.UpdateModel();

            this.m_DeckWebService = new DeckWebService(this.m_Sender, this.m_Presentation, this.m_DeckTraversal.Deck);

            this.m_CurrentChangedDispatcher = new EventQueue.PropertyEventDispatcher(this.m_Sender, new PropertyEventHandler(this.HandleCurrentChanged));
            this.m_DeckTraversal.Changed["Current"].Add(this.m_CurrentChangedDispatcher.Dispatcher);
        }

        #endregion

        #region IDisposable Members

        /// <summary>
        /// Finalizer
        /// </summary>
        ~DeckTraversalWebService()
        {
            this.Dispose(false);
        }

        /// <summary>
        /// Public Dispose method
        /// </summary>
        public void Dispose()
        {
            this.Dispose(true);
            System.GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Protected Dispose method
        /// </summary>
        /// <param name="disposing">True when user initiated</param>
        protected virtual void Dispose(bool disposing)
        {
            if (this.m_Disposed) return;
            if (disposing)
            {
                this.m_DeckTraversal.Changed["Current"].Remove(this.m_CurrentChangedDispatcher.Dispatcher);
                this.m_DeckWebService.Dispose();
            }
            this.m_Disposed = true;
        }

        #endregion

        public DeckTraversalModel DeckTraversal
        {
            get { return this.m_DeckTraversal; }
        }

        private void HandleCurrentChanged(object sender, PropertyEventArgs args)
        {
            int slideIndex = -1;

            using( Synchronizer.Lock( this.m_DeckTraversal.SyncRoot ) ) {
                slideIndex = this.m_DeckTraversal.AbsoluteCurrentSlideIndex;
            }

            lock (WebService.Instance.GlobalModel) {
                WebService.Instance.GlobalModel.CurrentSlide = slideIndex;
            }
            WebService.Instance.UpdateModel();
        }
    }
}
#endif