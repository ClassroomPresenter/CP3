#if WEBSERVER
using System;
using System.Collections;
using System.Text;

using UW.ClassroomPresenter.Model;
using UW.ClassroomPresenter.Model.Presentation;
using UW.ClassroomPresenter.Network.Messages;

using UW.ClassroomPresenter.Web.Model;

namespace UW.ClassroomPresenter.Web.Network {
    /// <summary>
    /// Web service for keeping track of a deck
    /// </summary>
    public class DeckWebService : IDisposable {
        #region Members

        /// <summary>
        /// Event queue for dealing with messages
        /// </summary>
        private readonly SendingQueue m_Sender;
        /// <summary>
        /// Event handler for the slide removed
        /// </summary>
        private readonly EventQueue.PropertyEventDispatcher m_SlideRemovedDispatcher;
        /// <summary>
        /// Event handler for a slide added
        /// </summary>
        private readonly EventQueue.PropertyEventDispatcher m_SlideAddedDispatcher;
        /// <summary>
        /// Event handler for slide content changing
        /// </summary>
//        private readonly EventQueue.PropertyEventDispatcher m_SlideContentAddedDispatcher;
        /// <summary>
        /// Event handler for deck background changed
        /// </summary>
//        private readonly EventQueue.PropertyEventDispatcher m_DeckBackgroundChangedDispatcher;

        /// <summary>
        /// The presentation model
        /// </summary>
        private readonly PresentationModel m_Presentation;
        /// <summary>
        /// The deck model
        /// </summary>
        private readonly DeckModel m_Deck;

        /// <summary>
        /// Container for slide web services
        /// </summary>
        private readonly Hashtable m_SlideWebServices;

        /// <summary>
        /// The table of contents model web service
        /// </summary>
//        private readonly TableOfContentsWebService m_TableOfContentsWebService;

        /// <summary>
        /// Whether the object is disposed
        /// </summary>
        private bool m_Disposed;

        #endregion

        #region Constructor

        /// <summary>
        /// Construct a new deck web service
        /// </summary>
        /// <param name="sender">The event handler </param>
        /// <param name="presentation">The presentation model</param>
        /// <param name="deck">The deck model</param>
        public DeckWebService(SendingQueue sender, PresentationModel presentation, DeckModel deck)
        {
            this.m_Sender = sender;
            this.m_Presentation = presentation;
            this.m_Deck = deck;

            this.m_SlideRemovedDispatcher = new EventQueue.PropertyEventDispatcher(this.m_Sender, new PropertyEventHandler(this.HandleSlideRemoved));
            this.m_SlideAddedDispatcher = new EventQueue.PropertyEventDispatcher(this.m_Sender, new PropertyEventHandler(this.HandleSlideAdded));
//            this.m_SlideContentAddedDispatcher = new EventQueue.PropertyEventDispatcher(this.m_Sender, new PropertyEventHandler(this.HandleSlideContentAdded));
//            this.m_DeckBackgroundChangedDispatcher = new EventQueue.PropertyEventDispatcher(this.m_Sender, new PropertyEventHandler(this.HandleDeckBackgroundChanged));

            this.m_SlideWebServices = new Hashtable();

//            this.m_TableOfContentsWebService = new TableOfContentsWebService(this.m_Sender, this.m_Presentation, this.m_Deck);

            // Lock the deck so no content can be added between registering the event listeners and calling SendAllSlidesAndContent().
            using (Synchronizer.Lock(this.m_Presentation.SyncRoot))
            {
                using (Synchronizer.Lock(deck.SyncRoot))
                {

                    this.m_Deck.SlideRemoved += this.m_SlideRemovedDispatcher.Dispatcher;
                    this.m_Deck.SlideAdded += this.m_SlideAddedDispatcher.Dispatcher;
//                    this.m_Deck.SlideContentAdded += this.m_SlideContentAddedDispatcher.Dispatcher;
//                    this.m_Deck.Changed["DeckBackgroundColor"].Add(this.m_DeckBackgroundChangedDispatcher.Dispatcher);
//                    this.m_Deck.Changed["DeckBackgroundTemplate"].Add(this.m_DeckBackgroundChangedDispatcher.Dispatcher);
                }
            }

            this.UpdateAllSlidesAndContent();
        }

