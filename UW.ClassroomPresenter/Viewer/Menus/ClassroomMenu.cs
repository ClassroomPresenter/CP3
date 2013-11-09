// $Id: ClassroomMenu.cs 1452 2007-09-13 04:22:58Z linnell $

using System;
using System.Diagnostics;
using System.Windows.Forms;

using UW.ClassroomPresenter.Model;
using UW.ClassroomPresenter.Model.Network;
using UW.ClassroomPresenter.Model.Presentation;
using UW.ClassroomPresenter.Model.Workspace;
using UW.ClassroomPresenter.Viewer.Classrooms;

namespace UW.ClassroomPresenter.Viewer.Menus {
    /// <summary>
    /// The root menu for the Connect menu
    /// </summary>
    /// 

    public class ConnectMenu : MenuItem {
        public ConnectMenu(ControlEventQueue dispatcher, PresenterModel model) {
            this.Text = "&Connect";
            this.MenuItems.Add(new ConnectDialogMenuItem(dispatcher, model));
        }

    }
    public class ConnectDialogMenuItem : MenuItem {
        private readonly StartupForm m_Startup;
        private PresenterModel m_Model;
        public ConnectDialogMenuItem(ControlEventQueue dispatcher, PresenterModel model) {
            this.Text = "Launch Connection Dialog...";
            this.m_Startup = new StartupForm(model, dispatcher);
            this.m_Model = model;
        }
        protected override void OnClick(EventArgs e) {
            base.OnClick(e);
            using (Synchronizer.Lock(this.m_Model.Workspace.CurrentPresentation.SyncRoot)){
            this.m_Model.Workspace.CurrentPresentation.Value = null;}
            this.m_Startup.ShowDialog();
        }
    }
    /*public class ConnectMenu : MenuItem {
        public ConnectMenu(ControlEventQueue dispatcher, PresenterModel model) {
            this.Text = "&Connect";
            PresentationsMenu pm = new PresentationsMenu();
            this.MenuItems.Add(new ManualConnectionMenu(dispatcher, model, pm));
            this.MenuItems.Add(pm);

            this.MenuItems.Add("-");
            this.MenuItems.Add(new ConnectTCPMenuItem(dispatcher, model));
        }
    }*/

    /// <summary>
    /// The menu subitem that lists all available presentations.
    /// </summary>
    /// <remarks>
    /// This menu is populated dynamically by the ManualConnectionMenu, as connections are made to classrooms.
    /// </remarks>
    public class PresentationsMenu : MenuItem {
        public PresentationsMenu() {
            this.Text = "&Presentations";
        }
    }

    /// <summary>
    /// The <see cref="ManualConnectionMenu"/> displays a list of available classrooms.  It also handles
    /// updating the list of presentations.
    /// </summary>
    /// <remarks>
    /// The list of classrooms and presentations is divided into two sections.  The first
    /// consists of <see cref="ClassroomMenuItem"/>s, managed by a <see cref="ClassroomCollectionHelper"/>,
    /// and allows the user to connect or disconnect from the classrooms available in the current
    /// set of network protocols.  This is located in the Manual Connection menu.
    /// The second consists of <see cref="PresentationMenuItem"/>s, managed by a
    /// <see cref="PresentationCollectionHelper"/> for each classroom, and allows the user to select
    /// exactly one current presentation.  This is located in the Presentations menu.
    /// </remarks>
    public class ManualConnectionMenu : MenuItem {
        private readonly ControlEventQueue m_EventQueue;
        private readonly PresenterModel m_Model;
        private readonly ProtocolCollectionHelper m_ProtocolCollectionHelper;
        private bool m_Disposed;

        /// <summary>
        /// Creates a new <see cref="ClassroomMenu"/>.
        /// </summary>
        /// <param name="model">
        /// The lists of classrooms and presentations are gathered from the
        /// protocols in the <see cref="PresenterModel.Network"/>.
        /// </param>
        public ManualConnectionMenu(ControlEventQueue dispatcher, PresenterModel model, PresentationsMenu pm) {
            this.m_EventQueue = dispatcher;
            this.m_Model = model;
            this.Text = "&Manual Connection";

            this.m_ProtocolCollectionHelper = new ProtocolCollectionHelper(this, pm, this.m_Model.Network);
        }

