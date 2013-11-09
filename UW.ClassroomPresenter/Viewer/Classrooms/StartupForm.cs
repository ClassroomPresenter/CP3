
using System;
using System.Drawing;
using System.Windows.Forms;

using System.Threading;
using System.Diagnostics;


using UW.ClassroomPresenter.Model;
using UW.ClassroomPresenter.Model.Network;
using UW.ClassroomPresenter.Model.Presentation;
using UW.ClassroomPresenter.Model.Workspace;


namespace UW.ClassroomPresenter.Viewer.Classrooms {
    public class StartupForm : Form {

        public PresenterModel m_Model;
        public ConnectionHelper m_Connection;
        public ControlEventQueue m_EventQueue;
        public EnableExternalMonitorCheckbox m_EnableExternalMonitorCheckbox;
        public StartJoinButton2 m_StartJoinButton;
        public UDPPanel m_UDPPanel;
        public StartupTabControl m_TabControl;
        public GroupBox m_TabGroupBox;
        public GroupBox m_ButtonGroupBox;
        public RoleGroup m_RoleGroup;

        public Cp3HelpButton m_HelpButton;

        public int GroupBoxLeft = 20;
        public int GroupBoxSpace = 15;
        private readonly EventQueue.PropertyEventDispatcher m_RoleChangedDispatcher;

        private bool short_mode = false;

        /// <summary>
        /// If the UDP broadcast classroom is connected, then the broadcast goes away, then the user selects a different one, we
        /// want to be sure that we disconnect the invisible one, so keep a reference here for that purpose.
        /// </summary>
        public ClassroomModel InvisibleConnectedClassroom = null;

        /// <summary>
        /// Constructs a properties form and hooks it into the model
        /// </summary>
        public StartupForm(PresenterModel model, ControlEventQueue dispatcher) {
            if (Screen.PrimaryScreen.Bounds.Height < 650) {
                //Alternate compact layout to work on netbooks or tablets with few vertical pixels
                short_mode = true;
                this.GroupBoxSpace = 10;
            }

            // Setup the display of the form
            this.SuspendLayout();
            this.m_Model = model;
            this.m_EventQueue = dispatcher;
            this.AutoScaleBaseSize = new Size(5, 13);
            if (short_mode) {
                this.ClientSize = new Size(640, 360);
            }
            else {
                this.ClientSize = new Size(640, 480);
            }
            this.FormBorderStyle = FormBorderStyle.Sizable;//FixedDialog;
            this.Font = Model.Viewer.ViewerStateModel.FormFont;
            this.AutoScroll = true;
            this.Name = "StartupForm";
            this.Text = Strings.StartupFormText;
            this.MinimizeBox = false;
            this.MaximizeBox = false;

            // Add the child controls

            this.AddConnection();
            if (short_mode) {
                this.m_RoleGroup = new RoleGroup(new Point(this.GroupBoxLeft, this.GroupBoxSpace), new Size(240, 83), this.m_Model, this, true);
            }
            else {
                this.m_RoleGroup = new RoleGroup(new Point(this.GroupBoxLeft, this.GroupBoxSpace), new Size(240, 110), this.m_Model, this, false);
            }
            this.m_RoleGroup.Font = Model.Viewer.ViewerStateModel.StringFont;
            this.Controls.Add(this.m_RoleGroup);


            //Tab control stuff
            this.m_UDPPanel = new UDPPanel();

            if (short_mode) {
                this.m_TabControl = new StartupTabControl(new Size(560, 150),
                    new Point(this.GroupBoxLeft, 15), 0, this, this.m_Model, this.m_EventQueue);
            }
            else { 
                this.m_TabControl = new StartupTabControl(new Size(560, 200),
                    new Point(this.GroupBoxLeft, 15), 0, this, this.m_Model, this.m_EventQueue);
         
            }
            this.m_TabGroupBox = new GroupBox();
            this.m_TabGroupBox.FlatStyle = FlatStyle.System;
            this.m_TabGroupBox.Font = Model.Viewer.ViewerStateModel.StringFont;
            this.m_TabGroupBox.Size = new Size(this.m_TabControl.Size.Width + 2 * this.GroupBoxLeft,
                this.m_TabControl.Size.Height + 2 * this.GroupBoxSpace);
            this.m_TabGroupBox.Location = new Point(this.GroupBoxLeft, this.m_RoleGroup.Bottom + this.GroupBoxSpace);
            this.Controls.Add(this.m_TabGroupBox);

            this.m_TabGroupBox.Controls.Add(this.m_TabControl);
            //end tab control stuff

            //Begin StartJoinButton stuff
            this.m_StartJoinButton = new StartJoinButton2(this.m_Model, this, short_mode);
            this.m_StartJoinButton.Location = new Point(this.GroupBoxLeft, this.GroupBoxSpace + 4);

            this.m_ButtonGroupBox = new GroupBox();
            this.m_ButtonGroupBox.FlatStyle = FlatStyle.System;
            this.m_ButtonGroupBox.Font = Model.Viewer.ViewerStateModel.StringFont;
            this.m_ButtonGroupBox.Size = new Size(this.m_StartJoinButton.Size.Width + 2 * this.GroupBoxLeft, 
                this.m_StartJoinButton.Size.Height + 2 * this.GroupBoxSpace);
            this.m_ButtonGroupBox.Location = new Point(this.GroupBoxLeft,
                this.m_TabGroupBox.Bottom + this.GroupBoxSpace);
            this.Controls.Add(this.m_ButtonGroupBox);

            this.m_ButtonGroupBox.Controls.Add(this.m_StartJoinButton);
            //End StartJoinButton stuff


            this.m_EnableExternalMonitorCheckbox = new EnableExternalMonitorCheckbox(
                new Point(300, ((this.m_RoleGroup.Top + this.m_RoleGroup.Bottom) / 2) - 20),
                new Size(300, 50), m_Model, this.m_EventQueue, this);
            this.Controls.Add(this.m_EnableExternalMonitorCheckbox);

            this.m_HelpButton = new Cp3HelpButton();
            this.m_HelpButton.Font = Model.Viewer.ViewerStateModel.StringFont1;
            this.m_HelpButton.Location = new Point((this.m_StartJoinButton.Right + this.Width) / 2, 
                (this.m_ButtonGroupBox.Top +this.m_ButtonGroupBox.Bottom)/2 -10);
            this.Controls.Add(this.m_HelpButton);

            this.m_StartJoinButton.HandleEnabled();//Can't call this until after other things are created. (ie until after AddConnection is called)

            this.m_RoleChangedDispatcher = new EventQueue.PropertyEventDispatcher(this.m_EventQueue, new PropertyEventHandler(this.OnRoleChanged));
            this.m_Model.ViewerState.Changed["iRole"].Add(this.m_RoleChangedDispatcher.Dispatcher);
            this.FormClosing += new FormClosingEventHandler(StartupForm_FormClosing);
            this.ResumeLayout();
        }

