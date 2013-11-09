// $Id: FullScreenAdapter.cs 1430 2007-08-31 23:04:48Z linnell $

using System;
using System.Drawing;
using System.Diagnostics;
using System.Windows.Forms;

using UW.ClassroomPresenter.Model;
using UW.ClassroomPresenter.Model.Viewer;
using UW.ClassroomPresenter.Model.Network;


namespace UW.ClassroomPresenter.Viewer.SecondMonitor {
    public class FullScreenAdapter {
        private readonly PresenterModel m_Model;
        private readonly ContainerControl m_Control;
        private Form m_ParentForm;

        private readonly ControlEventQueue m_EventQueue;
        private readonly EventQueue.PropertyEventDispatcher m_FullScreenChangedDispatcher;
        private readonly EventQueue.PropertyEventDispatcher m_RoleChangedDispatcher;

        private bool m_Disposed;

        private bool m_FullScreen;

        private DockStyle m_OldDock;
        private AnchorStyles m_OldAnchor;
        private Rectangle m_OldBounds;

        private MainMenu m_OldParentMenu;
        private FormBorderStyle m_OldParentFormBorderStyle;
        private Rectangle m_OldParentBounds;
        private bool m_OldShowFilmStrip;

        private FullScreenButtons m_FullScreenButtons;

        public FullScreenAdapter(PresenterModel model, ContainerControl control) {
            this.m_Model = model;
            this.m_Control = control;

            this.m_EventQueue = new ControlEventQueue(this.m_Control);
            this.m_FullScreenChangedDispatcher = new EventQueue.PropertyEventDispatcher(this.m_EventQueue, new PropertyEventHandler(this.HandleFullScreenChanged));
            this.m_RoleChangedDispatcher = new EventQueue.PropertyEventDispatcher(this.m_EventQueue, new PropertyEventHandler(this.HandleRoleChanged));

            this.m_Model.ViewerState.Changed["PrimaryMonitorFullScreen"].Add(this.m_FullScreenChangedDispatcher.Dispatcher);
            this.m_FullScreenChangedDispatcher.Dispatcher(this, null);

            this.m_Model.Participant.Changed["Role"].Add(this.m_RoleChangedDispatcher.Dispatcher);
            this.m_RoleChangedDispatcher.Dispatcher(this, null);
            this.m_FullScreenButtons = new FullScreenButtons(this.m_Model, this.m_EventQueue);
            //this.m_FullScreenToolBar.Dock = DockStyle.Top;

        }

        ~FullScreenAdapter() {
            this.Dispose(false);
        }

        public void Dispose() {
            this.Dispose(true);
            System.GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing) {
            if(this.m_Disposed) return;
            if(disposing) {
                this.m_Model.ViewerState.Changed["PrimaryMonitorFullScreen"].Remove(this.m_FullScreenChangedDispatcher.Dispatcher);
                this.m_Model.Participant.Changed["Role"].Remove(this.m_RoleChangedDispatcher.Dispatcher);
                if(this.m_FullScreen)
                    this.m_EventQueue.Post(new EventQueue.EventDelegate(this.ExitFullScreenMode));
            }
            this.m_Disposed = true;
        }

        private void HandleRoleChanged(object sender, PropertyEventArgs args) {
            using(Synchronizer.Lock(this.m_Model.ViewerState.SyncRoot)) {
                using(Synchronizer.Lock(this.m_Model.Participant.SyncRoot)) {
                    this.m_Model.ViewerState.PrimaryMonitorFullScreen = this.m_Model.Participant.Role is PublicModel;
                }
            }
        }

        private void HandleFullScreenChanged(object sender, PropertyEventArgs args) {
            if (this.m_ParentForm == null) {
                this.m_ParentForm = this.m_Control as Form;
                if (this.m_ParentForm == null)
                    this.m_ParentForm = this.m_Control.ParentForm;
            } else if (this.m_Control is Form ? this.m_ParentForm != this.m_Control : this.m_ParentForm != this.m_Control.ParentForm)
                throw new InvalidOperationException("The control's parent form has changed while in full-screen mode.");

            if (this.m_ParentForm == null)
                throw new InvalidOperationException("Full-screen mode is not available unless the control has been assigned to a Form.");

            using (Synchronizer.Lock(this.m_Model.ViewerState.SyncRoot)) {
                if (this.m_FullScreen == (this.m_FullScreen = this.m_Model.ViewerState.PrimaryMonitorFullScreen))
                    return;
            }

            // ExitFullScreenMode sets this.m_ParentForm to null, so save a copy.
            Form parent = this.m_ParentForm;

            parent.SuspendLayout();
            try {
                this.m_Control.SuspendLayout();
                try {
                    if (this.m_FullScreen) {
                        this.EnterFullScreenMode();
                    } else {
                        this.ExitFullScreenMode();
                    }
                } finally {
                    this.m_Control.ResumeLayout();
                }
            } finally {
                parent.ResumeLayout();
            }
        }