        protected override void Dispose(bool disposing) {
            if(this.m_Disposed) return;
            try {
                if(disposing) {
                    this.m_ProtocolCollectionHelper.Dispose();
                }
            } finally {
                base.Dispose(disposing);
            }
            this.m_Disposed = true;
        }

        /// <summary>
        /// Manages the set of <see cref="ClassroomCollectionHelper"/>s, one for each
        /// <see cref="ProtocolModel"/> in the <see cref="NetworkModel"/>.
        /// </summary>
        private class ProtocolCollectionHelper : PropertyCollectionHelper {
            private readonly ManualConnectionMenu m_Parent;
            private readonly PresentationsMenu m_PresentationsMenu;

            public ProtocolCollectionHelper(ManualConnectionMenu parent, PresentationsMenu presMenu, NetworkModel network) : base(parent.m_EventQueue, network, "Protocols") {
                this.m_Parent = parent;
                this.m_PresentationsMenu = presMenu;
                base.Initialize();
            }

            protected override void Dispose(bool disposing) {
                if (disposing)
                    foreach (IDisposable disposable in this.Tags)
                        if (disposable != null)
                            disposable.Dispose();
                base.Dispose(disposing);
            }

            protected override object SetUpMember(int index, object member) {
                return new ClassroomCollectionHelper(this.m_Parent, m_PresentationsMenu, ((ProtocolModel) member));
            }

            protected override void TearDownMember(int index, object member, object tag) {
                if(tag != null)
                    ((ClassroomCollectionHelper) tag).Dispose();
            }
        }


        /// <summary>
        /// Manages the sets of <see cref="ClassroomMenuItem"/>s and <see cref="PresentationCollectionHelper"/>s,
        /// one for each <see cref="ClassroomModel"/> in each <see cref="ProtocolModel"/> of the <see cref="NetworkModel"/>.
        /// </summary>
        /// <remarks>
        /// When <see cref="ClassroomMenuItem"/>s are created, they are inserted at the index before
        /// that of the <c>m_ClassroomsSeparator</c> item in the parent <see cref="ClassroomMenu"/>.
        /// This way the list of classrooms is kept separate from the list of presentations.
        /// </remarks>
        private class ClassroomCollectionHelper : PropertyCollectionHelper {
            private readonly ManualConnectionMenu m_Parent;
            private readonly PresentationsMenu m_PresentationsMenu;

            public ClassroomCollectionHelper(ManualConnectionMenu parent, PresentationsMenu presMenu, ProtocolModel protocol) : base(parent.m_EventQueue, protocol, "Classrooms") {
                this.m_Parent = parent;
                this.m_PresentationsMenu = presMenu;
                base.Initialize();
            }

            protected override void Dispose(bool disposing) {
                if(disposing) {
                    foreach(PresentationCollectionHelper disposable in this.Tags) {
                        disposable.MenuItem.Dispose();
                        disposable.Dispose();
                    }
                }
                base.Dispose (disposing);
            }

            protected override object SetUpMember(int index, object member) {
                ClassroomModel classroom = ((ClassroomModel) member);

                ClassroomMenuItem item = new ClassroomMenuItem(this.m_Parent.m_EventQueue, this.m_Parent.m_Model, classroom);
                this.m_Parent.MenuItems.Add(item);

                PresentationCollectionHelper helper = new PresentationCollectionHelper(this.m_Parent, this.m_PresentationsMenu, classroom);
                helper.MenuItem = item;
                return helper;
            }

            protected override void TearDownMember(int index, object member, object tag) {
                if(tag != null) {
                    PresentationCollectionHelper helper = ((PresentationCollectionHelper) tag);

                    MenuItem item = helper.MenuItem;
                    this.m_Parent.MenuItems.Remove(item);

                    item.Dispose();
                    helper.Dispose();
                }
            }
        }

        /// <summary>
        /// Represents a <see cref="ClassroomModel"/> in the <see cref="ClassroomMenu"/>.
        /// </summary>
        /// <remarks>
        /// Clicking a <see cref="ClassroomMenuItem"/> toggles the <see cref="ClassroomModel.Connected"/> state.
        /// When the associated <see cref="ClassroomModel"/>'s <see cref="ClassroomModel.Connected"/> property
        /// is <c>true</c>, the menu item is <em>checked</em>.
        /// </remarks>
        private class ClassroomMenuItem : MenuItem {
            private readonly EventQueue m_EventQueue;
            private readonly EventQueue.PropertyEventDispatcher m_HumanNameChangedDispatcher;
            private readonly EventQueue.PropertyEventDispatcher m_ConnectedChangedDispatcher;