        void StartupForm_FormClosing(object sender, FormClosingEventArgs e) {
            if (!(this.m_StartJoinButton.clicked)) {
                using (Synchronizer.Lock(this.m_Model.Participant.SyncRoot)) {
                    if (!(this.m_Model.Participant.Role is InstructorModel)) {
                        this.m_Model.Participant.Role = new InstructorModel(Guid.NewGuid());
                    }
                }
                using (Synchronizer.Lock(this.m_Model.ViewerState.SyncRoot)) {
                    this.m_Model.ViewerState.iRole = 0;
                }
                if (this.m_Connection != null &&
                    this.m_Connection.m_ManualConnectionPanel != null) {
                    this.m_Connection.m_ManualConnectionPanel.DisconnectAllButtons();
                }
                this.m_StartJoinButton.Association = null;
                StartJoinButton2.StartEmptyPresentation(this.m_Model);
            }
        }

        protected override void Dispose(bool disposing) {
            if (disposing) {
                this.m_Model.ViewerState.Changed["iRole"].Remove(this.m_RoleChangedDispatcher.Dispatcher);
            }

            base.Dispose(disposing);
        }

        public void AddConnection() {
            int currentRole = 0;
            using (Synchronizer.Lock(this.m_Model.ViewerState.SyncRoot)) {
                currentRole = this.m_Model.ViewerState.iRole;
            }
            if (currentRole == 0)//Disconnected
                    {
                if (this.Controls.Contains(this.m_TabGroupBox)) {
                    this.Controls.Remove(this.m_TabGroupBox);
                    if (this.m_TabGroupBox.Controls.Contains(this.m_TabControl)) {
                        this.m_TabGroupBox.Controls.Remove(this.m_TabControl);
                    }
                    this.m_ButtonGroupBox.Text = Strings.StandAloneInstrStartupStepTwo;
                }
                /*if (this.m_TabControl.Controls.Contains(this.m_TabControl.m_UDPTab))
                    {
                    this.m_TabControl.Controls.Remove(this.m_TabControl.m_UDPTab);
                    }
                if(this.m_TabControl.Controls.Contains(this.m_TabControl.m_AdvancedTab))
                    {
                    this.m_TabControl.Controls.Remove(this.m_TabControl.m_AdvancedTab);
                    }*/
            }
            else if (currentRole == 2) {//Presenter
                /*if (!(this.Controls.Contains(this.m_TabGroupBox))) {
                   this.Controls.Add(this.m_TabGroupBox);
                   }*/
                this.m_TabGroupBox.Text = Strings.PresenterStartupStepTwo;
                this.m_ButtonGroupBox.Text = Strings.PresenterStartupStepThree;
                this.m_Connection = new InstructorConnection(this, this.m_Model, this.m_EventQueue);

            }
            else if ((currentRole == 1) ||//Viewer
      (currentRole == 3)) {//Public Display
                /* if (!(this.Controls.Contains(this.m_TabGroupBox))) {
                     this.Controls.Add(this.m_TabGroupBox);
                     }*/
                this.m_TabGroupBox.Text = Strings.ViewerStartupStepTwo;
                this.m_ButtonGroupBox.Text = Strings.ViewerStartupStepThree;
                this.m_Connection = new ViewerConnection(this, this.m_Model, this.m_EventQueue);
            }
        }


