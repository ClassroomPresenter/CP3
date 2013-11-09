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
    public class DefaultBeaconService : BeaconService {
        private readonly SendingQueue m_Sender;
        private readonly EventQueue.PropertyEventDispatcher m_RoleChangedDispatcher;
        private readonly EventQueue.PropertyEventDispatcher m_BeaconIntervalChangedDispatcher;
        private readonly PresenterModel m_Model;

        private bool m_Disposed;

        public DefaultBeaconService(SendingQueue sender, PresenterModel model) : base(sender) {
            this.m_Sender = sender;
            this.m_Model = model;

            this.m_RoleChangedDispatcher = new EventQueue.PropertyEventDispatcher(this.m_Sender,
                new PropertyEventHandler(this.HandleRoleChanged));
            this.m_Model.Participant.Changed["Role"].Add(this.m_RoleChangedDispatcher.Dispatcher);
            this.m_RoleChangedDispatcher.Dispatcher(this, null);

            this.m_BeaconIntervalChangedDispatcher = new EventQueue.PropertyEventDispatcher(this.m_Sender,
                new PropertyEventHandler(this.HandleBeaconIntervalChanged));
            this.m_Model.ViewerState.Changed["BeaconInterval"].Add(this.m_BeaconIntervalChangedDispatcher.Dispatcher);
            this.m_BeaconIntervalChangedDispatcher.Dispatcher(this, null);
        }

        protected override void Dispose(bool disposing) {
            try {
                if(this.m_Disposed) return;
                if(disposing) {
                    this.m_Model.Participant.Changed["Role"].Remove(this.m_RoleChangedDispatcher.Dispatcher);
                    this.m_Model.ViewerState.Changed["BeaconInterval"].Remove(this.m_BeaconIntervalChangedDispatcher.Dispatcher);
                }
                this.m_Disposed = true;
            } finally {
                base.Dispose(disposing);
            }
        }

        private void HandleRoleChanged(object sender, PropertyEventArgs args) {
            using(Synchronizer.Lock(this)) {
                bool isInstructor = false;
                using(Synchronizer.Lock(this.m_Model.Participant.SyncRoot)) {
                    if(this.m_Model.Participant.Role is InstructorModel) {
                        isInstructor = true;
                    }
                }
                if (isInstructor) {
                    using (Synchronizer.Lock(this.m_Model.ViewerState.SyncRoot)) {
                        // Send beacons periodically for instructors 
                        // according to the timeout in the ViewerStateModel.
                        this.BeaconInterval = new TimeSpan(0, 0, 0, this.m_Model.ViewerState.BeaconInterval, 0);
                    }
                } else {
                    // Don't send beacons for other roles.
                    this.BeaconInterval = new TimeSpan(0, 0, 0, 0, Timeout.Infinite);
                }
            }
        }

        private void HandleBeaconIntervalChanged(object sender, PropertyEventArgs args) {
            using (Synchronizer.Lock(this)) {
                bool isInstructor = false;
                using (Synchronizer.Lock(this.m_Model.Participant.SyncRoot)) {
                    if (this.m_Model.Participant.Role is InstructorModel) {
                        isInstructor = true;
                    }
                }
                if (isInstructor) {
                    using (Synchronizer.Lock(this.m_Model.ViewerState.SyncRoot)) {
                        // Send beacons periodically for instructors 
                        // according to the timeout in the ViewerStateModel.
                        this.BeaconInterval = new TimeSpan(0, 0, 0, this.m_Model.ViewerState.BeaconInterval, 0);
                    }
                }
            }
        }

        protected override Message MakeBeaconMessage() {
            using(Synchronizer.Lock(this.m_Model.Participant.SyncRoot)) {
                if(this.m_Model.Participant.Role == null)
                    return null;

                using(Synchronizer.Lock(this.m_Model.Participant.Role.SyncRoot)) {
                    Message role = RoleMessage.ForRole(this.m_Model.Participant.Role);

                    if(this.m_Model.Participant.Role is InstructorModel) {
                        InstructorModel instructor = ((InstructorModel) this.m_Model.Participant.Role);

                        if(instructor.CurrentPresentation != null)
                            role.InsertChild(new InstructorCurrentPresentationChangedMessage(instructor.CurrentPresentation));

                        if(instructor.CurrentDeckTraversal != null) {
                            using(Synchronizer.Lock(instructor.CurrentDeckTraversal.SyncRoot)) {
                                Message traversal = new InstructorCurrentDeckTraversalChangedMessage(instructor.CurrentDeckTraversal);
                                Message predecessor = traversal.Predecessor;
                                traversal.Predecessor = new SlideInformationMessage(instructor.CurrentDeckTraversal.Current.Slide);
                                traversal.Predecessor.InsertChild(new TableOfContentsEntryMessage(instructor.CurrentDeckTraversal.Current));
                                traversal.Predecessor.AddOldestPredecessor(predecessor);
                                role.InsertChild(traversal);
                            }
                        }
                    }

                    return role;
                }
            }
        }
    }
}