            private readonly PresenterModel m_Model;
            private readonly ClassroomModel m_Classroom;
            private bool m_Disposed;

            public ClassroomMenuItem(ControlEventQueue dispatcher, PresenterModel model, ClassroomModel classroom) {
                this.m_EventQueue = dispatcher;
                this.m_Model = model;
                this.m_Classroom = classroom;

                this.m_HumanNameChangedDispatcher = new EventQueue.PropertyEventDispatcher(this.m_EventQueue, new PropertyEventHandler(this.HandleHumanNameChanged));
                this.m_ConnectedChangedDispatcher = new EventQueue.PropertyEventDispatcher(this.m_EventQueue, new PropertyEventHandler(this.HandleConnectedChanged));

                this.m_Classroom.Changed["HumanName"].Add(this.m_HumanNameChangedDispatcher.Dispatcher);
                this.m_Classroom.Changed["Connected"].Add(this.m_ConnectedChangedDispatcher.Dispatcher);

                this.m_HumanNameChangedDispatcher.Dispatcher(this, null);
                this.m_ConnectedChangedDispatcher.Dispatcher(this, null);
            }

            protected override void Dispose(bool disposing) {
                if(this.m_Disposed) return;
                try {
                    if(disposing) {
                        this.m_Classroom.Changed["HumanName"].Remove(this.m_HumanNameChangedDispatcher.Dispatcher);
                        this.m_Classroom.Changed["Connected"].Remove(this.m_ConnectedChangedDispatcher.Dispatcher);
                    }
                } finally {
                    base.Dispose(disposing);
                }
                this.m_Disposed = true;
            }

            private void HandleHumanNameChanged(object sender, PropertyEventArgs args) {
                using(Synchronizer.Lock(this.m_Classroom.SyncRoot)) {
                    if (this.m_Classroom.HumanName != null)
                        this.Text = this.m_Classroom.HumanName;
                }
            }

            private void HandleConnectedChanged(object sender, PropertyEventArgs args) {
                using(Synchronizer.Lock(this.m_Classroom.SyncRoot)) {
                    if (!this.m_Disposed)
                        this.Checked = this.m_Classroom.Connected;
                }
            }

            protected override void OnClick(EventArgs e) {
                base.OnClick(e);

                using(Synchronizer.Lock(this.m_Classroom.SyncRoot)) {
                    this.m_Classroom.Connected = (!this.Checked);
                }
            }
        }

        /// <summary>
        /// Manages the set of <see cref="PresentationMenuItem"/>s, one for each <see cref="PresentationModel"/>
        /// in each <see cref="ClassroomModel"/>.
        /// </summary>
        private class PresentationCollectionHelper : PropertyCollectionHelper {
            private readonly PresentationsMenu m_Parent;
            private readonly ManualConnectionMenu m_ManualConnectionMenu;
            private readonly ClassroomModel m_Classroom;
            internal MenuItem MenuItem;

            public PresentationCollectionHelper(ManualConnectionMenu mcm, PresentationsMenu parent, ClassroomModel classroom) : base(mcm.m_EventQueue, classroom, "Presentations") {
                this.m_Parent = parent;
                this.m_Classroom = classroom;
                this.m_ManualConnectionMenu = mcm;
                base.Initialize();
            }

            protected override void Dispose(bool disposing) {
                if(disposing)
                    this.HandlePropertyChanging(this.Owner, new PropertyChangeEventArgs("Presentations", null, null));
                base.Dispose(disposing);
            }

            protected override object SetUpMember(int index, object member) {
                PresentationMenuItem item = new PresentationMenuItem(this.m_ManualConnectionMenu.m_EventQueue, this.m_ManualConnectionMenu.m_Model, (PresentationModel) member);
                this.m_Parent.MenuItems.Add(item);
                return item;
            }

            protected override void TearDownMember(int index, object member, object tag) {
                if(tag != null) {
                    PresentationMenuItem item = ((PresentationMenuItem) tag);
                    this.m_Parent.MenuItems.Remove(item);
                    item.Dispose();
                }
            }
        }