        public void OnRoleChanged(object sender, PropertyEventArgs args) {
            for (int i = this.m_TabControl.m_AdvancedTab.Controls.Count; --i >= 0; ) {
                this.m_TabControl.m_AdvancedTab.Controls.RemoveAt(i);
            }
            for (int i = this.m_UDPPanel.Controls.Count; --i >= 0; ) {
                if (!(this.m_UDPPanel.Controls[i] is Label))
                    this.m_UDPPanel.Controls.RemoveAt(i);
            }

            AddConnection();
        }

        //Should have above just call this one.
        public void OnRoleChanged2() {
            for (int i = this.m_TabControl.m_AdvancedTab.Controls.Count; --i >= 0; ) {
                this.m_TabControl.m_AdvancedTab.Controls.RemoveAt(i);
            }

            for (int i = this.m_UDPPanel.Controls.Count; --i >= 0; ) {
                if (!(this.m_UDPPanel.Controls[i] is Label))
                    this.m_UDPPanel.Controls.RemoveAt(i);
            }

            AddConnection();
        }


        public class StartupTabControl : TabControl {
            public AdvancedTab m_AdvancedTab;
            public UDPTab m_UDPTab;
            public StartupForm m_Startup;
            PresenterModel m_Model;
            private readonly EventQueue.PropertyEventDispatcher m_AdvancedListener;


            public StartupTabControl(Size size, Point location, int tabIndex, StartupForm stup, PresenterModel model, ControlEventQueue eventqueue) {
                this.SuspendLayout();
                this.m_Startup = stup;
                this.m_Model = model;
                this.ItemSize = new System.Drawing.Size(52, 18);
                this.Location = location;
                this.Name = "tabControl";
                this.SelectedIndex = 0;
                this.Font = Model.Viewer.ViewerStateModel.StringFont;
                this.Size = size;
                this.TabIndex = tabIndex;
                this.m_AdvancedTab = new AdvancedTab(this.m_Model);
                this.m_UDPTab = new UDPTab(this.m_Model);
                this.m_UDPTab.Controls.Add(this.m_Startup.m_UDPPanel);
                this.Controls.Add(this.m_UDPTab);
                this.Controls.Add(this.m_AdvancedTab);

                this.m_AdvancedListener = new EventQueue.PropertyEventDispatcher(
                    this.m_Startup.m_EventQueue,
                    new PropertyEventHandler(this.HandleAdvancedChanged));
                this.m_Model.ViewerState.Changed["Advanced"].Add(this.m_AdvancedListener.Dispatcher);
                this.m_AdvancedListener.Dispatcher(this, null);

                this.ControlAdded += new ControlEventHandler(UDPTabAdded);

                this.ResumeLayout();
            }
            protected override void Dispose(bool disposing) {
                if (disposing) {
                    this.m_Model.ViewerState.Changed["Advanced"].Remove(this.m_AdvancedListener.Dispatcher);
                }

                base.Dispose(disposing);
            }