        protected void UpdateAllSlidesAndContent()
        {
            int deckIndex = GetDeckIndex();

            using (Synchronizer.Lock(this.m_Deck.SyncRoot))
            {
                using( Synchronizer.Lock( this.m_Deck.TableOfContents.SyncRoot ) ) {
                    foreach (TableOfContentsModel.Entry e in this.m_Deck.TableOfContents.Entries)
                    {
                        // Get the index of this entry
                        int index = this.m_Deck.TableOfContents.Entries.IndexOf( e );
                        using (Synchronizer.Lock(e.SyncRoot))
                        {
                            using( Synchronizer.Lock( e.Slide.SyncRoot ) ) {
                                if (!this.m_SlideWebServices.ContainsKey(e.Slide.Id))
                                {
                                    SlideWebService service = new SlideWebService(this.m_Sender, this.m_Presentation, this.m_Deck, e.Slide);
                                    this.m_SlideWebServices.Add(e.Slide.Id, service);
                                }

                                // Check if the slide exists yet
                                lock (WebService.Instance.GlobalModel) {
                                    SimpleWebSlide s = GetExistingWebSlide((SimpleWebDeck)WebService.Instance.GlobalModel.Decks[deckIndex], e.Slide.Id);
                                    if (s == null) {
                                        // Add the new slide
                                        s = new SimpleWebSlide();
                                        s.Id = e.Slide.Id;
                                        s.Index = index;
                                        s.Name = e.Slide.Title;
                                        ((SimpleWebDeck)WebService.Instance.GlobalModel.Decks[deckIndex]).Slides.Add(s);
                                    } else {
                                        // Update the slide values
                                        s.Index = index;
                                        s.Name = e.Slide.Title;
                                    }
                                }
                                WebService.Instance.UpdateModel();
                            }
                        }
                    }
                }
            }
        }

        protected SimpleWebSlide GetExistingWebSlide( SimpleWebDeck d, Guid id) {
            foreach( SimpleWebSlide s in d.Slides ) {
                if (s.Id == id)
                    return s;
            }
            return null;
        }

        protected int GetDeckIndex()
        {
            int index = -1;
            using (Synchronizer.Lock(this.m_Presentation.SyncRoot))
            {
                foreach( DeckTraversalModel t in this.m_Presentation.DeckTraversals )
                {
                    using (Synchronizer.Lock(t.SyncRoot))
                    {
                        if (t.Deck == this.m_Deck)
                        {
                            index = this.m_Presentation.DeckTraversals.IndexOf(t);
                        }
                    }
                }
            }
            return index;
        }
        #endregion

        #region IDisposable Members

        /// <summary>
        /// Finalizer
        /// </summary>
        ~DeckWebService()
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
//                this.m_TableOfContentsWebService.Dispose();

                // Unregister event listeners in reverse order than they were added.
//                this.m_Deck.Changed["DeckBackgroundColor"].Remove(this.m_DeckBackgroundChangedDispatcher.Dispatcher);
//                this.m_Deck.Changed["DeckBackgroundTemplate"].Remove(this.m_DeckBackgroundChangedDispatcher.Dispatcher);
//                this.m_Deck.SlideContentAdded -= this.m_SlideContentAddedDispatcher.Dispatcher;
                this.m_Deck.SlideAdded -= this.m_SlideAddedDispatcher.Dispatcher;
                this.m_Deck.SlideRemoved -= this.m_SlideRemovedDispatcher.Dispatcher;

