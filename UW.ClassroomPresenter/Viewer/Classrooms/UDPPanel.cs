using System;
using System.Collections;
using System.Diagnostics;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using System.Globalization;

using UW.ClassroomPresenter.Model;
using UW.ClassroomPresenter.Model.Network;
using UW.ClassroomPresenter.Model.Presentation;

namespace UW.ClassroomPresenter.Viewer.Classrooms {
    public class UDPPanel : Panel {
        public Label nameLabel;
        public UDPPanel() {
            this.Size = new Size(622, 240);
            this.Location = new Point(5, 5);
            this.Font = Model.Viewer.ViewerStateModel.StringFont2;
            this.BorderStyle = BorderStyle.Fixed3D;
            this.Dock = DockStyle.Fill;
            this.AutoScroll = true;
            this.nameLabel = new Label();
            this.nameLabel.FlatStyle = FlatStyle.System;
            this.nameLabel.Text = Strings.AvailablePresentations;
            this.nameLabel.Size = new Size(this.Width - 20, this.nameLabel.Font.Height + 10);
            this.nameLabel.Location = new Point(5, 5);
            this.Controls.Add(this.nameLabel);
            }

        /// <summary>
        /// After a RadioButton has been added or removed, call this to update positions.
        /// </summary>
        public void UpdateLayout() {
            int index = 0;
            foreach (Control c in this.Controls) {
                if (c is UDPPresentationRadioButton) {
                    c.Location = new Point(10, this.nameLabel.Bottom + index * c.Font.Height * 3);
                    index++;
                }   
            }
        }
        }
    public class UDPPresentationRadioButton : RadioButton {
        public ClassroomModel m_Classroom;
        public int index;
        public StartupForm m_Startup;
        public ControlEventQueue m_EventQueue;
        private UDPPresentationCollectionHelper m_PresentationHelper;
        private readonly EventQueue.PropertyEventDispatcher m_ConnectedChangedDispatcher;

        public UDPPresentationRadioButton(ClassroomModel classroom, StartupForm stup, int i, ControlEventQueue eventqueue) {
            this.m_Classroom = classroom;
            this.m_EventQueue = eventqueue;
            this.m_Startup = stup;
            this.index = i;

            this.FlatStyle = FlatStyle.System;
            this.Font = Model.Viewer.ViewerStateModel.StringFont1;
            this.Location = new Point(10, 
                this.m_Startup.m_UDPPanel.nameLabel.Bottom + this.index*this.Font.Height*3 - (2*this.Font.Height));
            this.Size = new Size(this.m_Startup.m_UDPPanel.Width -20, 3*this.Font.Height);
            using(Synchronizer.Lock(m_Classroom.SyncRoot))
                this.Text = this.m_Classroom.HumanName;
            this.CheckedChanged += this.HandleClick;

            this.m_ConnectedChangedDispatcher = new EventQueue.PropertyEventDispatcher(this.m_EventQueue, new PropertyEventHandler(this.HandleConnectedChanged));
            this.m_Classroom.Changed["Connected"].Add(this.m_ConnectedChangedDispatcher.Dispatcher);

            this.HandleConnectedChanged(this.m_Classroom, null);

            }

        /// Destroy resources, i.e. stop listening to model changes
        /// </summary>
        /// <param name="disposing">True if we are destroying all objects</param>
        protected override void Dispose(bool disposing) {
            if (disposing) {
                using (Synchronizer.Lock(this.m_Classroom.SyncRoot)) {
                    this.m_Classroom.Changed["Connected"].Remove(this.m_ConnectedChangedDispatcher.Dispatcher);
                    }
                }
            base.Dispose(disposing);
            }