            public void UDPTabAdded(object obj, ControlEventArgs e) {
                UpdateAdvanced();
            }
            public void HandleAdvancedChanged(object sender, PropertyEventArgs e) {
                UpdateAdvanced();
            }
            public void UpdateAdvanced() {
                using (Synchronizer.Lock(this.m_Model.ViewerState.SyncRoot)) {
                    if (this.m_Model.ViewerState.Advanced) {
                        this.m_AdvancedTab.Select();
                    }
                    else if (this.Controls.Contains(this.m_UDPTab)) {
                        this.m_UDPTab.Select();
                    }
                }
            }
        }
        public class AdvancedTab : TabPage {
            PresenterModel m_Model;
            public AdvancedTab(PresenterModel model) {
                this.m_Model = model;
                this.Text = Strings.AdvancedConnectionOptions;
                this.Click += new EventHandler(AdvancedTab_Click);
            }

            void AdvancedTab_Click(object sender, EventArgs e) {
                using (Synchronizer.Lock(this.m_Model.ViewerState.SyncRoot)) {
                    //Only set if we are student or public display
                    if (this.m_Model.ViewerState.iRole == 1 || //Student
                        this.m_Model.ViewerState.iRole == 3) //public display
                        {
                        this.m_Model.ViewerState.Advanced = true;
                    }
                }
            }
        }
        public class UDPTab : TabPage {
            PresenterModel m_Model;
            public UDPTab(PresenterModel model) {
                this.m_Model = model;
                this.Text = Strings.BroadcastedPresentations;
                this.Click += new EventHandler(UDPTab_Click);
            }

            void UDPTab_Click(object sender, EventArgs e) {
                using (Synchronizer.Lock(this.m_Model.ViewerState.SyncRoot)) {
                    this.m_Model.ViewerState.Advanced = false;
                }
            }
        }

        public class RoleGroup : GroupBox {
            PresenterModel m_Model;
            StartupForm m_StartupForm;
            RadioButton ViewerRadioButton, PresenterRadioButton, DisconnectedRadioButton, PublicRadioButton;
            private readonly EventQueue.PropertyEventDispatcher m_ViewerStateRoleListener;

