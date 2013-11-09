// $Id: ParticipantNetworkService.cs 1221 2006-09-26 21:37:26Z fred $

using System;
using System.Collections;
using System.Diagnostics;
using System.Threading;

using UW.ClassroomPresenter.Model;
using UW.ClassroomPresenter.Model.Network;
using UW.ClassroomPresenter.Network.Groups;

namespace UW.ClassroomPresenter.Network.Messages.Network {
    public class ParticipantNetworkService : INetworkService, IDisposable {
        private readonly SendingQueue m_Sender;
        private readonly EventQueue.PropertyEventDispatcher m_RoleChangedDispatcher;

        private readonly PresenterModel m_Model;
        private readonly ParticipantModel m_Participant;
        private readonly GroupCollectionHelper m_GroupCollectionHelper;
        private RoleModel m_Role;
        private RoleNetworkService m_RoleNetworkService;
        private bool m_Disposed;

        public ParticipantNetworkService(SendingQueue sender, PresenterModel model, ParticipantModel participant) {
            this.m_Sender = sender;
            this.m_Model = model;
            this.m_Participant = participant;

            this.m_RoleChangedDispatcher = new EventQueue.PropertyEventDispatcher(this.m_Sender, new PropertyEventHandler(this.HandleRoleChanged));
            this.m_Participant.Changed["Role"].Add(this.m_RoleChangedDispatcher.Dispatcher);
            this.m_RoleChangedDispatcher.Dispatcher(this, null);

            this.m_GroupCollectionHelper = new GroupCollectionHelper(this);
        }

        #region IDisposable Members

        ~ParticipantNetworkService() {
            this.Dispose(false);
        }

        public void Dispose() {
            this.Dispose(true);
            System.GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing) {
            if(this.m_Disposed) return;
            if(disposing) {
                this.m_Participant.Changed["Role"].Remove(this.m_RoleChangedDispatcher.Dispatcher);
                // Dispose of the RoleNetworkService via the Role setter.
                this.Role = null;
            }
            this.m_Disposed = true;
        }

        #endregion

        private RoleModel Role {
            get { return this.m_Role; }
            set {
                if(this.m_RoleNetworkService != null) {
                    this.m_RoleNetworkService.Dispose();
                    this.m_RoleNetworkService = null;
                }

                this.m_Role = value;

                if(this.m_Role != null) {
                    // Create a RoleNetworkService for the role (or not,
                    // since RoleNetworkService.ForRole will return null if the role has no published properties).
                    this.m_RoleNetworkService = RoleNetworkService.ForRole(this.m_Sender, this.m_Model, this.m_Role);
                }
            }
        }

        private void HandleRoleChanged(object sender, PropertyEventArgs args) {
            using(Synchronizer.Lock(this.m_Participant.SyncRoot)) {
                this.SendRoleChangedMessage(this.m_Participant.Role, Group.AllParticipant);
                this.Role = this.m_Participant.Role;
            }
        }

        private void SendRoleChangedMessage(RoleModel role, Group receivers) {
            Message message = RoleMessage.ForRole(role);
            message.Group = receivers;
            if(message != null) {
                this.m_Sender.Send(message);
            }
        }

        #region INetworkService Members

        public void ForceUpdate(Group receivers) {
            using (Synchronizer.Lock(this.m_Participant.SyncRoot)) {
                if (this.Role != null) {
                    this.SendRoleChangedMessage(this.Role, receivers);
                    this.m_RoleNetworkService.ForceUpdate(receivers);
                    this.m_GroupCollectionHelper.ForceUpdate(receivers);
                }
            }
        }

        #endregion


        private class GroupCollectionHelper : PropertyCollectionHelper, INetworkService {
            private readonly ParticipantNetworkService m_Service;

            public GroupCollectionHelper(ParticipantNetworkService service)
                : base(service.m_Sender, service.m_Participant, "Groups") {
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
                Group group = ((Group)member);

                this.SendGroupAddedMessage(group, Group.AllParticipant);

                return group;
            }

            protected override void TearDownMember(int index, object member, object tag) {
                Group group = (Group)member;

                if (group != null) {
                    Message message = new ParticipantGroupRemovedMessage(group);
                    this.m_Service.m_Sender.Send(message);
                }
            }

            private void SendGroupAddedMessage(Group group, Group receivers) {
                Message message = new ParticipantGroupAddedMessage(group);
                message.Group = receivers;
                this.m_Service.m_Sender.Send(message);
            }

            #region INetworkService Members

            public void ForceUpdate(Group receivers) {
                using (Synchronizer.Lock(this.Owner.SyncRoot)) {
                    foreach (Group group in this.Tags) {
                        if (group != null)
                            this.SendGroupAddedMessage(group, receivers);
                    }
                }
            }

            #endregion
        }
    }
}