        public void HandleClick(Object sender, EventArgs e) {

            if (m_Startup.InvisibleConnectedClassroom != null) {
                using (Synchronizer.Lock(m_Startup.InvisibleConnectedClassroom.SyncRoot)) {
                    m_Startup.InvisibleConnectedClassroom.Connected = false;
                    m_Startup.InvisibleConnectedClassroom = null;
                }
            }

            using (Synchronizer.Lock(this.m_Classroom.SyncRoot)) {
                this.m_Classroom.Connected = this.Checked;
                if (this.Checked) {
                    for (int i = 0; i < this.m_Startup.m_Connection.m_PresentationsPanel.Controls.Count; i++) {
                        if (this.m_Startup.m_Connection.m_PresentationsPanel.Controls[i] is RadioButton) {
                            using (Synchronizer.Lock(((PresentationRadioButton)(this.m_Startup.m_Connection.m_PresentationsPanel.Controls[i])).m_Classroom.SyncRoot)) {
                                if (!(this.m_Classroom == ((PresentationRadioButton)(this.m_Startup.m_Connection.m_PresentationsPanel.Controls[i])).m_Classroom)) {
                                    ((RadioButton)(this.m_Startup.m_Connection.m_PresentationsPanel.Controls[i])).Checked = false;
                                    } else {
                                    ((RadioButton)(this.m_Startup.m_Connection.m_PresentationsPanel.Controls[i])).Checked = true;
                                    }
                                }
                            }
                        }
                    for (int i = 0; i < this.m_Startup.m_Connection.m_ManualConnectionPanel.Controls.Count; i++) {
                        if (this.m_Startup.m_Connection.m_ManualConnectionPanel.Controls[i] is ManualConnectionPanel.ManualConnectionRadioButton) {
                            using (Synchronizer.Lock(((ManualConnectionPanel.ManualConnectionRadioButton)(this.m_Startup.m_Connection.m_ManualConnectionPanel.Controls[i])).Classroom.SyncRoot)) {
                                if (!(this.m_Classroom == ((ManualConnectionPanel.ManualConnectionRadioButton)(this.m_Startup.m_Connection.m_ManualConnectionPanel.Controls[i])).Classroom)) {
                                    ((RadioButton)(this.m_Startup.m_Connection.m_ManualConnectionPanel.Controls[i])).Checked = false;
                                } else {
                                    ((RadioButton)(this.m_Startup.m_Connection.m_ManualConnectionPanel.Controls[i])).Checked = true;
                                }
                            }
                        }
                    }

                    this.m_PresentationHelper = new UDPPresentationCollectionHelper(this, this.m_Classroom);//This will set the association of the StartJoinButton
                    }
                }
            }

        //This function handles the case where a classroom in the Advanced page gets selected.  In this case we want to uncheck this button
        public void HandleConnectedChanged(object sender, EventArgs args) {
            using (Synchronizer.Lock(this.m_Classroom.SyncRoot)) {
                if (!(this.m_Classroom.Connected)) {
                    this.Checked = false;
                    }
                }
            }

        private class UDPPresentationCollectionHelper : PropertyCollectionHelper {
            private readonly UDPPresentationRadioButton m_Parent;

            public UDPPresentationCollectionHelper(UDPPresentationRadioButton parent, ClassroomModel classroom)
                : base(parent.m_EventQueue, classroom, "Presentations") {
                this.m_Parent = parent;
                base.Initialize();
                }

            protected override void Dispose(bool disposing) {
                try {
                    if (disposing) {

                        }
                    } finally {
                    base.Dispose(disposing);
                    }
                }

            protected override object SetUpMember(int index, object member) {
                Debug.Assert(!this.m_Parent.InvokeRequired);
                //this.m_Parent.m_Startup.m_StartJoinButton.Association = ((PresentationModel)member).Owner;
                this.m_Parent.m_Startup.m_StartJoinButton.HandleEnabledAndAssociation(((PresentationModel)member).Owner);
                return (PresentationModel)member;
                }

            protected override void TearDownMember(int index, object member, object tag) {

                }
            }

        }
    }
    