            public RoleGroup(Point location, Size size, PresenterModel model, StartupForm startup, bool compact) {
                int buttonSpacing = 5;
                int topSpacing = 20;
                if (compact) {
                    buttonSpacing = 1;
                    topSpacing = 10;
                }
                this.SuspendLayout();
                this.m_Model = model;
                this.m_StartupForm = startup;
                this.FlatStyle = FlatStyle.System;
                this.Location = location;
                this.Size = size;
                this.Name = "RoleGroup";
                this.Text = Strings.StartupStepOne;
                this.Enabled = true;

                Label labelConnected = new Label();
                labelConnected.FlatStyle = FlatStyle.System;
                labelConnected.Text = Strings.NetworkedMode;
                labelConnected.Font = Model.Viewer.ViewerStateModel.StringFont2;
                labelConnected.Location = new Point(20, topSpacing);
                labelConnected.Size = new Size(120, 16);
                this.Controls.Add(labelConnected);

                Label labelDisconnected = new Label();
                labelDisconnected.FlatStyle = FlatStyle.System;
                labelDisconnected.Font = Model.Viewer.ViewerStateModel.StringFont2;
                labelDisconnected.Text = Strings.StandAloneMode;
                labelDisconnected.Location = new Point(140, topSpacing);
                labelDisconnected.Size = new Size(90, 16);
                this.Controls.Add(labelDisconnected);


                //Find a RadioButton constructor that does this stuff
                this.ViewerRadioButton = new RadioButton();
                this.ViewerRadioButton.FlatStyle = FlatStyle.System;
                this.ViewerRadioButton.Font = Model.Viewer.ViewerStateModel.StringFont1;
                this.ViewerRadioButton.Text = Strings.StudentRole;
                this.ViewerRadioButton.Location = new Point(20, labelConnected.Bottom + buttonSpacing);
                this.ViewerRadioButton.Size = new Size(100, this.ViewerRadioButton.Font.Height);
                this.ViewerRadioButton.CheckedChanged += new EventHandler(OnCheckedChanged);
                this.Controls.Add(this.ViewerRadioButton);

                this.PresenterRadioButton = new RadioButton();
                this.PresenterRadioButton.FlatStyle = FlatStyle.System;
                this.PresenterRadioButton.Font = Model.Viewer.ViewerStateModel.StringFont1;
                this.PresenterRadioButton.Text = Strings.InstructorRole;
                this.PresenterRadioButton.Location = new Point(20, ViewerRadioButton.Bottom + buttonSpacing);
                this.PresenterRadioButton.Size = new Size(100, this.PresenterRadioButton.Font.Height);
                this.PresenterRadioButton.CheckedChanged += new EventHandler(OnCheckedChanged);
                this.Controls.Add(this.PresenterRadioButton);


                this.PublicRadioButton = new RadioButton();
                this.PublicRadioButton.FlatStyle = FlatStyle.System;
                this.PublicRadioButton.Font = Model.Viewer.ViewerStateModel.StringFont1;
                this.PublicRadioButton.Text = Strings.PublicDisplayRole;
                this.PublicRadioButton.Location = new Point(20, PresenterRadioButton.Bottom + buttonSpacing);
                this.PublicRadioButton.Size = new Size(140, this.PublicRadioButton.Font.Height);
                this.PublicRadioButton.CheckedChanged += new EventHandler(OnCheckedChanged);
                this.Controls.Add(this.PublicRadioButton);

                this.DisconnectedRadioButton = new RadioButton();
                this.DisconnectedRadioButton.FlatStyle = FlatStyle.System;
                this.DisconnectedRadioButton.Font = Model.Viewer.ViewerStateModel.StringFont1;
                this.DisconnectedRadioButton.Text = Strings.InstructorRole;
                this.DisconnectedRadioButton.Location = new Point(140, labelDisconnected.Bottom + buttonSpacing);
                this.DisconnectedRadioButton.Size = new Size(90, this.DisconnectedRadioButton.Font.Height);
                this.DisconnectedRadioButton.CheckedChanged += new EventHandler(OnCheckedChanged);
                this.Controls.Add(this.DisconnectedRadioButton);
#if STUDENT_CLIENT_ONLY
                this.DisconnectedRadioButton.Enabled = false;
                this.PublicRadioButton.Enabled = false;
                this.PresenterRadioButton.Enabled = false;
#endif

                this.m_ViewerStateRoleListener = new EventQueue.PropertyEventDispatcher(this.m_StartupForm.m_EventQueue,
                    new PropertyEventHandler(this.HandleViewerStateRoleChanged));
                this.m_Model.ViewerState.Changed["iRole"].Add(this.m_ViewerStateRoleListener.Dispatcher);

                this.m_ViewerStateRoleListener.Dispatcher(this, null);

                this.ResumeLayout();
            }

            protected override void Dispose(bool disposing) {
                if (disposing) {
                    this.m_Model.ViewerState.Changed["iRole"].Remove(this.m_ViewerStateRoleListener.Dispatcher);
                }

                base.Dispose(disposing);
            }

            public void HandleViewerStateRoleChanged(object sender, PropertyEventArgs e) {
                //using (Synchronizer.Lock(this.m_Model.ViewerState.SyncRoot)) {
                /*if (e is PropertyChangeEventArgs && (e as PropertyChangeEventArgs).NewValue != null) {
                    PropertyChangeEventArgs pcea = e as PropertyChangeEventArgs;
                    int newRole = ((int)(pcea.NewValue));*/
                int newRole = 0;
                using (Synchronizer.Lock(this.m_Model.ViewerState.SyncRoot)) {
                    newRole = this.m_Model.ViewerState.iRole;
                }
                if (newRole == 0)//Disconnected
                        {
                    this.m_StartupForm.m_StartJoinButton.Association = null;
                    this.m_StartupForm.m_StartJoinButton.HandleEnabledAndAssociation(null);
                    this.m_StartupForm.m_EnableExternalMonitorCheckbox.HandleRoleChangedHelper();
                    this.DisconnectedRadioButton.Checked = true;
                }
                else if (newRole == 1)//Viewer
                        {
                    using (Synchronizer.Lock(this.m_Model.Participant.SyncRoot)) {
                        if (!(this.m_Model.Participant.Role is StudentModel)) {
                            this.m_Model.Participant.Role = new StudentModel(Guid.NewGuid());
                        }
                    }

                    this.m_StartupForm.m_StartJoinButton.HandleEnabled();
                    this.ViewerRadioButton.Checked = true;
                }
                else if (newRole == 2)//Presenter
                        {
                    using (Synchronizer.Lock(this.m_Model.Participant.SyncRoot)) {
                        if (!(this.m_Model.Participant.Role is InstructorModel)) {
                            this.m_Model.Participant.Role = new InstructorModel(Guid.NewGuid());
                        }
                    }
                    this.PresenterRadioButton.Checked = true;

                    this.m_StartupForm.m_StartJoinButton.HandleEnabledAndAssociation(null);

                }
                else if (newRole == 3)//Public Display
                        {

                    using (Synchronizer.Lock(this.m_Model.Participant.SyncRoot)) {
                        if (!(this.m_Model.Participant.Role is PublicModel)) {
                            this.m_Model.Participant.Role = new PublicModel(Guid.NewGuid());
                        }
                    }
                    this.PublicRadioButton.Checked = true;//this will actually trigger OnCheckedChanged, so I can take everything else out.

                    this.m_StartupForm.m_StartJoinButton.HandleEnabled();

                }
                this.m_StartupForm.OnRoleChanged2();
            }


