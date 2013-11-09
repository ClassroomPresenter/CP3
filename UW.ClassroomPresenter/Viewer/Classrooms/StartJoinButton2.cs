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
    public class StartJoinButton2 : Button {
        private readonly PresenterModel m_Model;
        private ParticipantModel m_Association;
        private string text1 = Strings.StartNewPresentation;
        private string text2 = Strings.JoinSelectedPresentation;
        private StartupForm m_Startup;
        public bool clicked;

        /// The role change dispatcher
        /// </summary>
        private readonly EventQueue.PropertyEventDispatcher m_RoleChangedDispatcher;


        public StartJoinButton2(PresenterModel model, StartupForm stup, bool compact) {
            this.m_Model = model;
            this.m_Startup = stup;
            this.Text = text1;
            this.AutoSize = false;
            this.FlatStyle = FlatStyle.System;
            this.Font = Model.Viewer.ViewerStateModel.StringFont1;
            this.clicked = false;



            //Calculate the largest size for this button
            SizeF size1, size2;
            using (Graphics g = this.CreateGraphics()) {
                size1 = g.MeasureString(text1, this.Font);
                size2 = g.MeasureString(text2, this.Font);
                size1.Width += 15;
                size2.Width += 10;
                if (compact) { 
                    size1.Height += 5;
                    size2.Height += 5;
                }
                else { 
                    size1.Height += 10;
                    size2.Height += 15;              
                }
            }
            this.Size = new Size(Math.Max((int)size1.Width, (int)size2.Width), Math.Max((int)size1.Height, (int)size2.Height));

            this.DialogResult = DialogResult.OK;

             this.m_RoleChangedDispatcher = new EventQueue.PropertyEventDispatcher(this.m_Startup.m_EventQueue, new PropertyEventHandler(this.TextOnRoleChange));
             this.m_Model.ViewerState.Changed["iRole"].Add(this.m_RoleChangedDispatcher.Dispatcher);
             this.m_RoleChangedDispatcher.Dispatcher(null, null);

            }
        /// Destroy resources, i.e. stop listening to model changes
        /// </summary>
        /// <param name="disposing">True if we are destroying all objects</param>
        protected override void Dispose(bool disposing) {
            if (disposing) {
                //this.m_Model.ViewerState.Changed["iRole"].Remove(this.m_RoleChangedDispatcher.Dispatcher);
                }

            base.Dispose(disposing);
            }

        public void TextOnRoleChange(object sender, PropertyEventArgs args) {
            using (Synchronizer.Lock(this.m_Model.ViewerState)) {
                if (this.m_Model.ViewerState.iRole == 0 || //disconnected
                    this.m_Model.ViewerState.iRole == 2) { //instructor
                    this.Text = text1;
                    }
                else {
                    this.Text = text2;
                    }
                }
            }

        /*
              public void HandleEnabledOnRoleChange(object sender, PropertyEventArgs args)
                  {
                  using(Synchronizer.Lock(this.m_Model.ViewerState.SyncRoot))
                      {
                  switch(this.m_Model.ViewerState.iRole)
                      {
                          //Natalie - figure out a 
                      case 0: //Disconnected
                          this.Association = null;
                      case 1: //Viewer
                      case 2: //Presenter
                          this.Association = nulll;
                      case 3: //Public Display
                      }
                      }
                  HandleEnabled();
                  }
              */
        public void HandleEnabledAndAssociation(ParticipantModel association) {
            /*if (this.InvokeRequired)
                {
                this.Invoke(new HandleEnabledDelegate(HandleEnabled));
                }
            else
                {
                HandleEnabled();
                }
            */

            this.Enabled = false;
            this.Association = association;
            HandleEnabled();
            }
        public delegate void HandleEnabledDelegate();


        public void HandleEnabled() {
            this.Enabled = false;
            bool ParticipantIsNull = false;
            using (Synchronizer.Lock(this.m_Model.Participant.SyncRoot)) {
                ParticipantIsNull = (this.m_Model.Participant.Role == null);
                }

            int currentRole = 0;
            using (Synchronizer.Lock(this.m_Model.ViewerState.SyncRoot)) {
                currentRole = this.m_Model.ViewerState.iRole;
                }
            if (currentRole == 0)//Disconnected
                { this.Enabled = true; }
            else if (ParticipantIsNull) {
                this.Enabled = false;
                }
            else if (currentRole == 2)//Instructor
                        {
                this.Enabled = false;
                if (this.m_Startup.m_Connection.m_ManualConnectionPanel == null)
                    this.Enabled = false; //This will not work when we have registry persistence...  probably should always make the ManualConnectionGroup even if we don't display
                else
                    for (int i = 0; i < this.m_Startup.m_Connection.m_ManualConnectionPanel.Controls.Count; i++) {
                        if (this.m_Startup.m_Connection.m_ManualConnectionPanel.Controls[i] is RadioButton) {
                            using (Synchronizer.Lock(((ClassroomModel)(this.m_Startup.m_Connection.m_ManualConnectionPanel.Controls[i].Tag)).SyncRoot)) {
                                if (((ClassroomModel)(this.m_Startup.m_Connection.m_ManualConnectionPanel.Controls[i].Tag)).Connected)
                                    this.Enabled = true;
                                }
                            }
                        }
                }
            else if ((this.m_Startup.m_Connection != null) &&
                    (this.m_Startup.m_Connection.m_PresentationsPanel != null)) {//should only be null if we are starting up in which case we know no presentation is selected anyway so Enabled should stay false
                for (int i = 0; i < this.m_Startup.m_Connection.m_PresentationsPanel.Controls.Count; i++) {
                    if (this.m_Startup.m_Connection.m_PresentationsPanel.Controls[i] is RadioButton) {
                        if (((RadioButton)(this.m_Startup.m_Connection.m_PresentationsPanel.Controls[i])).Checked)
                            this.Enabled = true;
                        }
                    }
                }
            }


        public ParticipantModel Association {
            get { return this.m_Association; }
            set {
                this.Enabled = false;
                Debug.Assert(!this.InvokeRequired);
                this.m_Association = value;
               /* if (value != null)
                    this.Text = text2;
                else
                    this.Text = text1;*/
                }
            }

        protected override void OnClick(EventArgs e) {
            base.OnClick(e);
            this.clicked = true;

            if (this.m_Association != null) {
                // Set the role accordingly
                using (Synchronizer.Lock(this.m_Model.Participant)) {
                    if (this.m_Model.Participant.Role is InstructorModel || this.m_Model.Participant.Role == null)
                        this.m_Model.Participant.Role = new StudentModel(Guid.NewGuid());
                    }

                // Set the network association
                using (Synchronizer.Lock(this.m_Model.Network.SyncRoot)) {
                    this.m_Model.Network.Association = this.Association;
                    }
                } else {
                // Instead Start an Empty Presentation
                StartJoinButton2.StartEmptyPresentation(this.m_Model);
                }
            }

        public static void StartEmptyPresentation(PresenterModel model) {
            // Make sure we have a suitable RoleModel for broadcasting a presentation.
            InstructorModel instructor;
            using (Synchronizer.Lock(model.Participant.SyncRoot)) {
                instructor = model.Participant.Role as InstructorModel;
                if (instructor == null) {
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

            using (Synchronizer.Lock(model.Network.SyncRoot)) {
                // Associate the participant with itself.  This removes any existing association
                // (since an Instructor should not be processing broadcasts from other clients),
                // and also causes the NetworkAssociationService to copy anything we do to the
                // InstructorModel to the WorkspaceModel.
                model.Network.Association = model.Participant;
                }

            // Here's the kicker, which triggers the viewer to hide the classroom browser
            // and start displaying the presentation.  The NetworkAssociationService copies
            // the presentation to the WorkspaceModel, which causes it to be displayed by the UI.
            using (Synchronizer.Lock(instructor.SyncRoot)) {
                instructor.CurrentPresentation = pres;
                }
            }
        }
    }
