// $Id: RoleNetworkService.cs 953 2006-04-22 20:16:07Z pediddle $

using System;
using System.Diagnostics;
using System.Threading;

using UW.ClassroomPresenter.Model;
using UW.ClassroomPresenter.Model.Network;
using UW.ClassroomPresenter.Network.Messages;
using UW.ClassroomPresenter.Network.Messages.Network;
using UW.ClassroomPresenter.Network.Messages.Presentation;
using UW.ClassroomPresenter.Network.Groups;

namespace UW.ClassroomPresenter.Network.Messages.Network {
    public abstract class RoleNetworkService : INetworkService {
        protected readonly SendingQueue Sender;
        private readonly RoleModel m_Role;

        public RoleNetworkService(SendingQueue sender, RoleModel role) {
            this.Sender = sender;
            this.m_Role = role;
        }

        #region IDisposable Members

        ~RoleNetworkService() {
            this.Dispose(false);
        }

        public void Dispose() {
            this.Dispose(true);
            System.GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing) {
            // Nothing to dispose, except in subclasses.
        }

        #endregion

        public RoleModel Role {
            get { return this.m_Role; }
        }

        public static RoleNetworkService ForRole(SendingQueue sender, PresenterModel model, RoleModel role) {
            if(role is InstructorModel) {
                return new InstructorNetworkService(sender, ((InstructorModel) role));
            } else if(role is StudentModel) {
                return new StudentNetworkService( sender, model, ((StudentModel) role) );
            } else {
                // Either the role type is unsupported, or it is a PublicModel
                // which currently have no published properties which need to be broadcast.
                return null;
            }
        }

        #region INetworkService Members

        public abstract void ForceUpdate(Group receivers);

        #endregion
    }
}