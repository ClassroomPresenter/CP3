// $Id: RTPConnectionManager.cs 1463 2007-09-25 19:30:52Z linnell $
#if RTP_BUILD
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Threading;

using UW.ClassroomPresenter.Model;
using UW.ClassroomPresenter.Model.Network;

namespace UW.ClassroomPresenter.Network.RTP {
    public class RTPConnectionManager : ConnectionManager {
        private readonly PresenterModel m_Model;
        private readonly ProtocolModel m_Protocol;
        private readonly List<RTPClassroomManager> m_Classrooms;

        public RTPConnectionManager(PresenterModel model) {
            this.m_Model = model;
            this.m_Protocol = new ProtocolModel(this, "RTP");

            this.m_Classrooms = new List<RTPClassroomManager>();
            
            List<Venue> venueList = RTPVenueManager.GetVenues();

            foreach (Venue v in venueList) {
                this.m_Classrooms.Add(new RTPClassroomManager(this.m_Model, this, v.VenueName, v.VenueEndPoint, false));
                //Note that the UI connection dialog only shows 5 classrooms, regardless of how many we add here
            }

            using (Synchronizer.Lock(this.m_Protocol.SyncRoot)) {
                foreach (RTPClassroomManager manager in this.m_Classrooms) {
                    this.m_Protocol.Classrooms.Add(manager.Classroom);
                }
            }
        }

        protected override void Dispose(bool disposing) {
            try {
                using (Synchronizer.Lock(this.m_Protocol.SyncRoot)) {
                    foreach (RTPClassroomManager manager in this.m_Classrooms) {
                        this.m_Protocol.Classrooms.Remove(manager.Classroom);
                        manager.Dispose();
                    }
                }
            } finally {
                base.Dispose(disposing);
            }
        }

        public override ProtocolModel Protocol {
            get { return this.m_Protocol; }
        }
    }
}
#endif