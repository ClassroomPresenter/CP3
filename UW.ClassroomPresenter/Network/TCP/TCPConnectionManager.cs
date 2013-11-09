// $Id: TCPConnectionManager.cs 1598 2008-04-30 00:49:50Z lining $

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Threading;

using UW.ClassroomPresenter.Model;
using UW.ClassroomPresenter.Model.Network;
using UW.ClassroomPresenter.Network.Broadcast;
using System.Collections;

namespace UW.ClassroomPresenter.Network.TCP {
    public class TCPConnectionManager : ConnectionManager {
        private readonly PresenterModel m_Model;
        private readonly ProtocolModel m_Protocol;
        private readonly TCPClassroomManager m_Classroom;

        public TCPConnectionManager(PresenterModel model) {
            this.m_Model = model;
            this.m_Protocol = new ProtocolModel(this, "TCP");
          
            this.m_Classroom = new TCPClassroomManager(this.m_Model, this, Strings.InstructorStartTCPServer);

            using (Synchronizer.Lock(this.m_Protocol.SyncRoot)) {
                this.m_Protocol.Classrooms.Add(this.m_Classroom.Classroom);
            }

        }

        protected override void Dispose(bool disposing) {
            if (disposing) {
                try {
                    using (Synchronizer.Lock(this.m_Protocol.SyncRoot)) {
                        this.m_Protocol.Classrooms.Remove(this.m_Classroom.Classroom);
                        this.m_Classroom.Dispose();
                    }
                }
                finally {
                    base.Dispose(disposing);
                }
            }
        }

        public override ProtocolModel Protocol {
            get { return this.m_Protocol; }
        }

        /// <summary>
        /// This is the "Server" classroom.  Should change the type name to reflect that.
        /// </summary>
        public TCPClassroomManager ClassroomManager
        {
            get { return this.m_Classroom; }
        }


    }
}
