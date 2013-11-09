
#if RTP_BUILD

using System;
using System.Diagnostics;
using System.Threading;
using System.Reflection;
using MSR.LST.Net.Rtp;
using UW.ClassroomPresenter.Model;
using UW.ClassroomPresenter.Model.Network;

namespace UW.ClassroomPresenter.Network.RTP {
    public class RtpLocalParticipant : RtpParticipant, IDisposable {

        private readonly ParticipantModel m_Participant;
        private bool m_Disposed;

        public RtpLocalParticipant(ParticipantModel participant) : base(participant.Guid.ToString(), null) {
            this.m_Participant = participant;

            // Enable broadcast of the application name and version.
            this.SetTool(true);

            this.m_Participant.Changed["HumanName"].Add(new PropertyEventHandler(this.HandleHumanNameChanged));
            this.HandleHumanNameChanged(this, null);
        }

        #region IDisposable Members

        ~RtpLocalParticipant() {
            this.Dispose(false);
        }

        public void Dispose() {
            this.Dispose(true);
            System.GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing) {
            if(this.m_Disposed) return;
            if(disposing) {
                this.m_Participant.Changed["HumanName"].Remove(new PropertyEventHandler(this.HandleHumanNameChanged));
            }
            this.m_Disposed = true;
        }

        #endregion

        private void HandleHumanNameChanged(object sender, PropertyEventArgs args) {
            using(Synchronizer.Lock(this.m_Participant.SyncRoot)) {
                this.Name = this.m_Participant.HumanName;
            }
        }
    }
}

#endif