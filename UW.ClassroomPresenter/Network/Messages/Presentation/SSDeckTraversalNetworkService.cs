// $Id: SSDeckTraversalNetworkService.cs 656 2005-03-18 15:52:00Z cmprince $

using System;
using System.Collections;
using System.Diagnostics;
using System.Drawing;
using System.Threading;

using UW.ClassroomPresenter.Model;
using UW.ClassroomPresenter.Model.Presentation;

namespace UW.ClassroomPresenter.Network.Messages.Presentation {
    public class SSDeckTraversalNetworkService : IDisposable {
        private readonly SendingQueue m_Sender;
        private readonly PresentationModel m_Presentation;
        private readonly DeckTraversalModel m_DeckTraversal;

        private readonly SSDeckNetworkService m_SSDeckNetworkService;

        private bool m_Disposed;

        public SSDeckTraversalNetworkService(SendingQueue sender, PresentationModel presentation, DeckTraversalModel traversal) {
            this.m_Sender = sender;
            this.m_Presentation = presentation;
            this.m_DeckTraversal = traversal;

            this.m_SSDeckNetworkService = new SSDeckNetworkService(this.m_Sender, this.m_Presentation, this.m_DeckTraversal.Deck);
        }

        #region IDisposable Members

        ~SSDeckTraversalNetworkService() {
            this.Dispose(false);
        }

        public void Dispose() {
            this.Dispose(true);
            System.GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing) {
            if(this.m_Disposed) return;
            if(disposing) {
                this.m_SSDeckNetworkService.Dispose();
            }
            this.m_Disposed = true;
        }

        #endregion

        public DeckTraversalModel DeckTraversal {
            get { return this.m_DeckTraversal; }
        }
    }
}
