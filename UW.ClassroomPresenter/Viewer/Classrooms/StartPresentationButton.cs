// $Id: StartPresentationButton.cs 877 2006-02-07 23:24:48Z pediddle $

using System;
using System.Threading;
using System.Windows.Forms;

using UW.ClassroomPresenter.Decks;
using UW.ClassroomPresenter.Model;
using UW.ClassroomPresenter.Model.Network;
using UW.ClassroomPresenter.Model.Presentation;

namespace UW.ClassroomPresenter.Viewer.Classrooms {
    internal class StartPresentationButton : PhatAutosizeButton {
        private readonly PresenterModel m_Model;

        private bool m_Disposed;

        public StartPresentationButton(PresenterModel model) {
            this.m_Model = model;

            this.Text = "Start a New Presentation…";
        }

        protected override void Dispose(bool disposing) {
            if(this.m_Disposed) return;
            try {
                if(disposing) {
                }
            } finally {
                base.Dispose(disposing);
            }
            this.m_Disposed = true;
        }

        protected override void OnClick(EventArgs e) {
            base.OnClick(e);
            this.StartEmptyPresentation();
        }

        private void StartEmptyPresentation() {
            // Make sure we have a suitable RoleModel for broadcasting a presentation.
            InstructorModel instructor;
            using(Synchronizer.Lock(this.m_Model.Participant.SyncRoot)) {
                instructor = this.m_Model.Participant.Role as InstructorModel;
                if(instructor == null) {
                    // Make the participant representing this user an Instructor,
                    // which tells the ConnectionManager to broadcast the presentation
                    // once it's added to the classroom.
                    instructor = new InstructorModel(Guid.NewGuid());
                    this.m_Model.Participant.Role = instructor;
                }
            }

            // Create the presentation.
            // TODO: Find something useful to use as the Presentation's HumanName.
            PresentationModel pres = new PresentationModel(Guid.NewGuid(), this.m_Model.Participant, "No presentation yet");

            using(Synchronizer.Lock(this.m_Model.Network.SyncRoot)) {
                // Associate the participant with itself.  This removes any existing association
                // (since an Instructor should not be processing broadcasts from other clients),
                // and also causes the NetworkAssociationService to copy anything we do to the
                // InstructorModel to the WorkspaceModel.
                this.m_Model.Network.Association = this.m_Model.Participant;
            }

            // Here's the kicker, which triggers the viewer to hide the classroom browser
            // and start displaying the presentation.  The NetworkAssociationService copies
            // the presentation to the WorkspaceModel, which causes it to be displayed by the UI.
            using(Synchronizer.Lock(instructor.SyncRoot)) {
                instructor.CurrentPresentation = pres;
            }
        }
    }
}