            public void OnCheckedChanged(object obj, EventArgs e) {
                RadioButton radiobtn = (RadioButton)obj;

                if (radiobtn.Checked) {
                    if (radiobtn == this.ViewerRadioButton) {
                        using (Synchronizer.Lock(this.m_Model.Participant.SyncRoot)) {
                            if (!(this.m_Model.Participant.Role is StudentModel)) {
                                this.m_Model.Participant.Role = new StudentModel(Guid.NewGuid());
                            }
                        }
                        using (Synchronizer.Lock(this.m_Model.ViewerState.SyncRoot)) {
                            this.m_Model.ViewerState.iRole = 1;
                        }
                    }
                    else if (radiobtn == this.DisconnectedRadioButton) {
                        using (Synchronizer.Lock(this.m_Model.Participant.SyncRoot)) {
                            if (!(this.m_Model.Participant.Role is InstructorModel)) {
                                this.m_Model.Participant.Role = new InstructorModel(Guid.NewGuid());
                            }
                        }
                        using (Synchronizer.Lock(this.m_Model.ViewerState.SyncRoot)) {
                            this.m_Model.ViewerState.iRole = 0;
                        }
                    }
                    else if (radiobtn == this.PresenterRadioButton) {
                        using (Synchronizer.Lock(this.m_Model.Participant.SyncRoot)) {
                            if (!(this.m_Model.Participant.Role is InstructorModel)) {
                                this.m_Model.Participant.Role = new InstructorModel(Guid.NewGuid());
                            }
                        }
                        using (Synchronizer.Lock(this.m_Model.ViewerState.SyncRoot)) {
                            this.m_Model.ViewerState.iRole = 2;
                        }
                    }
                    else if (radiobtn == this.PublicRadioButton) {
                        using (Synchronizer.Lock(this.m_Model.Participant.SyncRoot)) {
                            if (!(this.m_Model.Participant.Role is PublicModel)) {
                                this.m_Model.Participant.Role = new PublicModel(Guid.NewGuid());
                            }
                        }
                        using (Synchronizer.Lock(this.m_Model.ViewerState.SyncRoot)) {
                            this.m_Model.ViewerState.iRole = 3;
                        }
                    }
                    this.m_StartupForm.OnRoleChanged2();

                }
            }
        }

