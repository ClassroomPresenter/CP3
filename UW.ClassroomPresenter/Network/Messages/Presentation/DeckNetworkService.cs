// $Id: DeckNetworkService.cs 1766 2008-09-14 16:54:14Z lining $

using System;
using System.Collections;
using System.Diagnostics;
using System.Drawing;
using System.Threading;

using UW.ClassroomPresenter.Model;
using UW.ClassroomPresenter.Model.Presentation;
using UW.ClassroomPresenter.Network.Groups;

namespace UW.ClassroomPresenter.Network.Messages.Presentation {
    public class DeckNetworkService : INetworkService, IDisposable {
        private readonly SendingQueue m_Sender;
        private readonly EventQueue.PropertyEventDispatcher m_SlideRemovedDispatcher;
        private readonly EventQueue.PropertyEventDispatcher m_SlideAddedDispatcher;
        private readonly EventQueue.PropertyEventDispatcher m_SlideContentAddedDispatcher;
        private readonly EventQueue.PropertyEventDispatcher m_DeckBackgroundChangedDispatcher;

        private readonly PresentationModel m_Presentation;
        private readonly DeckModel m_Deck;

        private readonly Hashtable m_SlideNetworkServices;

        private readonly TableOfContentsNetworkService m_TableOfContentsNetworkService;

        private bool m_Disposed;

        public DeckNetworkService(SendingQueue sender, PresentationModel presentation, DeckModel deck) {
            this.m_Sender = sender;
            this.m_Presentation = presentation;
            this.m_Deck = deck;

            this.m_SlideRemovedDispatcher = new EventQueue.PropertyEventDispatcher(this.m_Sender, new PropertyEventHandler(this.HandleSlideRemoved));
            this.m_SlideAddedDispatcher = new EventQueue.PropertyEventDispatcher(this.m_Sender, new PropertyEventHandler(this.HandleSlideAdded));
            this.m_SlideContentAddedDispatcher = new EventQueue.PropertyEventDispatcher(this.m_Sender, new PropertyEventHandler(this.HandleSlideContentAdded));
            this.m_DeckBackgroundChangedDispatcher = new EventQueue.PropertyEventDispatcher(this.m_Sender, new PropertyEventHandler(this.HandleDeckBackgroundChanged));

            this.m_SlideNetworkServices = new Hashtable();

            this.m_TableOfContentsNetworkService = new TableOfContentsNetworkService(this.m_Sender, this.m_Presentation, this.m_Deck);

            // Lock the deck so no content can be added between registering the event listeners and calling SendAllSlidesAndContent().
            using( Synchronizer.Lock(this.m_Presentation.SyncRoot) ) {
                using(Synchronizer.Lock(deck.SyncRoot)) {

                    this.m_Deck.SlideRemoved += this.m_SlideRemovedDispatcher.Dispatcher;
                    this.m_Deck.SlideAdded += this.m_SlideAddedDispatcher.Dispatcher;
                    this.m_Deck.SlideContentAdded += this.m_SlideContentAddedDispatcher.Dispatcher;
                    this.m_Deck.Changed["DeckBackgroundColor"].Add(this.m_DeckBackgroundChangedDispatcher.Dispatcher);
                    this.m_Deck.Changed["DeckBackgroundTemplate"].Add(this.m_DeckBackgroundChangedDispatcher.Dispatcher);

                    this.SendAllSlidesAndContent(Group.AllParticipant);
                }
            }
        }

        #region IDisposable Members

        ~DeckNetworkService() {
            this.Dispose(false);
        }

