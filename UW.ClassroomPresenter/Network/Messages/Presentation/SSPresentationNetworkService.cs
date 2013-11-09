// $Id: PresentationNetworkService.cs 656 2005-08-30 06:51:53Z pediddle $

using System;
using System.Diagnostics;
using System.Threading;

using UW.ClassroomPresenter.Model;
using UW.ClassroomPresenter.Model.Presentation;

namespace UW.ClassroomPresenter.Network.Messages.Presentation {
    public class SSPresentationNetworkService : IDisposable {
        private readonly SendingQueue m_Sender;
        private readonly PresentationModel m_Presentation;
        private readonly SSDeckTraversalsCollectionHelper m_SSDeckTraversalsCollectionHelper;
        private bool m_Disposed;

        public SSPresentationNetworkService(SendingQueue sender, PresentationModel presentation) {
            this.m_Sender = sender;
            this.m_Presentation = presentation;
            this.m_SSDeckTraversalsCollectionHelper = new SSDeckTraversalsCollectionHelper(this);
        }

        #region IDisposable Members

        ~SSPresentationNetworkService() {
            this.Dispose(false);
        }

        public void Dispose() {
            this.Dispose(true);
            System.GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing) {
            if(this.m_Disposed) return;
            if(disposing) {
                this.m_SSDeckTraversalsCollectionHelper.Dispose();
            }
            this.m_Disposed = true;
        }

        #endregion

        private class SSDeckTraversalsCollectionHelper : PropertyCollectionHelper {
            private readonly SSPresentationNetworkService m_Service;

            public SSDeckTraversalsCollectionHelper(SSPresentationNetworkService service) : base(service.m_Sender, service.m_Presentation, "DeckTraversals") {
                this.m_Service = service;
                base.Initialize();
            }

            protected override void Dispose(bool disposing) {
                if(disposing)
                    foreach(IDisposable disposable in this.Tags)
                        if(disposable != null)
                            disposable.Dispose();
                base.Dispose(disposing);
            }

            protected override object SetUpMember(int index, object member) {
                DeckTraversalModel traversal = ((DeckTraversalModel) member);

                // Do not re-broadcast any changes to remote decks, just add a listener for student submissions
                // This avoids re-sending the DeckOpenedMessage and does not
                // create a DeckNetworkService to watch the deck.
                if( (traversal.Deck.Disposition & DeckDisposition.Remote) != 0 )
                    return new SSDeckTraversalNetworkService(this.m_Service.m_Sender, this.m_Service.m_Presentation, traversal);
                else
                    return null;
            }

            protected override void TearDownMember(int index, object member, object tag) {
                DeckTraversalNetworkService service = ((DeckTraversalNetworkService) tag);
                if(service != null) {
                    service.Dispose();
                }
            }
        }
    }
}