        private void EnterFullScreenMode() {
            this.m_OldAnchor = this.m_Control.Anchor;
            this.m_OldDock = this.m_Control.Dock;
            this.m_OldBounds = this.m_Control.Bounds;

            this.m_OldParentMenu = this.m_ParentForm.Menu;
            this.m_OldParentFormBorderStyle = this.m_ParentForm.FormBorderStyle;
            this.m_OldParentBounds = this.m_ParentForm.Bounds;

            bool publicdisplay = false;

            using (Synchronizer.Lock(this.m_Model.SyncRoot)) {
                using (Synchronizer.Lock(this.m_Model.ViewerState.SyncRoot)) {
                        this.m_OldShowFilmStrip = this.m_Model.ViewerState.FilmStripEnabled;
                        this.m_Model.ViewerState.FilmStripEnabled = false;
                }
            }
            ((ViewerPresentationLayout)(this.m_Control)).m_DeckStrip.Visible = false ;
            using (Synchronizer.Lock(this.m_Model.SyncRoot)) {
                using (Synchronizer.Lock(this.m_Model.Participant.SyncRoot)) {
                    using (Synchronizer.Lock(this.m_Model.Participant.Role.SyncRoot)) {
                        publicdisplay = this.m_Model.Participant.Role is PublicModel;
                    }
                }
            }

            this.m_Control.Anchor = AnchorStyles.None;
            this.m_Control.Dock = DockStyle.None;

            // FIXME: Instead of removing the menu, set its location to just above the top of the screen.
            this.m_ParentForm.Menu = null;
            this.m_ParentForm.FormBorderStyle = FormBorderStyle.None;
            this.m_ParentForm.Bounds = Screen.GetBounds(this.m_Control);

            this.m_Control.Bounds = (this.m_Control is Form ? this.m_Control : this.m_Control.ParentForm).ClientRectangle;

            if (!publicdisplay) {
                this.m_FullScreenButtons.AddButtons((ViewerPresentationLayout)(this.m_Control));
            }

        }

        private void ExitFullScreenMode() {
            Debug.Assert(this.m_Control.Dock == DockStyle.None, "The control's state cannot be modified while in full-screen mode.");
            Debug.Assert(this.m_Control.Anchor == AnchorStyles.None, "The control's state cannot be modified while in full-screen mode.");

            Debug.Assert(this.m_ParentForm.Menu == null, "The control's state cannot be modified while in full-screen mode.");
            Debug.Assert(this.m_ParentForm.FormBorderStyle == FormBorderStyle.None, "The control's state cannot be modified while in full-screen mode.");
            using (Synchronizer.Lock(this.m_Model.Participant.SyncRoot)) {
                using (Synchronizer.Lock(this.m_Model.Participant.Role.SyncRoot)) {
                    //if (this.m_Model.Participant.Role is InstructorModel) {
                        ((ViewerPresentationLayout)(this.m_Control)).m_DeckStrip.Visible = true; // this.m_OldShowFilmStrip;
                  /*  }
                    else {
                        ((ViewerPresentationLayout)(this.m_Control)).m_DeckStrip.Visible = this.m_OldShowFilmStrip;
                    }*/
                }
            }
            this.m_ParentForm.Bounds = this.m_OldParentBounds;
            this.m_ParentForm.FormBorderStyle = this.m_OldParentFormBorderStyle;
            this.m_ParentForm.Menu = this.m_OldParentMenu;

            this.m_Control.Bounds = this.m_OldBounds;
            this.m_Control.Dock = this.m_OldDock;
            this.m_Control.Anchor = this.m_OldAnchor;
            using (Synchronizer.Lock(this.m_Model.SyncRoot)) {
                using (Synchronizer.Lock(this.m_Model.ViewerState.SyncRoot)) {
                    this.m_Model.ViewerState.FilmStripEnabled = this.m_OldShowFilmStrip;
                }
            }

            
            this.m_OldDock = (DockStyle) 0;
            this.m_OldAnchor = (AnchorStyles) 0;
            this.m_OldBounds = Rectangle.Empty;

            this.m_OldParentMenu = null;
            this.m_OldParentBounds = Rectangle.Empty;
            this.m_OldParentFormBorderStyle = (FormBorderStyle) 0;

            this.m_FullScreenButtons.RemoveButtons((ViewerPresentationLayout)(this.m_Control));

            this.m_ParentForm = null;
        }
    }
}
