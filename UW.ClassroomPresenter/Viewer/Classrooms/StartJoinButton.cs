// $Id: SetAssociationButton.cs 776 2005-09-21 20:57:14Z pediddle $

using System;
using System.Diagnostics;
using System.Threading;
using System.Windows.Forms;
using System.Drawing;

using UW.ClassroomPresenter.Decks;
using UW.ClassroomPresenter.Model;
using UW.ClassroomPresenter.Model.Network;
using UW.ClassroomPresenter.Model.Presentation;


namespace UW.ClassroomPresenter.Viewer.Classrooms {
    class StartJoinButton : Button {
        private readonly PresenterModel m_Model;
        private ParticipantModel m_Association;
        private string text1 = Strings.StartNewPresentation;
        private string text2 = Strings.JoinSelectedPresentation;

        public StartJoinButton( PresenterModel model ) {
            this.m_Model = model;
            this.Text = text1;
            this.AutoSize = false;
            this.Font = new Font( this.Font.FontFamily, this.Font.Size * 4 / 3 );

            //Calculate the largest size for this button
            SizeF size1, size2; 
            using( Graphics g = this.CreateGraphics() ) {
                size1 = g.MeasureString( text1, this.Font );
                size2 = g.MeasureString( text2, this.Font );
                size1.Height += this.FontHeight;
                size1.Width += this.FontHeight;
                size2.Height += this.FontHeight;
                size2.Width += this.FontHeight;
            }
            this.Size = new Size( Math.Max( (int)size1.Width, (int)size2.Width ), Math.Max( (int)size1.Height, (int)size2.Height ) );
        }

        public ParticipantModel Association {
            get { return this.m_Association; }
            set {
                Debug.Assert( !this.InvokeRequired );
                this.m_Association = value;
                if( value != null )
                    this.Text = text2;
                else
                    this.Text = text1;
            }
        }

        protected override void OnClick( EventArgs e ) {
            base.OnClick( e );

            if( this.m_Association != null ) {
                // Set the role accordingly
                using( Synchronizer.Lock( this.m_Model.Participant ) ) {
                    if( this.m_Model.Participant.Role is InstructorModel || this.m_Model.Participant.Role == null )
                        this.m_Model.Participant.Role = new StudentModel( Guid.NewGuid() );
                }

                // Set the network association
                using( Synchronizer.Lock( this.m_Model.Network.SyncRoot ) ) {
                    this.m_Model.Network.Association = this.Association;
                }
            } else {
                // Instead Start an Empty Presentation
                StartJoinButton.StartEmptyPresentation( this.m_Model );
            }
        }

        public static void StartEmptyPresentation( PresenterModel model ) {
            // Make sure we have a suitable RoleModel for broadcasting a presentation.
            InstructorModel instructor;
            using(Synchronizer.Lock(model.Participant.SyncRoot)) {
                instructor = model.Participant.Role as InstructorModel;
                if(instructor == null) {
                    // Make the participant representing this user an Instructor,
                    // which tells the ConnectionManager to broadcast the presentation
                    // once it's added to the classroom.
                    instructor = new InstructorModel(Guid.NewGuid());
                    model.Participant.Role = instructor;
                }
            }

            // Create the presentation.
            // TODO: Find something useful to use as the Presentation's HumanName.
            PresentationModel pres = new PresentationModel(Guid.NewGuid(), model.Participant, "Untitled Presentation",true);

            using(Synchronizer.Lock(model.Network.SyncRoot)) {
                // Associate the participant with itself.  This removes any existing association
                // (since an Instructor should not be processing broadcasts from other clients),
                // and also causes the NetworkAssociationService to copy anything we do to the
                // InstructorModel to the WorkspaceModel.
                model.Network.Association = model.Participant;
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