        /// <summary>
        /// Represents an <see cref="PresentationModel"/> in the <see cref="ClassroomMenu"/>, and establishes
        /// an association with the presentation's owner <see cref="InstructorModel"/>.
        /// </summary>
        /// <remarks>
        /// Clicking a <see cref="PresentationMenuItem"/> causes its associated <see cref="InstructorModel"/>
        /// to be set as the current association of the <see cref="NetworkModel"/>.
        /// <para>
        /// When the associated <see cref="PresentationModel"/> is the current one of the <see cref="WorkspaceModel"/>,
        /// the menu item is <em>checked</em>.
        /// </para>
        /// </remarks>
        private class PresentationMenuItem : MenuItem {
            private readonly ControlEventQueue m_EventQueue;
            private readonly EventQueue.PropertyEventDispatcher m_HumanNameChangedDispatcher;
            private readonly EventQueue.PropertyEventDispatcher m_NetworkAssociationChangedDispatcher;
            private readonly IDisposable m_CurrentPresentationChangedDispatcher;

            private readonly PresenterModel m_Model;
            private readonly PresentationModel m_Presentation;
            private bool m_Disposed;

            public PresentationMenuItem(ControlEventQueue dispatcher, PresenterModel model, PresentationModel presentation) {
                this.m_EventQueue = dispatcher;
                this.m_Model = model;
                this.m_Presentation = presentation;

                this.m_HumanNameChangedDispatcher = new EventQueue.PropertyEventDispatcher(this.m_EventQueue, new PropertyEventHandler(this.HandleHumanNameChanged));

                this.m_Presentation.Changed["HumanName"].Add(this.m_HumanNameChangedDispatcher.Dispatcher);
                this.m_HumanNameChangedDispatcher.Dispatcher(this, null);

                this.m_CurrentPresentationChangedDispatcher =
                    this.m_Model.Workspace.CurrentPresentation.ListenAndInitialize(dispatcher,
                    delegate(Property<PresentationModel>.EventArgs args) {
                        this.Checked = (this.m_Presentation == args.New);
                    });

                this.m_NetworkAssociationChangedDispatcher = new EventQueue.PropertyEventDispatcher(this.m_EventQueue, new PropertyEventHandler(this.HandleNetworkAssociationChanged));
                this.m_Model.Network.Changed["Association"].Add(this.m_NetworkAssociationChangedDispatcher.Dispatcher);
                this.m_NetworkAssociationChangedDispatcher.Dispatcher(this, null);
            }

            protected override void Dispose(bool disposing) {
                if(this.m_Disposed) return;
                try {
                    if(disposing) {
                        this.m_Presentation.Changed["HumanName"].Remove(this.m_HumanNameChangedDispatcher.Dispatcher);
                        this.m_CurrentPresentationChangedDispatcher.Dispose();
                        this.m_Model.Network.Changed["Association"].Remove(this.m_NetworkAssociationChangedDispatcher.Dispatcher);
                    }
                } finally {
                    base.Dispose(disposing);
                }
                this.m_Disposed = true;
            }

            private void HandleHumanNameChanged(object sender, PropertyEventArgs args) {
                using(Synchronizer.Lock(this.m_Presentation.SyncRoot)) {
                    this.Text = this.m_Presentation.HumanName;
                }
            }

            private void HandleNetworkAssociationChanged(object sender, PropertyEventArgs args) {
                // From Bug 754:
                // "Each Participant (in a non-instructor role) may be associated with the
                // Presentation of at most one Participant.  An Instructor is associated with its
                // own Presentation."
                // So if we're already associated with ourselves, disable the menu item
                // because we shouldn't associate with another instructor.
                using (Synchronizer.Lock(this.m_Model.Network.SyncRoot))
                    this.Enabled = (this.m_Model.Network.Association != this.m_Model.Participant);
            }

            protected override void OnClick(EventArgs e) {
                base.OnClick(e);

                using(Synchronizer.Lock(this.m_Model.Network.SyncRoot)) {
                    // From Bug 754:
                    // "Each Participant (in a non-instructor role) may be associated with the
                    // Presentation of at most one Participant.  An Instructor is associated with its
                    // own Presentation."
                    // So if we're already associated with ourselves, don't associate with another instructor.
                    if (this.m_Model.Network.Association != this.m_Model.Participant) {
                        Debug.Assert(this.m_Presentation.Owner != null);
                        this.m_Model.Network.Association = this.m_Presentation.Owner;
                    }
                }
            }
        }
    }
}