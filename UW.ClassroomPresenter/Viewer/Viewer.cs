// $Id: Viewer.cs 1421 2007-08-10 22:52:53Z fred $

//#define LAUNCH_TWO_VIEWERS

using System;
using System.Collections;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using System.Diagnostics;

using UW.ClassroomPresenter.Decks;
using UW.ClassroomPresenter.Model;
using UW.ClassroomPresenter.Network;
using UW.ClassroomPresenter.Network.RTP;
using UW.ClassroomPresenter.Network.TCP;
using UW.ClassroomPresenter.Viewer.Classrooms;
using UW.ClassroomPresenter.Viewer.ToolBars;
using UW.ClassroomPresenter.Misc;
using UW.ClassroomPresenter.Undo;
using UW.ClassroomPresenter.DeckMatcher;
using UW.ClassroomPresenter.Viewer.SecondMonitor;
using UW.ClassroomPresenter.Model.Presentation;
using UW.ClassroomPresenter.Model.Workspace;
using UW.ClassroomPresenter.Model.Network;
using UW.ClassroomPresenter.Network.Groups;
using UW.ClassroomPresenter.Model.Stylus;
using UW.ClassroomPresenter.Viewer.Menus;
using UW.ClassroomPresenter.Network.Broadcast;
using System.Text;
using UW.ClassroomPresenter.Model.Viewer;
using System.Collections.Generic;
#if RTP_BUILD
using MSR.LST.ConferenceXP;
using UW.ClassroomPresenter.Network.CXP;
#endif

namespace UW.ClassroomPresenter.Viewer {