        public class EnableExternalMonitorCheckbox : CheckBox {
            PresenterModel m_Model;
            StartupForm m_Parent;
            /// Listens for changes to the user role
            /// </summary>
            private readonly EventQueue.PropertyEventDispatcher m_RoleChangedDispatcher;
            private readonly EventQueue.PropertyEventDispatcher m_SecondMonitorChangedDispatcher;
            public EnableExternalMonitorCheckbox(Point location, Size size, PresenterModel model, ControlEventQueue dispatcher, StartupForm parent) {

                this.SuspendLayout();

                this.m_Model = model;
                this.m_Parent = parent;
                this.FlatStyle = FlatStyle.System;
                this.Location = location;
                this.Font = Model.Viewer.ViewerStateModel.StringFont2;
                this.Size = size;
                this.Name = "enableSecondMonitorCheckBox";
                this.Text = Strings.EnableSecondMonitor;
                this.Enabled = false;
                // Set the default value according to the model's current setting
                // NOTE: Split into two steps to avoid deadlock with updating the check state
                bool bShouldBeChecked = true;

                if (this.m_Model != null) {
                    using (Synchronizer.Lock(this.m_Model.ViewerState.SyncRoot)) {
                        bShouldBeChecked = this.m_Model.ViewerState.SecondMonitorEnabled;
                    }
                }

                if (bShouldBeChecked) {
                    this.Checked = true;
                    this.CheckState = CheckState.Checked;
                }
                else {
                    this.Checked = false;
                    this.CheckState = CheckState.Unchecked;
                }

                this.ResumeLayout();

                // Listen for changes to the Role property
                this.m_RoleChangedDispatcher = new EventQueue.PropertyEventDispatcher(dispatcher, new PropertyEventHandler(this.HandleRoleChanged));
                this.m_Model.ViewerState.Changed["iRole"].Add(this.m_RoleChangedDispatcher.Dispatcher);
                this.m_RoleChangedDispatcher.Dispatcher(this, null);

                this.m_SecondMonitorChangedDispatcher = new EventQueue.PropertyEventDispatcher(dispatcher, new PropertyEventHandler(this.HandleSecondMonitorChanged));
                this.m_Model.ViewerState.Changed["SecondMonitorEnabled"].Add(this.m_SecondMonitorChangedDispatcher.Dispatcher);
                this.m_SecondMonitorChangedDispatcher.Dispatcher(this, null);


            }

            /// Destroy resources, i.e. stop listening to model changes
            /// </summary>
            /// <param name="disposing">True if we are destroying all objects</param>
            protected override void Dispose(bool disposing) {
                if (disposing) {
                    this.m_Model.ViewerState.Changed["iRole"].Remove(this.m_RoleChangedDispatcher.Dispatcher);
                    this.m_Model.ViewerState.Changed["SecondMonitorEnabled"].Remove(this.m_SecondMonitorChangedDispatcher.Dispatcher);
                }

                base.Dispose(disposing);
            }
            public void HandleSecondMonitorChanged(object sender, PropertyEventArgs args) {
                bool bShouldBeChecked = true;

                if (this.m_Model != null) {
                    using (Synchronizer.Lock(this.m_Model.ViewerState.SyncRoot)) {
                        bShouldBeChecked = this.m_Model.ViewerState.SecondMonitorEnabled;
                    }
                }

                if (bShouldBeChecked) {
                    this.Checked = true;
                    this.CheckState = CheckState.Checked;
                }
                else {
                    this.Checked = false;
                    this.CheckState = CheckState.Unchecked;
                }

            }
            public void HandleRoleChangedHelper() {
                using (Synchronizer.Lock(this.m_Model.ViewerState.SyncRoot)) {
                    this.Enabled = ((this.m_Model.ViewerState.iRole == 2) || (this.m_Model.ViewerState.iRole == 0));//Instructor or Disconnected

                }
            }

            private void HandleRoleChanged(object sender, PropertyEventArgs args) {
                HandleRoleChangedHelper();
            }
            protected override void OnCheckedChanged(EventArgs e) {
                base.OnCheckedChanged(e);

                // Update the model value
                if (this.m_Model != null) {
                    using (Synchronizer.Lock(this.m_Model.ViewerState.SyncRoot)) {
                        this.m_Model.ViewerState.SecondMonitorEnabled = this.Checked;
                    }
                }
            }

        }




        public abstract class ConnectionHelper {
            public ManualConnectionPanel m_ManualConnectionPanel;
            public PresentationsPanel m_PresentationsPanel;
            public PresenterModel m_Model;
            public ControlEventQueue m_EventQueue;
            public StartupForm m_StartupForm;
            public StartupTabControl m_TabControl;
            public ConnectionHelper(StartupForm stup, PresenterModel model, ControlEventQueue dispatcher) {
                this.m_StartupForm = stup;
                this.m_TabControl = this.m_StartupForm.m_TabControl;
                this.m_Model = model;
                this.m_EventQueue = dispatcher;
            }

        }

        public class InstructorConnection : ConnectionHelper {

