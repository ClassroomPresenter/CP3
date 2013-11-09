// $Id: PresenterNetworkService.cs 1499 2007-11-30 19:58:18Z fred $

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

using UW.ClassroomPresenter.Model;
using UW.ClassroomPresenter.Model.Network;

using UW.ClassroomPresenter.Network.Messages;
using UW.ClassroomPresenter.Network.Messages.Network;
using UW.ClassroomPresenter.Network.Groups;
using UW.ClassroomPresenter.Network.Messages.Presentation;

namespace UW.ClassroomPresenter.Network.Messages {
    public class PresenterNetworkService : INetworkService, IDisposable {
        private readonly SendingQueue m_Sender;
        private readonly PresenterModel m_Model;
        private readonly ParticipantNetworkService m_ParticipantNetworkService;
        private readonly VersionExchangeNetworkService m_VersionExchangeNetworkService;

        private bool m_Disposed;

        public PresenterNetworkService(SendingQueue sender, PresenterModel model) {
            this.m_Sender = sender;
            this.m_Model = model;


            this.m_ParticipantNetworkService = new ParticipantNetworkService(this.m_Sender, this.m_Model, this.m_Model.Participant);
            this.m_VersionExchangeNetworkService = new VersionExchangeNetworkService(m_Sender);
        }

        #region IDisposable Members

        ~PresenterNetworkService() {
            this.Dispose(false);
        }

        public void Dispose() {
            this.Dispose(true);
            System.GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing) {
            if(this.m_Disposed) return;
            if(disposing) {
                this.m_ParticipantNetworkService.Dispose();
            }
            this.m_Disposed = true;
        }

        #endregion

        #region INetworkService Members

        public void ForceUpdate(Group receivers) {
            this.m_ParticipantNetworkService.ForceUpdate(receivers);
            this.m_VersionExchangeNetworkService.ForceUpdate(receivers);
        }

        #endregion
    }
}
