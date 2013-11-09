// $Id: RoleSynchronizer.cs 1910 2009-07-15 16:59:08Z fred $

using System;
using UW.ClassroomPresenter.Model;
using UW.ClassroomPresenter.Model.Network;
using UW.ClassroomPresenter.Network.Groups;
using System.Threading;
using System.Diagnostics;


namespace UW.ClassroomPresenter.Viewer.Slides {
    public class RoleSynchronizer {
        private readonly SlideViewer m_SlideViewer;
        public SlideViewer SlideViewer {
            get { return this.m_SlideViewer; }
        }
        private readonly ParticipantModel m_Participant;

        public RoleSynchronizer (SlideViewer sv, ParticipantModel pm) {
            this.m_SlideViewer = sv;
            this.m_Participant = pm;
            this.m_Participant.Changed["Role"].Add(new PropertyEventHandler(this.onRoleChange));
            this.onRoleChange(this, null);
        }

        public void Dispose() {
            this.m_Participant.Changed["Role"].Remove(new PropertyEventHandler(this.onRoleChange));
        }

        /// <summary>
        /// Add a group, but only if not already a member.
        /// </summary>
        /// <param name="g"></param>
        private void addGroup(Group g) {
            if (!this.m_Participant.Groups.Contains(g)) {
                Trace.WriteLine("RoleSynchronizer adding group: " + g.FriendlyName, this.GetType().ToString());
                this.m_Participant.Groups.Add(g);
            }
        }

        /// <summary>
        /// remove a group if we are a member.
        /// </summary>
        /// <param name="g"></param>
        private void removeGroup(Group g) {
            if (this.m_Participant.Groups.Contains(g)) {
                Trace.WriteLine("RoleSynchronizer removing group: " + g.FriendlyName, this.GetType().ToString());
                this.m_Participant.Groups.Remove(g);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        /// Notice that we want to be careful about how groups are removed and added.  If we remove a group, even if
        /// we intend to add it back immediately, we can miss network transmissions to that group in the window
        /// of our non-membership.  To avoid this, we should only add/remove groups when necessary, never remove
        /// 'All Participants' (eg. never do Groups.Clear), and do adds before removes.  
        /// Another solution to this would be to merge all group changes into one atomic GroupUpdate message.
        private void onRoleChange(object sender, PropertyEventArgs e) {
            RoleModel role = null;
            using (Synchronizer.Lock(this.m_Participant.SyncRoot)) {
                if (m_Participant.Role == null)
                {
                    return;
                }
                role = m_Participant.Role;
            }
            using(Synchronizer.Lock(this.m_SlideViewer.SlideDisplay.SyncRoot)) {
                this.m_SlideViewer.SlideDisplay.SheetDisposition = Model.Presentation.SheetDisposition.All | Model.Presentation.SheetDisposition.Background;
                if (role is Model.Network.InstructorModel) {
                    addGroup(Group.AllInstructor);
                    addGroup(Group.Submissions);
                    removeGroup(Group.AllPublic);
                    removeGroup(Group.AllStudent);
                    this.m_SlideViewer.SlideDisplay.SheetDisposition ^= Model.Presentation.SheetDisposition.Instructor;
                } else if (role is Model.Network.StudentModel) {
                    addGroup(Group.AllStudent);
                    removeGroup(Group.AllPublic);
                    removeGroup(Group.AllInstructor);
                    removeGroup(Group.Submissions);
                    this.m_SlideViewer.SlideDisplay.SheetDisposition ^= Model.Presentation.SheetDisposition.Student;
                } else if (role is Model.Network.PublicModel) {
                    addGroup(Group.AllPublic);
                    addGroup(Group.Submissions);
                    removeGroup(Group.AllInstructor);
                    removeGroup(Group.AllStudent);
                    this.m_SlideViewer.SlideDisplay.SheetDisposition ^= Model.Presentation.SheetDisposition.Public;
                }
            }
        }
    }
}
