using System;
using System.Diagnostics;
using System.Threading;

using UW.ClassroomPresenter.Model;
using UW.ClassroomPresenter.Model.Network;
using UW.ClassroomPresenter.Model.Presentation;

using UW.ClassroomPresenter.Network.Messages;
using UW.ClassroomPresenter.Network.Messages.Network;
using UW.ClassroomPresenter.Network.Messages.Presentation;

namespace UW.ClassroomPresenter.Network.Beacons {
    /// <summary>
    /// This beacon is intended to be used by the Unicast to Multicast bridge.  It differs from the
    /// main beacon in that if the local node's role is not Instructor, we create a fake instructor
    /// beacon message.  This is to allow a Public node receiving a unicast presentation (which does 
    /// not have a beacon) to create a beacon for the purpose of bridging the presentation into a
    /// multicast group.
    /// </summary>
    public class BridgeBeaconService : BeaconService {
        private readonly SendingQueue m_Sender;
        private readonly PresenterModel m_Model;

        private bool m_Disposed;
        private int m_BeaconIntervalSeconds;
        private Guid m_NonInstructorId;

        public BridgeBeaconService(SendingQueue sender, PresenterModel model)
            : base(sender) {
            this.m_Sender = sender;
            this.m_Model = model;
            this.m_NonInstructorId = Guid.NewGuid();

            //Get the interval from app.config
            this.m_BeaconIntervalSeconds = 3;
            String tmp = System.Configuration.ConfigurationManager.AppSettings[this.GetType().ToString() + ".BeaconIntervalSeconds"];
            if ((tmp != null) && (tmp.Trim() != "")) {
                int tmpint;
                if (int.TryParse(tmp, out tmpint)) {
                    if ((tmpint > 0) && (tmpint <= 20)) {
                        Trace.WriteLine("Setting BeaconIntervalSeconds from app.config: " + tmpint.ToString(), this.GetType().ToString());
                        this.m_BeaconIntervalSeconds = tmpint;
                    }
                }
            }

            this.BeaconInterval = new TimeSpan(0, 0, 0, m_BeaconIntervalSeconds, 0);
        }

        protected override void Dispose(bool disposing) {
            try {
                if (this.m_Disposed) return;
                this.m_Disposed = true;
            }
            finally {
                base.Dispose(disposing);
            }
        }


        protected override Message MakeBeaconMessage() {
            bool isInstructor = false;
            using (Synchronizer.Lock(this.m_Model.Participant.SyncRoot)) {
                if (this.m_Model.Participant.Role == null)
                    return null;

                using (Synchronizer.Lock(this.m_Model.Participant.Role.SyncRoot)) {
                    if (this.m_Model.Participant.Role is InstructorModel) {
                        isInstructor = true;
                    }
                }
            }

            if (isInstructor) {
                return InstructorMakeBeaconMessage();
            }
            else {
                return NonInstructorMakeBeaconMessage();
            }

        }

        /// <summary>
        /// Create the normal beacon message
        /// </summary>
        /// <returns></returns>
        protected Message InstructorMakeBeaconMessage() {
            using (Synchronizer.Lock(this.m_Model.Participant.SyncRoot)) {
                using (Synchronizer.Lock(this.m_Model.Participant.Role.SyncRoot)) {
                    Message role = RoleMessage.ForRole(this.m_Model.Participant.Role);
                    InstructorModel instructor = ((InstructorModel)this.m_Model.Participant.Role);
                    if (instructor.CurrentPresentation != null)
                        role.InsertChild(new InstructorCurrentPresentationChangedMessage(instructor.CurrentPresentation));
                    if (instructor.CurrentDeckTraversal != null) {
                        using (Synchronizer.Lock(instructor.CurrentDeckTraversal.SyncRoot)) {
                            Message traversal = new InstructorCurrentDeckTraversalChangedMessage(instructor.CurrentDeckTraversal);
                            Message predecessor = traversal.Predecessor;
                            traversal.Predecessor = new SlideInformationMessage(instructor.CurrentDeckTraversal.Current.Slide);
                            traversal.Predecessor.InsertChild(new TableOfContentsEntryMessage(instructor.CurrentDeckTraversal.Current));
                            traversal.Predecessor.AddOldestPredecessor(predecessor);
                            role.InsertChild(traversal);
                        }
                    }
                    return role;
                }
            }
        }

        /// <summary>
        /// Even though we are not an instructor, here we fake an instructor beacon message for the purpose of unicast to multicast
        /// bridging.
        /// </summary>
        /// <returns></returns>
        protected Message NonInstructorMakeBeaconMessage() {
            InstructorModel instructor = new InstructorModel(m_NonInstructorId);
            Message role = RoleMessage.ForRole(instructor);

            using (Synchronizer.Lock(this.m_Model.Workspace)) { 
                if (this.m_Model.Workspace.CurrentPresentation != null) {
                    using (Synchronizer.Lock(this.m_Model.Workspace.CurrentPresentation.SyncRoot)) {
                        if (this.m_Model.Workspace.CurrentPresentation.Value != null) {
                            role.InsertChild(new InstructorCurrentPresentationChangedMessage(this.m_Model.Workspace.CurrentPresentation));
                        }
                    }
                }

                if (this.m_Model.Workspace.CurrentDeckTraversal != null) {
                    using (Synchronizer.Lock(this.m_Model.Workspace.CurrentDeckTraversal.SyncRoot)) {
                        if (this.m_Model.Workspace.CurrentDeckTraversal.Value != null) {
                            Message traversal = new InstructorCurrentDeckTraversalChangedMessage(this.m_Model.Workspace.CurrentDeckTraversal.Value,false);
                            Message predecessor = traversal.Predecessor;
                            using (Synchronizer.Lock((~this.m_Model.Workspace.CurrentDeckTraversal).SyncRoot)) {
                                using (Synchronizer.Lock((~this.m_Model.Workspace.CurrentDeckTraversal).Current.SyncRoot)) {
                                    traversal.Predecessor = new SlideInformationMessage((~this.m_Model.Workspace.CurrentDeckTraversal).Current.Slide);
                                    TableOfContentsModel.Entry entry = (~this.m_Model.Workspace.CurrentDeckTraversal).Current;
                                    if (entry != null)
                                        traversal.Predecessor.InsertChild(new TableOfContentsEntryMessage(entry));
                                    traversal.Predecessor.AddOldestPredecessor(predecessor);
                                    role.InsertChild(traversal);
                                }
                            }
                        }
                    }
                }
            }
            return role;
        }
    }
}
