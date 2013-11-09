// $Id: PresentationNetworkService.cs 1617 2008-06-12 23:28:28Z cmprince $

using System;
using System.Diagnostics;
using System.Threading;

using UW.ClassroomPresenter.Model;
using UW.ClassroomPresenter.Model.Presentation;
using UW.ClassroomPresenter.Network.Groups;

namespace UW.ClassroomPresenter.Network.Messages.Presentation {
    public class PresentationNetworkService : INetworkService, IDisposable {
        private readonly SendingQueue m_Sender;
        private readonly PresentationModel m_Presentation;
        private readonly DeckTraversalsCollectionHelper m_DeckTraversalsCollectionHelper;

        private readonly EventQueue.PropertyEventDispatcher m_QuickPollChangedDispatcher;
        private QuickPollNetworkService m_QuickPollNetworkService;

        private bool m_Disposed;

        public PresentationNetworkService(SendingQueue sender, PresentationModel presentation) {
            this.m_Sender = sender;
            this.m_Presentation = presentation;

            this.m_DeckTraversalsCollectionHelper = new DeckTraversalsCollectionHelper(this);

            this.m_QuickPollNetworkService = new QuickPollNetworkService( this.m_Sender, this.m_Presentation );

            this.m_QuickPollChangedDispatcher = new EventQueue.PropertyEventDispatcher( this.m_Sender, new PropertyEventHandler( this.HandleQuickPollChanged ) );
            this.m_Presentation.Changed["QuickPoll"].Add( this.m_QuickPollChangedDispatcher.Dispatcher );
        }

        #region IDisposable Members

        ~PresentationNetworkService() {
            this.Dispose(false);
        }

        public void Dispose() {
            this.Dispose(true);
            System.GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing) {
            if(this.m_Disposed) return;
            if(disposing) {
                this.m_DeckTraversalsCollectionHelper.Dispose();
                this.m_Presentation.Changed["QuickPoll"].Remove( this.m_QuickPollChangedDispatcher.Dispatcher );
                this.m_QuickPollNetworkService.Dispose();
            }
            this.m_Disposed = true;
        }

        #endregion

        #region INetworkService Members

        public void ForceUpdate(Group receivers) {
            this.m_DeckTraversalsCollectionHelper.ForceUpdate(receivers);
            this.m_QuickPollNetworkService.ForceUpdate(receivers);
            this.SendQuickPollChanged( receivers );
        }

        #endregion

        private void HandleQuickPollChanged( object sender, PropertyEventArgs args ) {
            Group receivers = Group.AllParticipant;
            this.SendQuickPollChanged( receivers );

            if( this.m_QuickPollNetworkService != null ) {
                this.m_QuickPollNetworkService.Dispose();
            }

            this.m_QuickPollNetworkService = new QuickPollNetworkService( this.m_Sender, this.m_Presentation );
        }

        private void SendQuickPollChanged( Group receivers ) {
            Message message;
            message = new PresentationInformationMessage( this.m_Presentation );
            message.Group = receivers;
            using( Synchronizer.Lock( this.m_Presentation.SyncRoot ) ) {
                message.InsertChild( new QuickPollInformationMessage( this.m_Presentation.QuickPoll ) );
            }

            this.m_Sender.Send( message );
        }

        private class DeckTraversalsCollectionHelper : PropertyCollectionHelper, INetworkService {
            private readonly PresentationNetworkService m_Service;

            public DeckTraversalsCollectionHelper(PresentationNetworkService service) : base(service.m_Sender, service.m_Presentation, "DeckTraversals") {
                this.m_Service = service;
                base.Initialize();
            }

            protected override void Dispose(bool disposing) {
                if (disposing)
                    foreach (IDisposable disposable in this.Tags)
                        if (disposable != null)
                            disposable.Dispose();
                base.Dispose(disposing);
            }

            protected override object SetUpMember(int index, object member) {
                DeckTraversalModel traversal = ((DeckTraversalModel) member);

                // Do not re-broadcast any changes to remote decks.
                // This avoids re-sending the DeckOpenedMessage and does not
                // create a DeckNetworkService to watch the deck.
                if((traversal.Deck.Disposition & DeckDisposition.Remote) != 0)
                    return null;
                // new SSDeckTraversalNetworkService( this.m_Service.m_Sender, this.m_Service.m_Presentation, traversal );
                Group receivers = Group.AllParticipant;
                if( (traversal.Deck.Disposition & (DeckDisposition.StudentSubmission | DeckDisposition.QuickPoll)) != 0 ) {
                    receivers = Group.Submissions;
                }

                this.SendTraversalInformation(traversal, receivers);

                return new DeckTraversalNetworkService(this.m_Service.m_Sender, this.m_Service.m_Presentation, traversal);
            }

            protected override void TearDownMember(int index, object member, object tag) {
                DeckTraversalNetworkService service = ((DeckTraversalNetworkService) tag);

                if(service != null) {
                    DeckTraversalModel traversal = service.DeckTraversal;

                    service.Dispose();

                    Message message = new PresentationInformationMessage(this.m_Service.m_Presentation);
                    message.InsertChild(new DeckTraversalRemovedFromPresentationMessage(traversal));
                    this.m_Service.m_Sender.Send(message);

                    // TODO: Also send a DeckClosedMessage if necessary -- or should that be done by the DeckTraversalNetworkService?
                }
            }

            private void SendTraversalInformation(DeckTraversalModel traversal, Group receivers) {
                Message message, deck;
                message = new PresentationInformationMessage(this.m_Service.m_Presentation);
                message.Group = receivers;
                message.InsertChild(deck = new DeckInformationMessage(traversal.Deck));
                deck.InsertChild(DeckTraversalMessage.ForDeckTraversal(traversal));

                this.m_Service.m_Sender.Send(message);
            }

            #region INetworkService Members

            public void ForceUpdate(Group receivers) {
                using(Synchronizer.Lock(this.Owner.SyncRoot)) {
                    foreach (DeckTraversalNetworkService service in this.Tags) {
                        if (service != null) {
                            this.SendTraversalInformation(service.DeckTraversal, receivers);
                            service.ForceUpdate(receivers);
                        }
                    }
                }
            }

            #endregion
        }
    }
}
