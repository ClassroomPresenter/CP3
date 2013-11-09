// $Id: DeckNetworkService.cs 897 2006-02-16 07:49:33Z pediddle $

using System;
using System.Collections;
using System.Diagnostics;
using System.Drawing;
using System.Threading;

using UW.ClassroomPresenter.Model;
using UW.ClassroomPresenter.Model.Presentation;

namespace UW.ClassroomPresenter.Network.Messages.Presentation {
    public class SSDeckNetworkService : IDisposable {
        private readonly SendingQueue m_Sender;
        private readonly EventQueue.PropertyEventDispatcher m_SlideRemovedDispatcher;
        private readonly EventQueue.PropertyEventDispatcher m_SlideAddedDispatcher;

        private readonly PresentationModel m_Presentation;
        private readonly DeckModel m_Deck;

        private readonly Hashtable m_SlideNetworkServices;

        private bool m_Disposed;

        public SSDeckNetworkService(SendingQueue sender, PresentationModel presentation, DeckModel deck) {
            this.m_Sender = sender;
            this.m_Presentation = presentation;
            this.m_Deck = deck;

            this.m_SlideRemovedDispatcher = new EventQueue.PropertyEventDispatcher(this.m_Sender, new PropertyEventHandler(this.HandleSlideRemoved));
            this.m_SlideAddedDispatcher = new EventQueue.PropertyEventDispatcher(this.m_Sender, new PropertyEventHandler(this.HandleSlideAdded));

            this.m_SlideNetworkServices = new Hashtable();

            // Lock the deck so no content can be added between registering the event listeners and calling SendAllSlidesAndContent().
            using( Synchronizer.Lock(this.m_Presentation.SyncRoot) ) {
                using(Synchronizer.Lock(deck.SyncRoot)) {
                    this.m_Deck.SlideRemoved += this.m_SlideRemovedDispatcher.Dispatcher;
                    this.m_Deck.SlideAdded += this.m_SlideAddedDispatcher.Dispatcher;

                    this.AttachAllSlides();
                }
            }
        }

        #region IDisposable Members

        ~SSDeckNetworkService() {
            this.Dispose(false);
        }

        public void Dispose() {
            this.Dispose(true);
            System.GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing) {
            if(this.m_Disposed) return;
            if(disposing) {
                // Unregister event listeners in reverse order than they were added.
                this.m_Deck.SlideAdded -= this.m_SlideAddedDispatcher.Dispatcher;
                this.m_Deck.SlideRemoved -= this.m_SlideRemovedDispatcher.Dispatcher;

                // Do this last, after the SlideAdded and SlideRemove handlers have been unregistered,
                // so that m_SlideNetworkServices cannot change during the iteration.
                foreach(SSSlideNetworkService service in this.m_SlideNetworkServices.Values)
                    service.Dispose();
            }
            this.m_Disposed = true;
        }

        #endregion

        public DeckModel Deck {
            get { return this.m_Deck; }
        }

        private void HandleSlideAdded(object sender, PropertyEventArgs args_) {
            PropertyCollectionEventArgs args = ((PropertyCollectionEventArgs) args_);
            SlideModel slide = ((SlideModel) args.Added);
            this.AttachSlide(slide);
        }

        private void HandleSlideRemoved(object sender, PropertyEventArgs args_) {
            PropertyCollectionEventArgs args = ((PropertyCollectionEventArgs) args_);
            SlideModel slide = ((SlideModel) args.Removed);
            using(Synchronizer.Lock(this.m_SlideNetworkServices.SyncRoot)) {
                SSSlideNetworkService service = ((SSSlideNetworkService) this.m_SlideNetworkServices[slide]);
                if(service != null) {
                    this.m_SlideNetworkServices.Remove(service);
                    service.Dispose();
                }
            }
        }

        private void AttachAllSlides() {
            using(Synchronizer.Lock(this.m_Deck.SyncRoot)) {
                foreach(SlideModel slide in this.m_Deck.Slides)
                    this.AttachSlide(slide);
            }
        }

        private void AttachSlide(SlideModel slide) {
            // Ensure that nothing about the slide can change between sending the slide's information
            // and creating the SlideNetworkService to listen for changes.
            // TODO: Find a way to also ensure that nothing about the SheetModels change either, but this is less of a concern.
            using(Synchronizer.Lock(slide.SyncRoot)) {
                using(Synchronizer.Lock(this.m_SlideNetworkServices.SyncRoot)) {
                    if(!this.m_SlideNetworkServices.ContainsKey(slide)) {
                        SSSlideNetworkService service = new SSSlideNetworkService(this.m_Sender, this.m_Presentation, this.m_Deck, slide);
                        this.m_SlideNetworkServices.Add(slide, service);
                    }
                }
            }
        }
    }
}
