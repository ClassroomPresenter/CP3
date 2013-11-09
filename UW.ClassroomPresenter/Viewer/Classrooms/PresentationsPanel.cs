
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
    
    public class PresentationsPanel : Panel {
        private readonly ControlEventQueue m_EventQueue;
        private readonly PresenterModel m_Model;
        private readonly ProtocolCollectionHelper m_ProtocolCollectionHelper;
        private bool m_Disposed;
        public StartupForm m_Startup;
 
        #region Construction & Destruction

        public PresentationsPanel(PresenterModel model, StartupForm stup) {
            if (model == null)
                throw new ArgumentNullException("model");

            this.m_EventQueue = new ControlEventQueue(this);
            this.m_Model = model;
            this.m_Startup = stup;

            this.Font = Model.Viewer.ViewerStateModel.StringFont;
            this.Location = new Point(276, 10);
            this.Size = new Size(250, 250);
            this.BorderStyle = BorderStyle.Fixed3D;
            this.AutoScroll = true;

            Label label = new Label();
            label.FlatStyle = FlatStyle.System;
            label.Font = Model.Viewer.ViewerStateModel.StringFont2;
            label.Size = new Size(this.Width + 30, label.Font.Height);
            label.Location = new Point(10, 5);
            label.Text = Strings.PresentationsInChosenVenue;
            this.Controls.Add(label);



            // Create a handle immediately so the ListViewItems can marshal event handlers to the creator thread.
            this.CreateHandle();

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





        private class ProtocolCollectionHelper : PropertyCollectionHelper {
            private readonly PresentationsPanel m_Parent;

            public ProtocolCollectionHelper(PresentationsPanel parent, NetworkModel network)
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
            private readonly PresentationsPanel m_Parent;

            public ClassroomCollectionHelper(PresentationsPanel parent, ProtocolModel protocol)
                : base(parent.m_EventQueue, protocol, "Classrooms") {
                this.m_Parent = parent;
                base.Initialize();
                }

            protected override void Dispose(bool disposing) {
                try {
                    if (disposing)
                        foreach (PresentationCollectionHelper helper in this.Tags)
                            helper.Dispose();
                    } finally {
                    base.Dispose(disposing);
                    }
                }

            protected override object SetUpMember(int index, object member) {
                return new PresentationCollectionHelper(this.m_Parent, ((ClassroomModel)member));
                }

            protected override void TearDownMember(int index, object member, object tag) {
                if (tag != null)
                    ((PresentationCollectionHelper)tag).Dispose();
                }
            }


        private class PresentationCollectionHelper : PropertyCollectionHelper {
            private readonly PresentationsPanel m_Parent;
            public ClassroomModel m_Classroom;

            public PresentationCollectionHelper(PresentationsPanel parent, ClassroomModel classroom)
                : base(parent.m_EventQueue, classroom, "Presentations") {
                this.m_Parent = parent;
                this.m_Classroom = classroom;
                base.Initialize();
                }

            protected override void Dispose(bool disposing) {
                try {
                    if (disposing) {
                        for (int i = (this.m_Parent.Controls.Count) - 1; i > 0; i--)
                            this.m_Parent.Controls.RemoveAt(i);
                        }
                    } finally {
                    base.Dispose(disposing);
                    }
                }

            protected override object SetUpMember(int index, object member) {
                Debug.Assert(!this.m_Parent.InvokeRequired);

                PresentationRadioButton button = new PresentationRadioButton(this.m_Parent.m_EventQueue, (PresentationModel)member, this.m_Parent.m_Startup, this.m_Parent, index, this.m_Classroom);
                return button;
                }

            protected override void TearDownMember(int index, object member, object tag) {
                Debug.Assert(!this.m_Parent.InvokeRequired);

                PresentationRadioButton rb = tag as PresentationRadioButton;
                if (this.m_Parent.Controls.Contains(rb)) {
                    this.m_Parent.Controls.Remove(rb);
                    }
                rb.Dispose();

                }
            }
        }


    #region PresentationRadioButton


    public class PresentationRadioButton : RadioButton {
        private readonly ControlEventQueue m_EventQueue;
        private readonly EventQueue.PropertyEventDispatcher m_HumanNameChangedDispatcher;
        private readonly EventQueue.PropertyEventDispatcher m_ConnectedChangedDispatcher;
        private readonly EventQueue.PropertyEventDispatcher m_ViewerStateRoleListener;

        private bool m_Disposed;

        private readonly PresentationModel m_Presentation;
        private StartupForm m_Startup;
        int index;
        public ClassroomModel m_Classroom;
        public PresentationsPanel m_PresentationsPanel; 
 

        public PresentationRadioButton(ControlEventQueue dispatcher, PresentationModel presentation, StartupForm stup, PresentationsPanel parent, int i, ClassroomModel classroom) {
            this.m_EventQueue = dispatcher;
            this.m_Presentation = presentation;
            this.Tag = presentation.Owner;
            this.m_Startup = stup;
            this.Parent = parent;
            this.m_PresentationsPanel = parent;
            this.m_Classroom = classroom;
            this.Parent.Controls.Add(this);


            this.FlatStyle = FlatStyle.System;
            this.Font = Model.Viewer.ViewerStateModel.StringFont1;
            this.index = i;

            this.Location = new Point(10, (2*this.Font.Height) * (this.index + 1));
            this.Size = new Size(this.Parent.Width - 14, 2*this.Font.Height);
            
            //If the role changes we should remove ourself from our parent.
            this.m_ViewerStateRoleListener = new EventQueue.PropertyEventDispatcher(this.m_Startup.m_EventQueue,
                    new PropertyEventHandler(this.HandleViewerStateRoleChanged));
            this.m_Startup.m_Model.ViewerState.Changed["iRole"].Add(this.m_ViewerStateRoleListener.Dispatcher);

           

            this.m_HumanNameChangedDispatcher = new EventQueue.PropertyEventDispatcher(this.m_EventQueue, new PropertyEventHandler(this.HandleHumanNameChanged));
            this.Presentation.Changed["HumanName"].Add(this.m_HumanNameChangedDispatcher.Dispatcher);
            this.m_HumanNameChangedDispatcher.Dispatcher(this.Presentation, null);
            this.CheckedChanged += new EventHandler(HandlePresentationSelectionChanged);

            this.m_ConnectedChangedDispatcher = new EventQueue.PropertyEventDispatcher(this.m_EventQueue, new PropertyEventHandler(this.HandleConnectedChanged));
            this.m_Classroom.Changed["Connected"].Add(this.m_ConnectedChangedDispatcher.Dispatcher);
            this.HandleConnectedChanged(this.m_Classroom, null);
            }



        private void HandlePresentationSelectionChanged(object sender, EventArgs e) {

            if (sender != null) {

                ParticipantModel pModel = (ParticipantModel)(((PresentationRadioButton)sender).Tag);
                using (Synchronizer.Lock(this.m_Classroom.SyncRoot)) {
                    if (this.Checked) {

                        for (int i = 0; i < this.m_Startup.m_UDPPanel.Controls.Count; i++) {
                            if (this.m_Startup.m_UDPPanel.Controls[i] is RadioButton) {

                                using (Synchronizer.Lock(((UDPPresentationRadioButton)(this.m_Startup.m_UDPPanel.Controls[i])).m_Classroom.SyncRoot)) {
                                    if (!(this.m_Classroom == ((UDPPresentationRadioButton)(this.m_Startup.m_UDPPanel.Controls[i])).m_Classroom)) {
                                        ((RadioButton)(this.m_Startup.m_UDPPanel.Controls[i])).Checked = false;
                                        } else {
                                        ((RadioButton)(this.m_Startup.m_UDPPanel.Controls[i])).Checked = true;
                                        }
                                    }
                                }
                            }
                        }
                    }
                //this.m_Startup.m_StartJoinButton.Association = pModel;

                //Do I want to make this if checked?  If I do this is there danger of an unchecking message coming in after a checking message and screwing things up?
                this.Parent = this.m_PresentationsPanel;
                //StartJoinButton2 button = ((PresentationsPanel)(this.Parent)).m_Startup.m_StartJoinButton;
                /* ((PresentationsPanel)(this.Parent)).m_Startup.m_StartJoinButton.Association = null;
                 if (((PresentationsPanel)(this.Parent)).m_Startup.m_StartJoinButton.InvokeRequired)
                     {
                    ((PresentationsPanel)(this.Parent)).m_Startup.m_StartJoinButton.Invoke(new StartJoinButton2.HandleEnabledDelegate(((PresentationsPanel)(this.Parent)).m_Startup.m_StartJoinButton.HandleEnabled));
                     }
                 else
                     {
                     ((PresentationsPanel)(this.Parent)).m_Startup.m_StartJoinButton.HandleEnabled();
                     }*/
                ((PresentationsPanel)(this.Parent)).m_Startup.m_StartJoinButton.HandleEnabledAndAssociation(pModel);

                }

            }


        protected override void Dispose(bool disposing) {
            if (!this.m_Disposed) {
                if (disposing) {
                    this.Presentation.Changed["HumanName"].Remove(this.m_HumanNameChangedDispatcher.Dispatcher);

                    }
                this.m_Disposed = true;
                }
            }

        public PresentationModel Presentation {
            get { return this.m_Presentation; }
            }

        private void HandleHumanNameChanged(object sender, PropertyEventArgs args_) {
            using (Synchronizer.Lock(this.Presentation.SyncRoot)) {
                if (!this.Presentation.IsUntitledPresentation) this.Text = this.Presentation.HumanName;
                else this.Text = Strings.UntitledPresentation;
                }
            }
        private void HandleConnectedChanged(object sender, EventArgs args_) {
            Debug.Assert(this.m_Classroom != null);
            using (Synchronizer.Lock(this.m_Classroom.SyncRoot)) {
                if (!(this.m_Classroom.Connected)) {
                    this.Visible = false;
                    this.Parent = this.m_PresentationsPanel;
                    this.Parent.Controls.Remove(this);
                    this.Checked = this.m_Classroom.Connected;
                    } else {
                    this.Visible = true;
                    this.Parent = this.m_PresentationsPanel;
                    this.Parent.Controls.Add(this);
                    foreach (Control c in this.m_Startup.m_UDPPanel.Controls) {
                        if (c is UDPPresentationRadioButton) {
                            UDPPresentationRadioButton rb = c as UDPPresentationRadioButton;
                            if ((rb.m_Classroom == this.m_Classroom) && rb.Checked) {
                                this.Checked = true;
                                }
                            }
                        }
                    }
                }
            }
        private void HandleViewerStateRoleChanged(object sender, EventArgs args_) {
            Debug.Assert(this.m_Classroom != null);
            using (Synchronizer.Lock(this.m_Classroom.SyncRoot)) {
                this.m_Classroom.Connected = false;
                    this.Visible = false;
                    this.Parent = this.m_PresentationsPanel;
                    this.Parent.Controls.Remove(this);
                    this.Checked = this.m_Classroom.Connected;
                }
            }


        }

    #endregion PresentationRadioButton

    } 





    
          
  


