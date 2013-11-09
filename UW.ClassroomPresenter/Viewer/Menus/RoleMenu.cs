// $Id: RoleMenu.cs 1309 2007-02-28 00:23:58Z pediddle $

using System;
using System.Windows.Forms;
using System.Threading;
using System.Diagnostics;

using UW.ClassroomPresenter.Model;
using UW.ClassroomPresenter.Model.Network;

namespace UW.ClassroomPresenter.Viewer.Menus {
    public class RoleMenu : MenuItem {
        protected readonly PresenterModel m_Model;

        public RoleMenu(PresenterModel pm) {
            this.m_Model = pm;
            this.Text = "&Role";

            this.MenuItems.Add(new StudentRoleMenuItem(pm));
            this.MenuItems.Add(new PublicRoleMenuItem(pm));

            using (Synchronizer.Lock(this.m_Model.Participant.SyncRoot)) {
                this.m_Model.Participant.Changed["Role"].Add(new PropertyEventHandler(this.HandleRoleChanged));
            }

            // Initialize the state of the menu items.
            this.HandleRoleChanged(this, null);
        }

        protected void HandleRoleChanged(object sender, PropertyEventArgs e) {
            // Bug 988: Completely disable the role menu for the Instructor role.
            using (Synchronizer.Lock(this.m_Model.Participant.SyncRoot)) {
                this.Enabled = this.Visible = !(this.m_Model.Participant.Role is InstructorModel);
            }
        }

        protected override void Dispose(bool disposing) {
            if (disposing) {
                using (Synchronizer.Lock(this.m_Model.Participant.SyncRoot)) {
                    this.m_Model.Participant.Changed["Role"].Remove(new PropertyEventHandler(this.HandleRoleChanged));
                }
            }
            base.Dispose(disposing);
        }

        public abstract class RoleMenuItem : MenuItem {
            protected readonly PresenterModel m_Model;

            public RoleMenuItem(PresenterModel pm) {
                this.m_Model = pm;

                using(Synchronizer.Lock(this.m_Model.Participant.SyncRoot)) {
                    this.m_Model.Participant.Changed["Role"].Add(new PropertyEventHandler(this.HandleRoleChanged));
                }

                // Initialize the state of the menu items.
                this.HandleRoleChanged(this, null);
            }

            protected override void Dispose(bool disposing) {
                if(disposing) {
                    using(Synchronizer.Lock(this.m_Model.Participant.SyncRoot)) {
                        this.m_Model.Participant.Changed["Role"].Remove(new PropertyEventHandler(this.HandleRoleChanged));
                    }
                }
                base.Dispose(disposing);
            }

            protected abstract void HandleRoleChanged(object sender, PropertyEventArgs e);
        }

        public class StudentRoleMenuItem : RoleMenuItem {
            private RoleModel m_LastRole;

            public StudentRoleMenuItem(PresenterModel pm) : base(pm) {
                this.Text = "&Student Role";
            }

            protected override void HandleRoleChanged(object sender, PropertyEventArgs e) {
                using(Synchronizer.Lock(this.m_Model.Participant.SyncRoot)) {
                    if(this.m_Model.Participant.Role is StudentModel) {
                        this.Checked = true;
                        this.m_LastRole = this.m_Model.Participant.Role;
                    } else {
                        this.Checked = false;
                    }
                }
            }

            protected override void OnClick(EventArgs e) {
                base.OnClick(e);

                using(Synchronizer.Lock(this.m_Model.SyncRoot)) {
                    using(Synchronizer.Lock(this.m_Model.Participant.SyncRoot)) {
                        this.m_Model.Participant.Role = this.m_LastRole != null ? this.m_LastRole
                            : new StudentModel(Guid.NewGuid());
                    }

                    using(Synchronizer.Lock(this.m_Model.Network.SyncRoot)) {
                        if(this.m_Model.Network.Association == null || this.m_Model.Network.Association == this.m_Model.Participant)
                            // TODO: This is probably a good place to make a new type of ParticipantModel which automatically associates with the first available instructor in the classroom.
                            this.m_Model.Network.Association = null;
                    }
                }
            }
        }

        public class PublicRoleMenuItem : RoleMenuItem {
            private RoleModel m_LastRole;

            public PublicRoleMenuItem(PresenterModel pm) : base(pm) {
                this.Text = "&Public Role";
            }

            protected override void HandleRoleChanged(object sender, PropertyEventArgs e) {
                using(Synchronizer.Lock(this.m_Model.Participant.SyncRoot)) {
                    if(this.m_Model.Participant.Role is PublicModel) {
                        this.Checked = true;
                        this.m_LastRole = this.m_Model.Participant.Role;
                    } else {
                        this.Checked = false;
                    }
                }
            }

            protected override void OnClick(EventArgs e) {
                base.OnClick(e);

                using(Synchronizer.Lock(this.m_Model.SyncRoot)) {
                    using(Synchronizer.Lock(this.m_Model.Participant.SyncRoot)) {
                        this.m_Model.Participant.Role = this.m_LastRole != null ? this.m_LastRole
                            : new PublicModel(Guid.NewGuid());
                    }

                    using(Synchronizer.Lock(this.m_Model.Network.SyncRoot)) {
                        if(this.m_Model.Network.Association == null || this.m_Model.Network.Association == this.m_Model.Participant)
                            // TODO: This is probably a good place to make a new type of ParticipantModel which automatically associates with the first available instructor in the classroom.
                            this.m_Model.Network.Association = null;
                    }
                }
            }
        }
    }
}