        public void Dispose() {
            this.Dispose(true);
            System.GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing) {
            if(this.m_Disposed) return;
            if(disposing) {
                this.m_TableOfContentsNetworkService.Dispose();

                // Unregister event listeners in reverse order than they were added.
                this.m_Deck.Changed["DeckBackgroundColor"].Remove(this.m_DeckBackgroundChangedDispatcher.Dispatcher);
                this.m_Deck.Changed["DeckBackgroundTemplate"].Remove(this.m_DeckBackgroundChangedDispatcher.Dispatcher);
                this.m_Deck.SlideContentAdded -= this.m_SlideContentAddedDispatcher.Dispatcher;
                this.m_Deck.SlideAdded -= this.m_SlideAddedDispatcher.Dispatcher;
                this.m_Deck.SlideRemoved -= this.m_SlideRemovedDispatcher.Dispatcher;

                // Do this last, after the SlideAdded and SlideRemove handlers have been unregistered,
                // so that m_SlideNetworkServices cannot change during the iteration.
                foreach(SlideNetworkService service in this.m_SlideNetworkServices.Values)
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
            Group receivers = Group.AllParticipant;
            if( ( this.m_Deck.Disposition & (DeckDisposition.StudentSubmission | DeckDisposition.QuickPoll) ) != 0 ) {
                receivers = Group.Submissions;
            }
            this.SendSlideInformation(slide, receivers);
        }

        private void HandleSlideRemoved(object sender, PropertyEventArgs args_) {
            PropertyCollectionEventArgs args = ((PropertyCollectionEventArgs) args_);
            SlideModel slide = ((SlideModel) args.Removed);

            using(Synchronizer.Lock(this.m_SlideNetworkServices.SyncRoot)) {
                SlideNetworkService service = ((SlideNetworkService) this.m_SlideNetworkServices[slide]);
                if(service != null) {
                    this.m_SlideNetworkServices.Remove(service);
                    service.Dispose();
                }
            }

            Message message, deck;
            message = new PresentationInformationMessage(this.m_Presentation);
            message.InsertChild(deck = new DeckInformationMessage(this.m_Deck));
            deck.InsertChild(new SlideDeletedMessage(slide));
            this.m_Sender.Send(message);
        }

        private void HandleSlideContentAdded(object sender, PropertyEventArgs args_) {
            PropertyCollectionEventArgs args = ((PropertyCollectionEventArgs) args_);
            ByteArray hash = ((ByteArray) args.Added);

            // For the purposes of ForceUpdate, this information is sent by SendAllSlidesAndContent.

            Message message, deck;
            message = new PresentationInformationMessage(this.m_Presentation);
            message.InsertChild(deck = new DeckInformationMessage(this.m_Deck));
            deck.InsertChild(new DeckSlideContentMessage(this.m_Deck, hash));

            if( ( this.m_Deck.Disposition & (DeckDisposition.StudentSubmission | DeckDisposition.QuickPoll) ) != 0 ) {
                // If we get here in response to a new student submission received 
                // we don't need to send anything because we can assume the public display
                // already has the slide, however if the instructor manually adds new 
                // slides to a student submission deck, the public display may not have them.  
                // For now we send in both cases, but that could be improved.
                // In any case we only send slides from a Student Submission deck to Public nodes.
                message.Group = Group.Submissions; 
            }
            this.m_Sender.Send(message, MessagePriority.Low);
        }

        private void HandleDeckBackgroundChanged(object sender, PropertyEventArgs args_) {
            // No need to send this during ForceUpdate since the DeckTraversalNetworkService
            // will already have sent a DeckInformationMessage containing this information.

            Message message = new PresentationInformationMessage(this.m_Presentation);
            message.InsertChild(new DeckInformationMessage(this.m_Deck));
            this.m_Sender.Send(message);
        }

        private void SendAllSlidesAndContent(Group receivers) {
            using(Synchronizer.Lock(this.m_Deck.SyncRoot)) {

                foreach (SlideModel slide in this.m_Deck.Slides)
                    this.SendSlideInformation(slide, receivers);

                foreach(ByteArray hash in this.m_Deck.SlideContentKeys) {
                    Message message, deck;
                    message = new PresentationInformationMessage(this.m_Presentation);
                    message.Group = receivers;
                    message.InsertChild(deck = new DeckInformationMessage(this.m_Deck));
                    deck.InsertChild(new DeckSlideContentMessage(this.m_Deck, hash));

                    message.Tags = new MessageTags();
                    message.Tags.BridgePriority = MessagePriority.Lowest;
                    SlideModel slide = this.m_Deck.GetSlideFromContent(hash);
                    if (slide != null) {
                        using (Synchronizer.Lock(slide.SyncRoot)) {
                            message.Tags.SlideID = slide.Id;
                        }
                    }

                    this.m_Sender.Send(message, MessagePriority.Low);
                }
            }
        }

        private void SendSlideInformation(SlideModel slide, Group receivers) {
            Message message, deck;
            message = new PresentationInformationMessage(this.m_Presentation);
            message.Group = receivers;
            message.InsertChild(deck = new DeckInformationMessage(this.m_Deck));

            // Ensure that nothing about the slide can change between sending the slide's information
            // and creating the SlideNetworkService to listen for changes.
            // TODO: Find a way to also ensure that nothing about the SheetModels change either, but this is less of a concern.
            using (Synchronizer.Lock(this.m_SlideNetworkServices.SyncRoot)) {
                using (Synchronizer.Lock(slide.SyncRoot)) {
                    deck.InsertChild(new SlideInformationMessage(slide));

                    message.Tags = new MessageTags();
                    message.Tags.SlideID = slide.Id;

                    this.m_Sender.Send(message);

                    if (!this.m_SlideNetworkServices.ContainsKey(slide)) {
                        SlideNetworkService service = new SlideNetworkService(this.m_Sender, this.m_Presentation, this.m_Deck, slide);
                        this.m_SlideNetworkServices.Add(slide, service);
                    }
                }
            }
        }

        #region INetworkService Members

        public void ForceUpdate(Group receivers) {
            this.m_TableOfContentsNetworkService.ForceUpdate(receivers);

            this.SendAllSlidesAndContent(receivers);

            using( Synchronizer.Lock( this.m_Presentation.SyncRoot ) ) {
                using( Synchronizer.Lock( this.m_Deck.SyncRoot ) ) {
                    using( Synchronizer.Lock( this.m_SlideNetworkServices.SyncRoot ) ) {
                        foreach( SlideNetworkService service in this.m_SlideNetworkServices.Values ) {
                            service.ForceUpdate( receivers );
                        }
                    }
                }
            }
        }

        #endregion
    }
}