    /// <summary>
    /// The main Classroom Presenter application form.
    /// </summary>
    public class ViewerForm :
#if RTP_BUILD
        CapabilityForm
#else
        Form
#endif
    {
        private readonly ControlEventQueue m_EventQueue;
        public ControlEventQueue EventQueue {
            get { return m_EventQueue; }
        }

        private MainToolBars m_MainToolBars;
        private readonly StartupForm m_StartupForm;
        internal readonly ViewerPresentationLayout m_PresentationLayout;
        private PresenterModel m_Model;
        private DeckMarshalService m_Marshal;
        private FullScreenAdapter m_FullScreenAdapter;

        public List<string> OpenInputFiles = null;
        public bool StandAlone = false;

        /// <summary>
        /// determines whether key input is allowed in a slide.
        /// </summary>
        public bool key_input_allowed_;

        private readonly EventQueue.PropertyEventDispatcher role_listener_;
        private readonly EventQueue.PropertyEventDispatcher stylus_listener_;
        private readonly EventQueue.PropertyEventDispatcher instructor_listener_;
        private readonly EventQueue.PropertyEventDispatcher m_NetworkStatusListener;
        private readonly EventQueue.PropertyEventDispatcher m_VersionExchangePopUpListener;

        private bool m_Saved = false;

        public static int white_board_num = 0;
        // TODO: Figure out what slide bounds should be, merge with DecksMenu.CreateBlankWhiteboardDeckMenuItem,
        //   and allow the aspect ratio to be user-customizable.
        public static Rectangle DEFAULT_SLIDE_BOUNDS = new Rectangle(0, 0, 720, 540);

#if RTP_BUILD

        #region CXP Capability

        private PresenterCapability m_Capability;
        private CXPCapabilityMessageSender m_CapabilitySender;
        private RegistryService m_Registry = null;
        private DeckMarshalService m_Loader = null;
        private ViewerStateService m_ViewerState = null;
        private WorkspaceUndoService m_Undo = null;
        private NetworkAssociationService m_Association = null;
        private DeckMatcherService m_DeckMatcher = null;
        /// <summary>
        /// This constructor is used for launching as a CXP capability.
        /// </summary>
        public ViewerForm() {

            Application.EnableVisualStyles();
            //Application.DoEvents();  //Note: this seems to cause problems in the CXP Capability context.

#if DEBUG && LOGGING
            LoggingService logger = new LoggingService();
#endif
            PresenterModel model = new PresenterModel();
            m_Loader = new DefaultDeckMarshalService();

            this.SuspendLayout();
            this.m_EventQueue = new ControlEventQueue(this);

            this.m_Model = model;
            this.m_Marshal = m_Loader;

            UpdateTitle();

            this.Name = "ViewerForm";

            //Retrieve the icon
            System.Reflection.Assembly thisExe = System.Reflection.Assembly.GetExecutingAssembly();
            this.Icon = new Icon( thisExe.GetManifestResourceStream( "UW.ClassroomPresenter.Presenter.ico" ) );

            // The form should fill 5/6 of the screen by default.
            // FIXME: Load/Store previous window size from/in the registry.
            this.Size = new Size(SystemInformation.WorkingArea.Size.Width * 5 / 6,
                SystemInformation.WorkingArea.Size.Height * 5 / 6);
            // the form should be no smaller than 1/2 it's original size
            this.MinimumSize = new Size(this.Size.Width / 2, this.Size.Height / 2);

            Menus.FileMenu.CloseFormDelegate cfd = new UW.ClassroomPresenter.Viewer.Menus.FileMenu.CloseFormDelegate(this.Close);
            this.Menu = new Menus.ViewerMainMenu(this.m_EventQueue, this.m_Model, m_Loader, cfd);


            this.m_MainToolBars = new MainToolBars(this.m_Model, this.m_EventQueue);
            this.m_PresentationLayout = new ViewerPresentationLayout(this.m_Model);
            //this.m_MainToolBar.Dock = DockStyle.Top;
            this.m_PresentationLayout.Dock = DockStyle.Fill;

            this.Controls.Add(this.m_PresentationLayout);

            this.Controls.Add(this.m_MainToolBars.m_MainToolBar);
            this.Controls.Add(this.m_MainToolBars.m_MainClassmateToolBar);
            this.Controls.Add(this.m_MainToolBars.m_ExtraClassmateToolBar);

            this.ResumeLayout(false);

            this.KeyDown += new KeyEventHandler(this.OnKeyDown);
            this.KeyPreview = true;

            ///add listeners for HumanName, Role, Stylus properties
            role_listener_ = new EventQueue.PropertyEventDispatcher(this.m_EventQueue, new PropertyEventHandler(this.ParticipantRoleChanged));
            stylus_listener_ = new EventQueue.PropertyEventDispatcher(this.m_EventQueue, new PropertyEventHandler(this.StylusChanged));
            instructor_listener_ = new EventQueue.PropertyEventDispatcher(this.m_EventQueue, new PropertyEventHandler(this.InstructorChanged));

            this.m_Model.Participant.Changed["Role"].Add(role_listener_.Dispatcher);
            this.m_Model.Changed["Stylus"].Add(stylus_listener_.Dispatcher);
            this.m_Model.Network.Changed["Association"].Add(instructor_listener_.Dispatcher);

            //Add listener for the version exchange warning pop-up
            this.m_VersionExchangePopUpListener = new EventQueue.PropertyEventDispatcher(this.m_EventQueue, new PropertyEventHandler(this.PopUpWarningHandler));
            this.m_Model.VersionExchange.Changed["PopUpWarningMessage"].Add(m_VersionExchangePopUpListener.Dispatcher);

            //Add listener for NetworkStatus
            m_NetworkStatusListener = new EventQueue.PropertyEventDispatcher(this.m_EventQueue, new PropertyEventHandler(this.NetworkStatusChanged));
            this.m_Model.Network.Changed["NetworkStatus"].Add(m_NetworkStatusListener.Dispatcher);

            this.m_FullScreenAdapter = new FullScreenAdapter(this.m_Model, this.m_PresentationLayout);

            instructor_listener_.Dispatcher(this, null);
            role_listener_.Dispatcher(this, null);
            stylus_listener_.Dispatcher(this, null);

            this.FormClosing += this.SaveClose;

            //Store local DPI settings which are used to make sure ink is scaled correctly.
            using (Graphics g = this.CreateGraphics()) {
                ViewerStateModel.SetLocalDpi(g.DpiX, g.DpiY);
            }

            m_ViewerState = new ViewerStateService(model.ViewerState);
            m_Registry = new RegistryService(model.ViewerState); 
#if DEBUG && LOGGING
            logger.AttachToModel(model.ViewerState);
#endif
            PrintingService printing = new PrintingService(model.ViewerState);
            m_Undo = new WorkspaceUndoService(this.m_EventQueue, model);

            m_Association = new NetworkAssociationService(this.m_EventQueue, model);
            m_DeckMatcher = new DeckMatcherService(this.m_EventQueue, model);
            
        }

        /// <summary>
        /// This is called from Dispose
        /// </summary>
        private void DisposeCapabilityResources() {
            if (m_DeckMatcher != null) {
                m_DeckMatcher.Dispose();
                m_DeckMatcher = null;
            }

            if (m_Association != null) {
                m_Association.Dispose();
                m_Association = null;
            }

            if (m_Undo != null) {
                m_Undo.Dispose();
                m_Undo = null;
            }

            if (m_Registry != null) {
                m_Registry.SaveAllProperties();
                m_Registry.Dispose();
                m_Registry = null;
            }

            if (m_ViewerState != null) {
                m_ViewerState.Dispose();
                m_ViewerState = null;
            }

            if (m_Loader != null) {
                m_Loader.Dispose();
                m_Loader = null;
            }
        }


        public override void AddCapability(ICapability capability) {
            base.AddCapability(capability);

            if (m_Capability == null) {
                m_Capability = (PresenterCapability)capability;

                // Hook the ObjectReceived event so we can receive incoming data
                m_Capability.ObjectReceived += new CapabilityObjectReceivedEventHandler(objectReceived);

                //Set the role to instructor if we initiated the capability.
                if (m_Capability.IsSender) {
                    //Set the instructor role
                    InstructorModel instructor = new InstructorModel(Guid.NewGuid());
                    using (Synchronizer.Lock(this.m_Model.Participant.SyncRoot)) {
                        m_Model.Participant.Role = instructor;
                    }
                    using (Synchronizer.Lock(this.m_Model.ViewerState.SyncRoot)) {
                        this.m_Model.ViewerState.iRole = 2;
                    }
                    //We also need to set up a couple of other things to make the instructor functional:  A network association
                    // and an empty presentation.
                    PresentationModel pres = new PresentationModel(Guid.NewGuid(), m_Model.Participant, "Untitled Presentation", true);
                    using (Synchronizer.Lock(m_Model.Network.SyncRoot)) {
                        m_Model.Network.Association = m_Model.Participant;
                    }
                    using (Synchronizer.Lock(instructor.SyncRoot)) {
                        instructor.CurrentPresentation = pres;
                    }
                }
                else { 
                    //Set student role
                    using (Synchronizer.Lock(this.m_Model.Participant.SyncRoot)) {
                        m_Model.Participant.Role = new StudentModel(Guid.NewGuid());
                    }
                    using (Synchronizer.Lock(this.m_Model.ViewerState.SyncRoot)) {
                        this.m_Model.ViewerState.iRole = 1;
                    }
                }

                //Before we create the CXPCapabilityMessageSender, we want to wait until the Play and Send methods have completed.
                //Otherwise we can end up trying to send before the capability is ready to accept messages.
                bool createCapabilitySenderNow = false;
                lock (m_Capability) { //This section synchronized with the PresenterCapability instance.
                    if (m_Capability.IsPlayingAndSending) {
                        createCapabilitySenderNow = true;
                    }
                    else {
                        m_Capability.OnPlay += new PresenterCapability.OnPlayHandler(OnCapabilityPlay);
                    }
                }
                if (createCapabilitySenderNow) {
                    m_CapabilitySender = new CXPCapabilityMessageSender(m_Model, m_Capability);
                }
            }
        }

        /// <summary>
        /// Handle an event to be raised once after the capability's Send and Play methods have completed.
        /// </summary>
        void OnCapabilityPlay() {
            m_Capability.OnPlay -= new PresenterCapability.OnPlayHandler(OnCapabilityPlay);
            m_CapabilitySender = new CXPCapabilityMessageSender(m_Model, m_Capability);
        }

        public override bool RemoveCapability(ICapability capability) {
            bool ret = base.RemoveCapability(capability);

            if (ret) {
                // Remove the ObjectReceived event handler.
                // This form is going away, but the Capability may be replayed in which case we'd receive this event into a disposed form!
                m_Capability.ObjectReceived -= new CapabilityObjectReceivedEventHandler(objectReceived);
                m_CapabilitySender.Dispose();
                m_Capability = null;
                m_CapabilitySender = null;
            }
            return ret;
        }

        // Hook the objectReceived event and process it
        private void objectReceived(object o, ObjectReceivedEventArgs orea) {
            if (m_CapabilitySender != null) {               
                m_CapabilitySender.Receive(orea.Data, orea.Participant.RtpParticipant);
            }
        }
        
        #endregion CXP Capability

#endif

        /// <summary>
        /// This constructor is for the case where the app is started normally (not as a CXP capability).
        /// </summary>
        /// <param name="loader"></param>
        /// <param name="model"></param>
        public ViewerForm(DeckMarshalService loader, PresenterModel model) {
            Trace.WriteLine("Start ViewerForm Ctor");
            this.SuspendLayout();
            this.m_EventQueue = new ControlEventQueue(this);

            this.m_Model = model;
            this.m_Marshal = loader;

            UpdateTitle();

            FontSetting();

            this.Font = ViewerStateModel.FormFont;
            this.Name = "ViewerForm";

            //Retrieve the icon
            System.Reflection.Assembly thisExe = System.Reflection.Assembly.GetExecutingAssembly();
            Trace.WriteLine("Get Icon");

            this.Icon = new Icon( thisExe.GetManifestResourceStream( "UW.ClassroomPresenter.Presenter.ico" ) );

            // The form should fill 5/6 of the screen by default.
            // FIXME: Load/Store previous window size from/in the registry.
            this.Size = new Size(SystemInformation.WorkingArea.Size.Width * 5 / 6,
                SystemInformation.WorkingArea.Size.Height * 5 / 6);
            // the form should be no smaller than 1/2 it's original size
            this.MinimumSize = new Size(this.Size.Width / 2, this.Size.Height / 2);

            Menus.FileMenu.CloseFormDelegate cfd = new UW.ClassroomPresenter.Viewer.Menus.FileMenu.CloseFormDelegate(this.Close);
            this.Menu = new Menus.ViewerMainMenu(this.m_EventQueue, this.m_Model, loader, cfd);

            Trace.WriteLine("Create tool bars");

            this.m_MainToolBars = new MainToolBars(this.m_Model, this.m_EventQueue);
            //this.m_ClassroomBrowser = new AutoDisplayClassroomsBrowser(this.m_Model);
            Trace.WriteLine("Make StartupForm");

            this.m_StartupForm = new StartupForm(this.m_Model, this.m_EventQueue);
            Trace.WriteLine("Make ViewerPresentationLayout");

            this.m_PresentationLayout = new ViewerPresentationLayout(this.m_Model);

            //this.m_ClassroomBrowser.Dock = DockStyle.Fill;
            this.m_PresentationLayout.Dock = DockStyle.Fill;

            this.Controls.Add(this.m_PresentationLayout);
            //this.Controls.Add(this.m_ClassroomBrowser);
            this.m_StartupForm.Visible = false;

            this.Controls.Add(this.m_MainToolBars.m_MainToolBar);
            this.Controls.Add(this.m_MainToolBars.m_MainClassmateToolBar);
            this.Controls.Add(this.m_MainToolBars.m_ExtraClassmateToolBar);

            this.ResumeLayout(false);

            Trace.WriteLine("Make KeyEventHandler");

            this.KeyDown += new KeyEventHandler(this.OnKeyDown);
            this.KeyPreview = true;

            ///add listeners for HumanName, Role, Stylus properties
            role_listener_ = new EventQueue.PropertyEventDispatcher(this.m_EventQueue, new PropertyEventHandler(this.ParticipantRoleChanged));
            stylus_listener_ = new EventQueue.PropertyEventDispatcher(this.m_EventQueue, new PropertyEventHandler(this.StylusChanged));
            instructor_listener_ = new EventQueue.PropertyEventDispatcher(this.m_EventQueue, new PropertyEventHandler(this.InstructorChanged));


            this.m_Model.Participant.Changed["Role"].Add(role_listener_.Dispatcher);
            this.m_Model.Changed["Stylus"].Add(stylus_listener_.Dispatcher);
            this.m_Model.Network.Changed["Association"].Add(instructor_listener_.Dispatcher);

            //Add listener for the version exchange warning pop-up
            this.m_VersionExchangePopUpListener = new EventQueue.PropertyEventDispatcher(this.m_EventQueue, new PropertyEventHandler(this.PopUpWarningHandler));
            this.m_Model.VersionExchange.Changed["PopUpWarningMessage"].Add(m_VersionExchangePopUpListener.Dispatcher);

            Trace.WriteLine("Make EventDispatcher");

            //Add listener for NetworkStatus
            m_NetworkStatusListener = new EventQueue.PropertyEventDispatcher(this.m_EventQueue, new PropertyEventHandler(this.NetworkStatusChanged));
            this.m_Model.Network.Changed["NetworkStatus"].Add(m_NetworkStatusListener.Dispatcher);

            Trace.WriteLine("Make FullScreenAdapter");

            this.m_FullScreenAdapter = new FullScreenAdapter(this.m_Model, this.m_PresentationLayout);

            instructor_listener_.Dispatcher(this, null);
            role_listener_.Dispatcher(this, null);
            stylus_listener_.Dispatcher(this, null);

            this.Activated += this.ShowStartup;
            this.FormClosing += this.SaveClose;

            //Store local DPI settings which are used to make sure ink is scaled correctly.
            using (Graphics g = this.CreateGraphics()) {
                ViewerStateModel.SetLocalDpi(g.DpiX, g.DpiY);
            }
            Trace.WriteLine("ViewerForm Ctor done.");

        }

        void ShowStartup(object sender, EventArgs ea) {
            //We only want this to be called the first time it's activated...  look for a better event to use here.
            this.Activated -= this.ShowStartup;
            if (this.StandAlone) {
                this.m_StartupForm.StandaloneStartup();
            }
            else {
                this.m_StartupForm.ShowDialog();
            }

            if (this.OpenInputFiles != null) {
                foreach (string inputFile in this.OpenInputFiles) {
                    if (inputFile != null && inputFile != "") {
                        Decks.OpenDeckDialog odd = new OpenDeckDialog(this.m_Model, this.m_Marshal);
                        odd.OpenDeck(new FileInfo(inputFile));
                    }
                }
            }
        }

        /// <summary>
        /// Handler for FormClosing event
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="ea"></param>
        /// Note: in some cases in the capability context this gets called more than once!
        void SaveClose(object sender, EventArgs ea) {
            bool dirty = this.Dirty();
            bool saveonclose = true;
            using (Synchronizer.Lock(this.m_Model.ViewerState.SyncRoot)) {
                saveonclose = this.m_Model.ViewerState.SaveOnClose;
            }

            if ((saveonclose) && (dirty) && (!m_Saved)) {
                using (Synchronizer.Lock(this.m_Model.Workspace.CurrentDeckTraversal.SyncRoot)) {
                    if (this.m_Model.Workspace.CurrentDeckTraversal.Value != null) {
                        SaveOnCloseDialog sd = new SaveOnCloseDialog(this.m_Model, this.m_Marshal, ea);
                        sd.ShowDialog();
                        if(!sd.Cancel)m_Saved = true;
                    }
                }
            }

#if RTP_BUILD
            if (m_Capability != null) {
                if (m_Capability.IsSending) {
                    m_Capability.StopSending();
                }
            }
            if (m_Capability != null) {
                if (m_Capability.IsPlaying) {
                    m_Capability.StopPlaying();
                }
            }
#endif
        }

        private bool Dirty() {
            using (Synchronizer.Lock(this.m_Model.Workspace.DeckTraversals.SyncRoot)) {
                foreach (DeckTraversalModel DT in this.m_Model.Workspace.DeckTraversals) {
                    using (Synchronizer.Lock(DT.Deck.SyncRoot)) {
                        if (DT.Deck.Dirty)
                            return true;
                    }
                }
            }
            return false;
        }
 
        private void FontSetting() {
            string family = SystemFonts.MessageBoxFont.Name;
            float size = SystemFonts.MessageBoxFont.Size;

            ViewerStateModel.SetUIFont(family, size);
        }

        private void NetworkStatusChanged(object o, PropertyEventArgs args) {
            this.UpdateTitle();
        }

        #region Julia's Code EDIT

        /// <summary>
        /// if the stylus becomes a text stylus, we should not allow any sort of funky
        /// key input to toggle slides
        /// </summary>
        /// <param name="o"></param>
        /// <param name="e"></param>
        private void StylusChanged(object o, PropertyEventArgs e) {
            using (Synchronizer.Lock(this.m_Model.SyncRoot)) {
                if (this.m_Model.Stylus is TextStylusModel) {
                    this.key_input_allowed_ = false;
                } else {
                    this.key_input_allowed_ = true;
                }
            }
        }

        /// <summary>
        /// When we change the participant, we need to make sure we update this in the form's title. This 
        /// method does just that.
        /// </summary>
        /// <param name="o"></param>
        /// <param name="e"></param>
        private void ParticipantRoleChanged(object o, PropertyEventArgs e) {
            ///update classroom and instructor information as well.
            InstructorChanged(null, null);

            UpdateTitle();
        }

        /// <summary>
        /// Whenever we change instructors (ie change clasrooms, since an instructor cannot be in two classrooms
        /// at once)we should update the instructor and classroom information on the title bar.
        /// </summary>
        /// <param name="o"></param>
        /// <param name="args"></param>
        private void InstructorChanged(object o, PropertyEventArgs args) {
            UpdateTitle();
        }

        /// <summary>
        /// If the version exchange finds a serious problem, this responds to the event to throw up a message box.
        /// </summary>
        /// <param name="o"></param>
        /// <param name="args"></param>
        private void PopUpWarningHandler(object o, PropertyEventArgs args) {
            PropertyChangeEventArgs pcea = (PropertyChangeEventArgs)args;
            MessageBox.Show(this,(string)pcea.NewValue, "Version Compatibility Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        /// <summary>
        /// updates the form's title, including version information and information about the status of the user (role, and location).
        /// </summary>
        private void UpdateTitle() {
            StringBuilder title = new StringBuilder();
            title.Append(Application.ProductName);

#if STUDENT_CLIENT_ONLY
            title.Append(" " + Strings.StudentClient);
#endif

#if DEBUG
            title.Append(" (" + Strings.Version + " ");
            string[] version = Application.ProductVersion.Split('.');
            for (int i = 0; ; ) {
                title.Append(version[i]);
                if(++i >= 3 || i >= version.Length)
                    break;
                title.Append(".");
            }
            title.Append(")");
#endif

            RoleModel role;
            using (Synchronizer.Lock(this.m_Model.Participant.SyncRoot))
                role = this.m_Model.Participant.Role;

            do {
                if (role != null) {
                    title.Append(": ");
                    if (role is StudentModel) {
                        title.Append(Strings.TitleRoleStudent);
                    } else if (role is InstructorModel) {
                        title.Append(Strings.TitleRoleInstructor);
                        // Do not append the "(Instructor:)" string below.
                        break;
                    } else if (role is PublicModel) {
                        title.Append(Strings.TitleRolePublic);
                    } else {
                        break;
                    }

                    using (Synchronizer.Lock(m_Model.Network.SyncRoot)) {
                        if (m_Model.Network.Association != null) {
                            using (Synchronizer.Lock(m_Model.Network.Association.SyncRoot)) {
                                if (m_Model.Network.Association.HumanName != null) {
                                    title.Append(" (" + Strings.TitleRoleInstructor + ": ");
                                    // FIXME: In the TCP case, HumanName is always null.
                                    title.Append(m_Model.Network.Association.HumanName.ToString());
                                    title.Append(")");
                                }
                            }
                        }
                    }
                }
            } while (false);

            title.Append("; ");
            using (Synchronizer.Lock(m_Model.Network.SyncRoot)) {
                title.Append(this.m_Model.Network.NetworkStatus.ToString());
            }

            this.Text = title.ToString();
        }

        #endregion

 
        protected override void Dispose(bool disposing) {
#if RTP_BUILD
            //There are some things we need to dispose only in the capability context
            if ((m_Capability != null) && (disposing)) {
                DisposeCapabilityResources();
            }
#endif

            try {
                this.m_EventQueue.Dispose();
            } finally {
                base.Dispose(disposing);
            }
        }


        protected override bool IsInputKey(Keys keyData) {
            //Only override arrow keys
            if (keyData == Keys.Up ||
                keyData == Keys.Down ||
                keyData == Keys.Left ||
                keyData == Keys.Right) {
                return true;
            } else {
                return false;
            }
        }

        private void OnKeyDown(object sender, KeyEventArgs e) {
            ///if we are using the text stylus (key input not allowed), we should not
            ///do anything funky with the keys.
            if (!key_input_allowed_) {
                return;
            }
            if (e.KeyCode == Keys.P ||
                e.KeyCode == Keys.PageUp ||
                e.KeyCode == Keys.Back ||
                e.KeyCode == Keys.Left ||
                e.KeyCode == Keys.Up ||
                e.KeyCode == Keys.NumPad4 ||
                e.KeyCode == Keys.NumPad8) {
                //Previous Slide
                using(this.m_Model.Workspace.Lock()) {
                    if( this.m_Model.Workspace.CurrentDeckTraversal != null &&
                        (~this.m_Model.Workspace.CurrentDeckTraversal) != null) {
                        using (Synchronizer.Lock((~this.m_Model.Workspace.CurrentDeckTraversal).SyncRoot)) {
                            if ((~this.m_Model.Workspace.CurrentDeckTraversal).Previous != null) {
                                (~this.m_Model.Workspace.CurrentDeckTraversal).Current = (~this.m_Model.Workspace.CurrentDeckTraversal).Previous;
                            }
                        }
                    }
                }
                e.Handled = true;
            } else if ((e.Alt && e.KeyCode == Keys.F11) || 
                (e.Alt && e.KeyCode == Keys.Enter)) {
                //Toggle Full Screen
                using(Synchronizer.Lock(this.m_Model.ViewerState.SyncRoot)) {
                    this.m_Model.ViewerState.PrimaryMonitorFullScreen ^= true;
                }

                e.Handled = true;
            } else if (e.KeyCode == Keys.N ||
                e.KeyCode == Keys.Right ||
                e.KeyCode == Keys.PageDown ||
                e.KeyCode == Keys.Space ||
                e.KeyCode == Keys.Down ||
                e.KeyCode == Keys.Enter ||
                e.KeyCode == Keys.NumPad6 ||
                e.KeyCode == Keys.NumPad2) {
                //Next Slide
                using(this.m_Model.Workspace.Lock()) {
                    if (this.m_Model.Workspace.CurrentDeckTraversal != null &&
                        (~this.m_Model.Workspace.CurrentDeckTraversal) != null) {
                        using (Synchronizer.Lock((~this.m_Model.Workspace.CurrentDeckTraversal).SyncRoot)) {
                            if ((~this.m_Model.Workspace.CurrentDeckTraversal).Next != null) {
                                (~this.m_Model.Workspace.CurrentDeckTraversal).Current = (~this.m_Model.Workspace.CurrentDeckTraversal).Next;
                            }
                        }
                    }
                }
                e.Handled = true;
            } else if (e.KeyCode == Keys.Escape) {
                //Exit Full Screen
                using(Synchronizer.Lock(this.m_Model.ViewerState.SyncRoot)) {
                    this.m_Model.ViewerState.PrimaryMonitorFullScreen = false;
                }

                e.Handled = true;
            }
        }

//        /// <summary>
//        /// The main entry point for the application.
//        /// </summary>
//        [STAThread]
//        static void Main(string[] args) {
//#if LAUNCH_TWO_VIEWERS
//            Thread second = new Thread(new ThreadStart(ViewerThreadStart));
//            second.Name = "Second Viewer";
//            second.Start();
//#endif
//            //Parse the input arguments
//            string inputFile = null;
//            for (int i = 0; i < args.Length; i++) {
//                if ("--input".StartsWith(args[i])) {
//                    if ((i + 1) >= args.Length) {
//                        Console.WriteLine("Missing file argument for --input");
//                        Usage();
//                        return;
//                    }
//                    if ("--".StartsWith(args[i+1])) {
//                        Console.WriteLine("Missing file argument for --input");
//                        Usage();
//                        return;
//                    }
//                    inputFile = args[i+1];
//                    i++;
//                    if (!File.Exists(inputFile)) {
//                        Console.WriteLine("File not found: " + inputFile);
//                        Usage();
//                        return;
//                    }
//                } else {
//                    Console.WriteLine("Invalid argument: " + args[i]);
//                    Usage();
//                    return;
//                }
//            }

//            ViewerThreadStart(inputFile);
//        }

#if LAUNCH_TWO_VIEWERS
        /// <summary>
        /// For use when <c>LAUNCH_TWO_VIEWERS</c> is <c>#define</c>d.
        /// </summary>
        private static void ViewerThreadStart() {
            ViewerThreadStart("");
        }
#endif
        [System.Runtime.InteropServices.DllImport("kernel32.dll")]
        public static extern bool SetProcessWorkingSetSize(IntPtr proc, int min, int max);

        public static void ViewerThreadStart(List<string> inputFiles, bool standalone) {
            Trace.WriteLine("ViewerThreadStart starting");
            //FIXME: Initialize model from values in registry.
            Application.EnableVisualStyles();
            Application.DoEvents();
            Process p = Process.GetCurrentProcess();
            SetProcessWorkingSetSize(p.Handle, -1, -1);

#if WEBSERVER
            // Create the web service
            Web.WebService web_server = new Web.WebService( null, "9090" );
#endif
            
#if DEBUG && LOGGING
            LoggingService logger = new LoggingService();
#endif
            Trace.WriteLine("Make PresenterModel");

            PresenterModel model = new PresenterModel();

            Trace.WriteLine("Make DeckMarshalService");

            using (DeckMarshalService loader = new DefaultDeckMarshalService()) {
                Trace.WriteLine("Construct ViewerForm");

                ViewerForm viewer = new ViewerForm(loader, model);
                ConnectionManager rtp = null;
#if RTP_BUILD
                Trace.WriteLine("Make RTPConnectionManager");

                rtp = new RTPConnectionManager(model);
#endif
                ConnectionManager tcp = new TCPConnectionManager(model);

#if RTP_BUILD
                Trace.WriteLine("Make BroadcastManager");

                BroadcastManager bm = new BroadcastManager(model, (TCPConnectionManager)tcp, (RTPConnectionManager)rtp);
#else
                BroadcastManager bm = new BroadcastManager(model, (TCPConnectionManager)tcp);
#endif

                using (rtp) {
                    using (tcp) {
                        using (bm) {
                            Trace.WriteLine("Make ViewerStateService");

                            using (ViewerStateService viewerState = new ViewerStateService(model.ViewerState)) {
                                using (RegistryService registry = new RegistryService(model.ViewerState)) {
                                    using (RegistryService penreg = new RegistryService(model.PenState)) {
#if DEBUG && LOGGING
                                        // Attach the logger to the model
                                        logger.AttachToModel(model.ViewerState);
#endif

#if STUDENT_CLIENT_ONLY
                                        using (Synchronizer.Lock(model.ViewerState.SyncRoot)){
                                            model.ViewerState.iRole = 1;
                                        }
#endif
                                        // Start the printing service
                                        PrintingService printing = new PrintingService(model.ViewerState);

                                        using (WorkspaceUndoService undo = new WorkspaceUndoService(viewer.m_EventQueue, model)) {

                                            using (Synchronizer.Lock(model.Network.SyncRoot)) {
                                                model.Network.Protocols.Add(tcp.Protocol);
#if RTP_BUILD
                                                model.Network.Protocols.Add(rtp.Protocol);
#endif
                                            }

                                            using (NetworkAssociationService association = new NetworkAssociationService(viewer.m_EventQueue, model)) {
                                                using (DeckMatcherService deckMatcher = new DeckMatcherService(viewer.m_EventQueue, model)) {

                                                    // Set the Initial Deck to Load if we are Loading from an Icon
                                                    viewer.OpenInputFiles = inputFiles;
                                                    viewer.StandAlone = standalone;
                                                    Trace.WriteLine("Ready to run viewer");

                                                    Application.Run(viewer);

                                                }
                                            }
                                        }
                                        penreg.SaveAllProperties();
                                    }

                                    registry.SaveAllProperties();
                                }
                            }
                        }
                    }
                }
            }

#if WEBSERVER
            // Dispose of the web server
            web_server.Dispose();
#endif
        }

        private static void Usage() {
            Console.WriteLine(Application.ProductName + " (Version " + Application.ProductVersion + ")");
            Console.WriteLine("Usage: " + Application.ExecutablePath + " [--input <file>]");
            Console.WriteLine("Arguments:");
            Console.WriteLine("--input      Specify a PPT or CP3 file to open with Presenter");
        }
    }
}
