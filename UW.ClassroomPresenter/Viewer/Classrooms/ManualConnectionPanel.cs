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
    /// <summary>
    /// Displays a list of classrooms available via a given protocol,
    /// and allows the user to connect to a classroom by selecting
    /// one or more of them from the list.
    /// </summary>
    /// 
    public class ManualConnectionPanel : Panel {
        private readonly ControlEventQueue m_EventQueue;
        private readonly PresenterModel m_Model;
        private readonly ProtocolCollectionHelper m_ProtocolCollectionHelper;
        private bool m_Disposed;
        public StartupForm m_Startup;
        public Label UnicastLabel;
        public Label MulticastLabel;
        public ConnectTCPRadioButton TCPRadioButton;
        public ReenterServerButton Reenter;

        #region Construction & Destruction

        public ManualConnectionPanel(PresenterModel model, StartupForm stup) {
            if (model == null)
                throw new ArgumentNullException("model");

            this.m_EventQueue = new ControlEventQueue(this);
            this.m_Model = model;
            this.m_Startup = stup;

            //this.Text = "Manual Connections";
            this.Font = Model.Viewer.ViewerStateModel.StringFont;
            this.Location = new Point(10, 10);
            this.Size = new Size(250, 250);

            this.CreateHandle();

            this.BorderStyle = BorderStyle.Fixed3D;
            this.AutoScroll = true;

            Label label1 = new Label();
            label1.FlatStyle = FlatStyle.System;
            label1.Font = Model.Viewer.ViewerStateModel.StringFont2;
            label1.Text = Strings.ManualConnectionVenues;
            label1.Location = new Point(10, 5);
            label1.Size = new Size(this.Width - 20, label1.Font.Height);
            this.Controls.Add(label1);

            this.UnicastLabel = new Label();
            this.UnicastLabel.FlatStyle = FlatStyle.System;
            this.UnicastLabel.Font = Model.Viewer.ViewerStateModel.StringFont2;
            this.UnicastLabel.Text = Strings.Unicast;
            this.UnicastLabel.Location = new Point(10, (int)(6*this.UnicastLabel.Font.Height) + 5);
            this.UnicastLabel.Size = new Size(this.Width - 20, this.UnicastLabel.Font.Height); 
            this.Controls.Add(this.UnicastLabel);

#if RTP_BUILD
            this.MulticastLabel = new Label();
            this.MulticastLabel.Text = Strings.MultiCast;
            this.MulticastLabel.FlatStyle = FlatStyle.System;
            this.MulticastLabel.Font = Model.Viewer.ViewerStateModel.StringFont2;
            this.MulticastLabel.Location = new Point(10, (int)(2 * this.MulticastLabel.Font.Height));
            this.MulticastLabel.Size = new Size(this.Width - 20, this.MulticastLabel.Font.Height); 
            this.Controls.Add(this.MulticastLabel);

#else
            this.UnicastLabel.Location = new Point(10, label1.Bottom + 5);

#endif


            
            //Add the TCP dealie to the list
            bool isInstructor = false;
            using (Synchronizer.Lock(this.m_Model.Participant.SyncRoot)) {
                   isInstructor = (this.m_Model.Participant.Role is InstructorModel);
            }
            if (!isInstructor) {
                this.TCPRadioButton = new ConnectTCPRadioButton(m_EventQueue, m_Model, this, 
                    new Point(10, this.UnicastLabel.Bottom + 5), this.Width - 60);
                this.Controls.Add(this.TCPRadioButton);//Add click handlers, etc
                this.Reenter = new ReenterServerButton(this.TCPRadioButton);
                this.Reenter.Location = new Point(this.TCPRadioButton.Right, this.TCPRadioButton.Top);
                this.Reenter.Width = 100;
                this.Controls.Add(this.Reenter);
            }

            // Set up a helper to add other classrooms
            this.m_ProtocolCollectionHelper = new ProtocolCollectionHelper(this, this.m_Model.Network);

            }

        protected override void Dispose(bool disposing) {
            if (this.m_Disposed) return;

            try {
                if (disposing) {
                    this.m_ProtocolCollectionHelper.Dispose();

                    this.m_EventQueue.Dispose();
                    }
                } finally {
                base.Dispose(disposing);
                }

            this.m_Disposed = true;
            }

        #endregion Construction & Destruction

        /// <summary>
        /// Update the positions of the advertised classrooms
        /// </summary>
        private void UpdateLayout() {
            int dynamicClassroomCount = 0;
            int firstDynamicClassroomYPos = this.UnicastLabel.Bottom + 15 + this.UnicastLabel.Font.Height;
            foreach (Control c in this.Controls) {
                if (c is ManualConnectionRadioButton) {
                    if (((ManualConnectionRadioButton)c).Classroom.ClassroomModelType == ClassroomModelType.Dynamic) {
                        c.Location = new Point(10, firstDynamicClassroomYPos + (dynamicClassroomCount * c.Font.Height * 4));
                        dynamicClassroomCount++;
                    }
                }
            }
        }

        public void DisconnectAllButtons() {
            foreach (Control c in this.Controls) {
                if (c is ManualConnectionRadioButton) {
                    ManualConnectionRadioButton rb = c as ManualConnectionRadioButton;

                    using (Synchronizer.Lock(rb.Classroom.SyncRoot)) {
                        rb.Classroom.Connected = false;
                        }
                    }
                else if (c is ConnectTCPRadioButton) {
                    ConnectTCPRadioButton tcp = c as ConnectTCPRadioButton;
                    tcp.Checked = false; //this should trigger disconnection, but we are closing the app, so it might not work.  Can't test until TCPRadioButton is fixed.
                    }
                }
            }

        private class ProtocolCollectionHelper : PropertyCollectionHelper {
            private readonly ManualConnectionPanel m_Parent;

            public ProtocolCollectionHelper(ManualConnectionPanel parent, NetworkModel network)
                : base(parent.m_EventQueue, network, "Protocols") {
                this.m_Parent = parent;
                base.Initialize();
                }

            protected override void Dispose(bool disposing) {
                try {
                    if (disposing)
                        foreach (ClassroomCollectionHelper helper in this.Tags)
                            helper.Dispose();
                    } finally {
                    base.Dispose(disposing);
                    }
                }

            protected override object SetUpMember(int index, object member) {
                return new ClassroomCollectionHelper(this.m_Parent, ((ProtocolModel)member));
                }

            protected override void TearDownMember(int index, object member, object tag) {
                if (tag != null)
                    ((ClassroomCollectionHelper)tag).Dispose();
                }
            }


        private class ClassroomCollectionHelper : PropertyCollectionHelper {
            private readonly ManualConnectionPanel m_Parent;

            public ClassroomCollectionHelper(ManualConnectionPanel parent, ProtocolModel protocol)
                : base(parent.m_EventQueue, protocol, "Classrooms") {
                this.m_Parent = parent;
                base.Initialize();
            }

            protected override void Dispose(bool disposing) {
                try {
                    if (disposing) {
                        for (int i = (this.m_Parent.Controls.Count) - 1; i > 0; i--)
                            this.m_Parent.Controls.Remove(this.m_Parent.Controls[i]);
                    }
                }
                finally {
                    base.Dispose(disposing);
                }
            }

            protected override object SetUpMember(int index, object member) {
                Debug.Assert(!this.m_Parent.InvokeRequired);
                ManualConnectionRadioButton button = new ManualConnectionRadioButton(this.m_Parent.m_EventQueue, (ClassroomModel)member, this.m_Parent, index);
                this.m_Parent.UpdateLayout();
                return button;
            }

            protected override void TearDownMember(int index, object member, object tag) {
                Debug.Assert(!this.m_Parent.InvokeRequired);
                ManualConnectionRadioButton rb = tag as ManualConnectionRadioButton;

                if (rb != null) {
                    using (Synchronizer.Lock(rb.Classroom.SyncRoot)) {
                        if (rb.Classroom.HumanName.Length > 7 &&
                            rb.Classroom.HumanName.Substring(0, 8) == "Join the") {
                            int UDPControls = ((ManualConnectionPanel)rb.Parent).m_Startup.m_UDPPanel.Controls.Count;
                            int removeIndex = -1;
                            for (int j = 0; j < UDPControls; j++) {
                                if (((ManualConnectionPanel)rb.Parent).m_Startup.m_UDPPanel.Controls[j].Text == rb.Classroom.HumanName) {
                                    removeIndex = j;
                                }
                            }
                            if (removeIndex != -1) {
                                ((ManualConnectionPanel)rb.Parent).m_Startup.m_UDPPanel.Controls.RemoveAt(removeIndex);
                                ((ManualConnectionPanel)rb.Parent).m_Startup.m_UDPPanel.UpdateLayout();
                            }
                            if (rb.Classroom.Connected) {
                                m_Parent.m_Startup.InvisibleConnectedClassroom = rb.Classroom;
                            }
                        }
                    
                    }

                    rb.Parent = null;
                    m_Parent.Controls.Remove(rb);
                    rb.Dispose();
                    this.m_Parent.UpdateLayout();
                }
            }
        }

        public class ManualConnectionRadioButton : RadioButton {
            private readonly ControlEventQueue m_EventQueue;
            private readonly EventQueue.PropertyEventDispatcher m_HumanNameChangedDispatcher;

            private readonly EventQueue.PropertyEventDispatcher m_ViewerStateRoleListener;
            public int spacer = 25;

            private bool m_Disposed;
            int index;

            private ClassroomModel m_Classroom;

            public ManualConnectionRadioButton(ControlEventQueue dispatcher, ClassroomModel classroom, ManualConnectionPanel parent, int i) {
                if (classroom == null)
                    throw new ArgumentNullException("classroom",
                        "The ClassroomModel associated with this ManualConnectionRadioButton cannot be null.");


                this.Font = Model.Viewer.ViewerStateModel.StringFont1;
                this.m_EventQueue = dispatcher;
                this.m_Classroom = classroom;
                this.Tag = classroom;
                this.Parent = parent;
                this.Parent.Controls.Add(this);
                this.index = i;
                int count = this.Parent.Controls.Count;
#if RTP_BUILD
                this.Location = new Point(10, 
                    (int)(((ManualConnectionPanel)(this.Parent)).MulticastLabel.Bottom +5     
                        + 1.5*this.Font.Height * (this.index)));
#endif

                this.Size = new Size(this.Parent.Width-15, this.Font.Height);

                string classroomName = "";
                using (Synchronizer.Lock(this.Classroom.SyncRoot)) {
                    classroomName = this.Classroom.HumanName;
                    }


                using (Synchronizer.Lock(((ManualConnectionPanel)(this.Parent)).m_Model.ViewerState.SyncRoot)) {
                    if (classroomName == ((ManualConnectionPanel)(this.Parent)).m_Model.ViewerState.ManualConnectionButtonName) {
                        this.Checked = true;
                        }
                    }
                    
                if (classroomName == Strings.InstructorStartTCPServer) {

                    this.Location = new Point(10,
                        (int)(((ManualConnectionPanel)(this.Parent)).UnicastLabel.Bottom 
                            + 4 * this.Font.Height * (this.index)));
                    this.Size = new Size(this.Parent.Width - 15, 3*this.Font.Height);
                        //this.Location = new Point(10, 200 + 30 * (this.index));
                        //this.Size = new Size(180, 30);
                        using (Synchronizer.Lock(((ManualConnectionPanel)(this.Parent)).m_Model.Participant.SyncRoot)) {
                            if (!(((ManualConnectionPanel)(this.Parent)).m_Model.Participant.Role is InstructorModel)) {
                                this.Visible = false;
                                this.Enabled = false;
                                }
                            }
                    }
                if (m_Classroom.ClassroomModelType == ClassroomModelType.Dynamic) {
                    //Note the locations of the radio buttons for the advertised (Dynamic) classrooms are set by the ManualConnectionPanel.UpdateLayout method

                    this.Size = new Size(this.Parent.Width - 20, 4 * this.Font.Height);

                    //This is a hack...  figure out why added more than once.
                    bool isAlreadyAdded = false;
                    int UDPControls = ((ManualConnectionPanel)this.Parent).m_Startup.m_UDPPanel.Controls.Count;
                    for (int j = 0; j < UDPControls; j++) {
                        if (((ManualConnectionPanel)this.Parent).m_Startup.m_UDPPanel.Controls[j].Text == classroomName) {
                            isAlreadyAdded = true;
                        }
                    }

                    if (!isAlreadyAdded) {
                        ((ManualConnectionPanel)this.Parent).m_Startup.m_UDPPanel.Controls.Add(new UDPPresentationRadioButton(this.m_Classroom, ((ManualConnectionPanel)this.Parent).m_Startup, UDPControls, this.m_EventQueue));
                        ((ManualConnectionPanel)this.Parent).m_Startup.m_UDPPanel.UpdateLayout();
                    }

                    using (Synchronizer.Lock(((ManualConnectionPanel)(this.Parent)).m_Model.Participant.SyncRoot)) {
                        if (((ManualConnectionPanel)(this.Parent)).m_Model.Participant.Role is InstructorModel) {
                            this.Visible = false;
                            }
                        }
                    }


                //If the role changes we should disconnect from our classroom
                this.m_ViewerStateRoleListener = new EventQueue.PropertyEventDispatcher(((ManualConnectionPanel)(this.Parent)).m_EventQueue,
                            new PropertyEventHandler(this.HandleViewerStateRoleChanged));
                ((ManualConnectionPanel)(this.Parent)).m_Model.ViewerState.Changed["iRole"].Add(this.m_ViewerStateRoleListener.Dispatcher);



                this.m_HumanNameChangedDispatcher = new EventQueue.PropertyEventDispatcher(this.m_EventQueue, new PropertyEventHandler(this.HandleHumanNameChanged));

                this.Classroom.Changed["HumanName"].Add(this.m_HumanNameChangedDispatcher.Dispatcher);

                this.CheckedChanged += new EventHandler(HandleCheckedChanged);

                this.HandleHumanNameChanged(this.Classroom, null);

                }



            public ClassroomModel Classroom {
                get { return this.m_Classroom; }
                }


            protected override void Dispose(bool disposing) {
                if (!this.m_Disposed) {
                    if (disposing) {
                        Debug.Assert(this.Classroom != null);
                        if (this.Classroom != null && this.m_HumanNameChangedDispatcher != null) {
                            this.Classroom.Changed["HumanName"].Remove(this.m_HumanNameChangedDispatcher.Dispatcher);
                            }
                        this.m_Disposed = true;
                        }
                    }
                }

            private void HandleHumanNameChanged(object sender, PropertyEventArgs args_) {
                Debug.Assert(this.Classroom != null);
                using (Synchronizer.Lock(this.Classroom.SyncRoot)) {
                    this.Text = this.Classroom.HumanName;
                    }
                }

            private void HandleViewerStateRoleChanged(object sender, PropertyEventArgs args){
                Debug.Assert(this.Classroom != null);
                using (Synchronizer.Lock(this.Classroom.SyncRoot)) {
                    this.Classroom.Connected = false;
                    }
                }


            //OnClick for radiobuttons
            private void HandleCheckedChanged(object sender, EventArgs args_) {
                using (Synchronizer.Lock(((ManualConnectionRadioButton)(sender)).Classroom.SyncRoot)) {
                    ((ManualConnectionRadioButton)(sender)).Classroom.Connected = ((ManualConnectionRadioButton)(sender)).Checked;
                    }
                //StartJoinButton2 button = ((ManualConnectionPanel)(this.Parent)).m_Startup.m_StartJoinButton;
                /* ((ManualConnectionPanel)(this.Parent)).m_Startup.m_StartJoinButton.Association = null;
                 if (((ManualConnectionPanel)(this.Parent)).m_Startup.m_StartJoinButton.InvokeRequired)
                     {
                     ((ManualConnectionPanel)(this.Parent)).m_Startup.m_StartJoinButton.Invoke(new StartJoinButton2.HandleEnabledDelegate(((ManualConnectionPanel)(this.Parent)).m_Startup.m_StartJoinButton.HandleEnabled));
                     }
                 else
                     {
                     ((ManualConnectionPanel)(this.Parent)).m_Startup.m_StartJoinButton.HandleEnabled();
                     }*/
                ((ManualConnectionPanel)(this.Parent)).m_Startup.m_StartJoinButton.HandleEnabledAndAssociation(null);
                /* using (Synchronizer.Lock(this.m_Model.ViewerState.SyncRoot))
                     {
                     using (Synchronizer.Lock(classroom.SyncRoot))
                         {
                         if(rb.Checked)
                             this.m_Model.ViewerState.ManualConnectionButtonName = classroom.HumanName;
                         }
                     }*/
                }
            }
        }


    }

   