            public InstructorConnection(StartupForm parent, PresenterModel model, ControlEventQueue dispatcher)
                : base(parent, model, dispatcher) {
                this.m_PresentationsPanel = null;
                this.m_ManualConnectionPanel = new ManualConnectionPanel(this.m_Model, this.m_StartupForm);
                this.m_ManualConnectionPanel.Location = parent.m_UDPPanel.Location;
                this.m_ManualConnectionPanel.Size = parent.m_UDPPanel.Size;
                /*if (!(this.m_StartupForm.Controls.Contains(this.m_TabControl))) {
                    this.m_StartupForm.Controls.Add(this.m_TabControl);

                    }*/
                if (!(this.m_StartupForm.Controls.Contains(this.m_StartupForm.m_TabGroupBox))) {
                    this.m_StartupForm.Controls.Add(this.m_StartupForm.m_TabGroupBox);
                    if (!(this.m_StartupForm.m_TabGroupBox.Controls.Contains(this.m_TabControl))) {
                        this.m_StartupForm.m_TabGroupBox.Controls.Add(this.m_TabControl);
                    }
                }
                this.m_TabControl.m_AdvancedTab.Controls.Add(this.m_ManualConnectionPanel);
                if (this.m_TabControl.Controls.Contains(this.m_TabControl.m_UDPTab)) {
                    this.m_TabControl.Controls.Remove(this.m_TabControl.m_UDPTab);
                }

            }


        }



        public class ViewerConnection : ConnectionHelper {


            public ViewerConnection(StartupForm parent, PresenterModel model, ControlEventQueue dispatcher)
                : base(parent, model, dispatcher) {
                if (!(this.m_StartupForm.Controls.Contains(this.m_StartupForm.m_TabGroupBox))) {
                    this.m_StartupForm.Controls.Add(this.m_StartupForm.m_TabGroupBox);
                    if (!(this.m_StartupForm.m_TabGroupBox.Controls.Contains(this.m_TabControl))) {
                        this.m_StartupForm.m_TabGroupBox.Controls.Add(this.m_TabControl);
                    }
                }
                this.m_ManualConnectionPanel = new ManualConnectionPanel(this.m_Model, this.m_StartupForm);
                this.m_ManualConnectionPanel.Location = parent.m_UDPPanel.Location;
                this.m_ManualConnectionPanel.Size = new Size((parent.m_UDPPanel.Width - 10) / 2, parent.m_UDPPanel.Height);
                this.m_TabControl.m_AdvancedTab.Controls.Add(this.m_ManualConnectionPanel);

                this.m_PresentationsPanel = new PresentationsPanel(this.m_Model, this.m_StartupForm);
                this.m_PresentationsPanel.Location = new Point(parent.m_UDPPanel.Location.X + (parent.m_UDPPanel.Width + 10) / 2, parent.m_UDPPanel.Location.Y);
                this.m_PresentationsPanel.Size = new Size((parent.m_UDPPanel.Width - 10) / 2, parent.m_UDPPanel.Height);
                this.m_TabControl.m_AdvancedTab.Controls.Add(this.m_PresentationsPanel);


                using (Synchronizer.Lock(this.m_Model.ViewerState.SyncRoot)) {
                    if (this.m_Model.ViewerState.Advanced) {
                        if (this.m_TabControl.Controls.Contains(this.m_TabControl.m_UDPTab)) {
                            this.m_TabControl.Controls.Remove(this.m_TabControl.m_UDPTab);
                        }
                        this.m_TabControl.Controls.Add(this.m_TabControl.m_UDPTab);
                    }
                    else {
                        this.m_TabControl.Controls.Remove(this.m_TabControl.m_AdvancedTab);
                        if (!(this.m_TabControl.Controls.Contains(this.m_TabControl.m_UDPTab))) {
                            this.m_TabControl.Controls.Add(this.m_TabControl.m_UDPTab);
                        }
                        this.m_TabControl.Controls.Add(this.m_TabControl.m_AdvancedTab);
                    }
                }//This is a bit of a hack because the stuff in the tabcontrol doesn't work.  This puts the udptab in front and selected if not in advanced mode and the advanced tab in front and selected otherwise.  THis does change the order of the tabs...  not sure if that's good or bad.  


            }
        }
        public class Cp3HelpButton : Button {
            public Cp3HelpButton() {
                this.FlatStyle = FlatStyle.System;
                this.Size = new Size(60, 24);
                this.Text = Strings.StartupHelp;

            }

            protected override void OnClick(EventArgs e) {
                base.OnClick(e);
                try {
                    string s = System.Reflection.Assembly.GetExecutingAssembly().Location;
                    while (s[s.Length - 1] != '\\') {
                        s = s.Substring(0, s.Length - 1);
                    }
                    Help.ShowHelp(this.Parent, s + "Help\\startguide3.html");
                }
                catch {  }
            }
        }
    }
}
   