                // Do this last, after the SlideAdded and SlideRemove handlers have been unregistered,
                // so that m_SlideNetworkServices cannot change during the iteration.
//                foreach (SlideWebService service in this.m_SlideWebServices.Values)
//                    service.Dispose();
            }
            this.m_Disposed = true;
        }

        #endregion

        #region Accessors

        /// <summary>
        /// The public deck model
        /// </summary>
        public DeckModel Deck
        {
            get { return this.m_Deck; }
        }

        #endregion

        #region Event Handlers

        /// <summary>
        /// Handle the slide being added
        /// </summary>
        /// <param name="sender">The object that sent the event</param>
        /// <param name="args_">The arguments</param>
        private void HandleSlideAdded(object sender, PropertyEventArgs args_)
        {

/*
            PropertyCollectionEventArgs args = ((PropertyCollectionEventArgs)args_);
            SlideModel slide = ((SlideModel)args.Added);
            Group receivers = Group.AllParticipant;
            if ((this.m_Deck.Disposition & (DeckDisposition.StudentSubmission | DeckDisposition.QuickPoll)) != 0)
            {
                receivers = Group.Submissions;
            }
            this.SendSlideInformation(slide, receivers);
*/
            this.UpdateAllSlidesAndContent();
        }

        /// <summary>
        /// Handle a slide being removed
        /// </summary>
        /// <param name="sender">The object that sent the event</param>
        /// <param name="args_">The event arguments</param>
        private void HandleSlideRemoved(object sender, PropertyEventArgs args_)
        {
/*
            PropertyCollectionEventArgs args = ((PropertyCollectionEventArgs)args_);
            SlideModel slide = ((SlideModel)args.Removed);

            using (Synchronizer.Lock(this.m_SlideNetworkServices.SyncRoot))
            {
                SlideNetworkService service = ((SlideNetworkService)this.m_SlideNetworkServices[slide]);
                if (service != null)
                {
                    this.m_SlideNetworkServices.Remove(service);
                    service.Dispose();
                }
            }

            Message message, deck;
            message = new PresentationInformationMessage(this.m_Presentation);
            message.InsertChild(deck = new DeckInformationMessage(this.m_Deck));
            deck.InsertChild(new SlideDeletedMessage(slide));
            this.m_Sender.Send(message);
*/
            this.UpdateAllSlidesAndContent();
        }

        /// <summary>
        /// Handle the slide content changing
        /// </summary>
        /// <param name="sender">The object that sent the event</param>
        /// <param name="args_">The event arguments</param>
        private void HandleSlideContentAdded(object sender, PropertyEventArgs args_)
        {
/*
            PropertyCollectionEventArgs args = ((PropertyCollectionEventArgs)args_);
            ByteArray hash = ((ByteArray)args.Added);

            // For the purposes of ForceUpdate, this information is sent by SendAllSlidesAndContent.

            Message message, deck;
            message = new PresentationInformationMessage(this.m_Presentation);
            message.InsertChild(deck = new DeckInformationMessage(this.m_Deck));
            deck.InsertChild(new DeckSlideContentMessage(this.m_Deck, hash));

            if ((this.m_Deck.Disposition & (DeckDisposition.StudentSubmission | DeckDisposition.QuickPoll)) != 0)
            {
                // If we get here in response to a new student submission received 
                // we don't need to send anything because we can assume the public display
                // already has the slide, however if the instructor manually adds new 
                // slides to a student submission deck, the public display may not have them.  
                // For now we send in both cases, but that could be improved.
                // In any case we only send slides from a Student Submission deck to Public nodes.
                message.Group = Group.Submissions;
            }
            this.m_Sender.Send(message, MessagePriority.Low);
*/
        }

        /// <summary>
        /// Handle the deck background changing
        /// </summary>
        /// <param name="sender">The object that sent the event</param>
        /// <param name="args_">The event arguments</param>
        private void HandleDeckBackgroundChanged(object sender, PropertyEventArgs args_)
        {
/*
            // No need to send this during ForceUpdate since the DeckTraversalNetworkService
            // will already have sent a DeckInformationMessage containing this information.

            Message message = new PresentationInformationMessage(this.m_Presentation);
            message.InsertChild(new DeckInformationMessage(this.m_Deck));
            this.m_Sender.Send(message);
*/
        }

        #endregion

        #region Updaters
/*
        private void SendSlideInformation(SlideModel slide, Group receivers)
        {
            Message message, deck;
            message = new PresentationInformationMessage(this.m_Presentation);
            message.Group = receivers;
            message.InsertChild(deck = new DeckInformationMessage(this.m_Deck));

            // Ensure that nothing about the slide can change between sending the slide's information
            // and creating the SlideNetworkService to listen for changes.
            // TODO: Find a way to also ensure that nothing about the SheetModels change either, but this is less of a concern.
            using (Synchronizer.Lock(this.m_SlideNetworkServices.SyncRoot))
            {
                using (Synchronizer.Lock(slide.SyncRoot))
                {
                    deck.InsertChild(new SlideInformationMessage(slide));

                    message.Tags = new MessageTags();
                    message.Tags.SlideID = slide.Id;

                    this.m_Sender.Send(message);

                    if (!this.m_SlideNetworkServices.ContainsKey(slide))
                    {
                        SlideNetworkService service = new SlideNetworkService(this.m_Sender, this.m_Presentation, this.m_Deck, slide);
                        this.m_SlideNetworkServices.Add(slide, service);
                    }
                }
            }
        }
*/
        #endregion
    }
}
#endif