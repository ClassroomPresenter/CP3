// $Id: ClassroomBrowser.cs 968 2006-06-23 06:52:46Z cmprince $

using System;
using System.Windows.Forms;
using System.Drawing;
using System.Diagnostics;
using System.Runtime.InteropServices;

using UW.ClassroomPresenter.Decks;
using UW.ClassroomPresenter.Model;
using UW.ClassroomPresenter.Model.Presentation;
using UW.ClassroomPresenter.Model.Network;

namespace UW.ClassroomPresenter.Viewer.Classrooms {
    /// <summary>
    /// A wizard allowing the user to join a presentation.
    /// </summary>
    public class ClassroomBrowserControl : UserControl {
        private PresenterModel m_Model;

        private readonly ClassroomsListView m_ClassroomsList;
        private readonly PresentationsListView m_PresentationsList;
        private readonly PresentationButtonPanel m_PresentationButtonPanel;

        public ClassroomBrowserControl(PresenterModel model) {
            this.SuspendLayout();

            this.m_Model = model;

            this.m_ClassroomsList = new ClassroomsListView(this.m_Model);
            this.m_PresentationsList = new PresentationsListView(this.m_Model);
            this.m_PresentationButtonPanel = new PresentationButtonPanel(this.m_Model);

            this.Controls.Add(this.m_ClassroomsList);
            this.Controls.Add(this.m_PresentationButtonPanel);
            this.Controls.Add(this.m_PresentationsList);

            // The model has no concept of the currently selected classroom for the purposes of
            // this UI (nor should it), so we're responsible for updating the buttons directly.
            this.m_PresentationsList.SelectedIndexChanged += new EventHandler(HandlePresentationSelectionChanged);
            this.HandlePresentationSelectionChanged(null, null);

            this.ResumeLayout(false);
        }

        protected override void OnLayout(LayoutEventArgs levent) {
            Rectangle rect = this.ClientRectangle;

            this.m_ClassroomsList.Location = new Point(0, 0);
            this.m_ClassroomsList.Size = new Size(rect.Width, (rect.Height / 2) - (this.m_PresentationButtonPanel.Height / 2));

            this.m_PresentationButtonPanel.Location = new Point(
                Math.Max(0, (rect.Width / 2) - (this.m_PresentationButtonPanel.Width / 2)),
                this.m_ClassroomsList.Bottom + 2);

            this.m_PresentationsList.Location = new Point(0, this.m_PresentationButtonPanel.Bottom + 2);
            this.m_PresentationsList.Size = new Size(rect.Width, rect.Height - this.m_PresentationsList.Top);

            this.Bounds = this.Bounds;
        }

        private void HandlePresentationSelectionChanged(object sender, EventArgs e) {
            ParticipantModel pModel = null;
            if( this.m_PresentationsList.SelectedItems.Count > 0 && 
                this.m_PresentationsList.SelectedItems[0] is DisposableListViewItem )
                pModel = (ParticipantModel)this.m_PresentationsList.SelectedItems[0].Tag;
            
            this.m_PresentationButtonPanel.Association = pModel;
        }

        private class PresentationButtonPanel : Panel {
            private PresenterModel m_Model;

            internal StartJoinButton m_StartJoinButton;

            public PresentationButtonPanel(PresenterModel model) {
                this.m_Model = model;

                this.SuspendLayout();

                this.m_StartJoinButton = new StartJoinButton( this.m_Model );

                Size size = new Size( this.m_StartJoinButton.Width, this.m_StartJoinButton.Height);
                this.Size = new Size( size.Width+20, size.Height+20);

                this.Padding = new Padding( 10 );
                this.m_StartJoinButton.Dock = DockStyle.Fill;

                this.Controls.Add(this.m_StartJoinButton);

                this.ResumeLayout(false);
            }

            internal ParticipantModel Association {
                set { this.m_StartJoinButton.Association = value; }
            }
        }
    }
}
