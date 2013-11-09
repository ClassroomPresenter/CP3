// $Id: ViewMenu.cs 799 2005-09-27 18:55:08Z shoat $

using System;
using System.Windows.Forms;

using UW.ClassroomPresenter.Decks;
using UW.ClassroomPresenter.Model;
using UW.ClassroomPresenter.Viewer.Slides;
using UW.ClassroomPresenter.Model.Presentation;
using UW.ClassroomPresenter.Model.Viewer;
using UW.ClassroomPresenter.Model.Network;

using UW.ClassroomPresenter.Viewer.FilmStrips;

namespace UW.ClassroomPresenter.Viewer.Menus {
    public class ViewMenu : MenuItem {
        public ViewMenu(ControlEventQueue dispatcher, PresenterModel model) {
            this.Text = Strings.View;
          
            this.MenuItems.Add(new ToggleSlideZoomMenuItem(dispatcher, model));
            this.MenuItems.Add(new FullScreenMenuItem(model));
            this.MenuItems.Add(new MenuItem("-"));  //Separator 
            this.MenuItems.Add(new FilmStripAlignmentMenu(dispatcher, model));
            this.MenuItems.Add(new FilmStripWidthMenu(dispatcher, model));
            this.MenuItems.Add(new SSFilmStripWidthMenu(dispatcher, model));
//            this.MenuItems.Add(new ToolBarMenu(dispatcher, model));
        }
    }

    //Much of this code is duplicated from SlideToolbarButtons.cs
    public class ToggleSlideZoomMenuItem : MenuItem, DeckTraversalModelAdapter.IAdaptee {
        private readonly EventQueue.PropertyEventDispatcher m_ZoomChangedDispatcher;
        private readonly ControlEventQueue m_EventQueue;
        private readonly PresenterModel m_Model;
        private WorkspaceModelAdapter m_Adapter;
        private bool m_Disposed = false;
        private SlideModel m_Slide;
        private float m_NextZoom;

        public ToggleSlideZoomMenuItem(ControlEventQueue dispatcher, PresenterModel model) {
            this.m_EventQueue = dispatcher;
            this.m_Model = model;
            this.m_ZoomChangedDispatcher = new EventQueue.PropertyEventDispatcher(this.m_EventQueue, new PropertyEventHandler(this.HandleZoomChanged));
            this.m_Adapter = new WorkspaceModelAdapter(dispatcher, this, model);
            this.Text = Strings.ZoomSlide;
        }

        protected override void Dispose(bool disposing) {
            if (this.m_Disposed) return;
            try {
                if (disposing) {
                    this.m_Adapter.Dispose();
                }
            } finally {
                base.Dispose(disposing);
            }
            this.m_Disposed = true;
        }

        public SlideModel Slide {
            set {
                using (Synchronizer.Lock(this)) {
                    if (this.m_Slide != null) {
                        this.m_Slide.Changed["Zoom"].Remove(this.m_ZoomChangedDispatcher.Dispatcher);
                    }

                    this.m_Slide = value;

                    if (this.m_Slide != null) {
                        this.m_Slide.Changed["Zoom"].Add(this.m_ZoomChangedDispatcher.Dispatcher);
                    }

                    this.m_ZoomChangedDispatcher.Dispatcher(this, null);
                }
            }
        }

        private void HandleZoomChanged(object sender, PropertyEventArgs args) {
            // Disable the button if:
            // (1) There is no slide, or
            // (2) The slide is remote (the instructor's Zoom will overwrite the local zoom,
            //     so it is confusing and pointless to be able to zoom locally).
            if (this.m_Slide == null) {
                this.Enabled = this.Checked = false;
            } else {
                using (Synchronizer.Lock(this.m_Slide.SyncRoot)) {
                    this.m_NextZoom = this.m_Slide.Zoom <= ViewerStateModel.ZOOM_FACTOR ? 1f : ViewerStateModel.ZOOM_FACTOR;
                    this.Checked = this.m_NextZoom > ViewerStateModel.ZOOM_FACTOR;
                    this.Enabled = ((this.m_Slide.Disposition & SlideDisposition.Remote) == 0);
                }
            }
        }

        protected override void OnClick(EventArgs e) {
            base.OnClick(e);
            using (Synchronizer.Lock(this.m_Slide.SyncRoot)) {
                this.m_Slide.Zoom = this.m_NextZoom;
            }
        }


        //Unused IAdaptee member
        public System.Drawing.Color DefaultDeckBGColor {
            set { }
        }
        public UW.ClassroomPresenter.Model.Background.BackgroundTemplate DefaultDeckBGTemplate {
            set { }
        }
    }

    public class FullScreenMenuItem : MenuItem {
        private readonly PresenterModel m_Model;

        public FullScreenMenuItem(PresenterModel model) {
            this.m_Model = model;
            this.Text = Strings.FullScreen;
            Misc.MenuShortcutHelper.SetShortcut(this, Keys.Alt | Keys.Enter);
        }

        protected override void OnClick(EventArgs e) {
            base.OnClick(e);
            using (Synchronizer.Lock(this.m_Model.ViewerState.SyncRoot)) {
                this.m_Model.ViewerState.PrimaryMonitorFullScreen ^= true;
            }
        }
    }

    public class FilmStripAlignmentMenu : MenuItem {
        public FilmStripAlignmentMenu(ControlEventQueue dispatcher, PresenterModel model)
            : base(Strings.FilmStripAlignment) {
            this.MenuItems.Add(new DockMenuItem(dispatcher, model, DockStyle.Left, Strings.Left));
            this.MenuItems.Add(new DockMenuItem(dispatcher, model, DockStyle.Right, Strings.Right));
            this.MenuItems.Add(new DockMenuItem(dispatcher, model, DockStyle.Top, Strings.Top));
            this.MenuItems.Add(new DockMenuItem(dispatcher, model, DockStyle.Bottom, Strings.Bottom));
        }

        public class DockMenuItem : MenuItem {
            private readonly DockStyle m_DockStyle;
            private readonly PresenterModel m_Model;
            private readonly EventQueue.PropertyEventDispatcher m_FilmStripAlignmentListener;
            private bool m_Disposed;

            public DockMenuItem(ControlEventQueue dispatcher, PresenterModel model, DockStyle dock, string text)
                : base(text) {
                this.m_Model = model;
                this.m_DockStyle = dock;

                this.m_FilmStripAlignmentListener = new EventQueue.PropertyEventDispatcher(dispatcher,
                    new PropertyEventHandler(this.HandleFilmStripAlignmentChanged));
                this.m_Model.ViewerState.Changed["FilmStripAlignment"].Add(this.m_FilmStripAlignmentListener.Dispatcher);
                this.m_FilmStripAlignmentListener.Dispatcher(this, null);
            }

            protected override void Dispose(bool disposing) {
                if (this.m_Disposed) return;
                this.m_Disposed = true;
                try {
                    if (disposing) {
                        this.m_Model.ViewerState.Changed["FilmStripAlignment"].Remove(this.m_FilmStripAlignmentListener.Dispatcher);
                    }
                } finally {
                    base.Dispose(disposing);
                }
            }

            protected override void OnClick(EventArgs e) {
                base.OnClick(e);
                using (Synchronizer.Lock(this.m_Model.ViewerState.SyncRoot))
                    this.m_Model.ViewerState.FilmStripAlignment = this.m_DockStyle;
            }

            private void HandleFilmStripAlignmentChanged(object sender, PropertyEventArgs e) {
                using (Synchronizer.Lock(this.m_Model.ViewerState.SyncRoot))
                    this.Checked = (this.m_Model.ViewerState.FilmStripAlignment == this.m_DockStyle);
            }
        }
    }


    public class FilmStripWidthMenu : MenuItem
    {
        public FilmStripWidthMenu(ControlEventQueue dispatcher, PresenterModel model)
            : base(Strings.FilmStripWidth)
        {
            this.MenuItems.Add(new WidthMenuItem(dispatcher, model, 1, Strings.OneSlide));
            this.MenuItems.Add(new WidthMenuItem(dispatcher, model, 2, Strings.TwoSlides));
            this.MenuItems.Add(new WidthMenuItem(dispatcher, model, 3, Strings.ThreeSlides));
        }

        public class WidthMenuItem : MenuItem
        {
            private readonly int m_Width;
            private readonly PresenterModel m_Model;
            private readonly EventQueue.PropertyEventDispatcher m_FilmStripWidthListener;
            private bool m_Disposed;

            public WidthMenuItem(ControlEventQueue dispatcher, PresenterModel model, int width, string text)
                : base(text)
            {
                this.m_Model = model;
                this.m_Width = width;

                this.m_FilmStripWidthListener = new EventQueue.PropertyEventDispatcher(dispatcher,
                    new PropertyEventHandler(this.HandleFilmStripWidthChanged));
                this.m_Model.ViewerState.Changed["FilmStripWidth"].Add(this.m_FilmStripWidthListener.Dispatcher);
                this.m_FilmStripWidthListener.Dispatcher(this, null);
            }

            protected override void Dispose(bool disposing)
            {
                if (this.m_Disposed) return;
                this.m_Disposed = true;
                try
                {
                    if (disposing)
                    {
                        this.m_Model.ViewerState.Changed["FilmStripWidth"].Remove(this.m_FilmStripWidthListener.Dispatcher);
                    }
                }
                finally
                {
                    base.Dispose(disposing);
                }
            }

            protected override void OnClick(EventArgs e)
            {
                base.OnClick(e);
                using (Synchronizer.Lock(this.m_Model.ViewerState.SyncRoot))
                    this.m_Model.ViewerState.FilmStripWidth = this.m_Width;
            }

            private void HandleFilmStripWidthChanged(object sender, PropertyEventArgs e)
            {
                using (Synchronizer.Lock(this.m_Model.ViewerState.SyncRoot))
                    this.Checked = (this.m_Model.ViewerState.FilmStripWidth == this.m_Width);
            }
        }
    }

    public class SSFilmStripWidthMenu : MenuItem
    {
        /// <summary>
        /// The event queue
        /// </summary>
        protected readonly ControlEventQueue m_EventQueue;
        /// <summary>
        /// The role change dispatcher
        /// </summary>
        private readonly EventQueue.PropertyEventDispatcher m_RoleChangedDispatcher;

        /// <summary>
        /// The presenter model
        /// </summary>
        private readonly PresenterModel m_Model;

        /// <summary>
        /// True when disposed
        /// </summary>
        private bool m_Disposed;

 

        public SSFilmStripWidthMenu(ControlEventQueue dispatcher, PresenterModel model)
            : base(Strings.SSDeckFilmStripWidth)
        {
            this.m_EventQueue = dispatcher;
            this.m_Model = model;

            //Ensure that this menu item is only shown if our user is an instructor
            this.m_RoleChangedDispatcher = new EventQueue.PropertyEventDispatcher(this.m_EventQueue, new PropertyEventHandler(this.HandleRoleChanged));
            this.m_Model.Participant.Changed["Role"].Add(this.m_RoleChangedDispatcher.Dispatcher);
            this.InitializeRole();

            this.MenuItems.Add(new SSWidthMenuItem(dispatcher, model, 1, Strings.OneSlide));
            this.MenuItems.Add(new SSWidthMenuItem(dispatcher, model, 2, Strings.TwoSlides));
            this.MenuItems.Add(new SSWidthMenuItem(dispatcher, model, 3, Strings.ThreeSlides));
        }

        /// <summary>
        /// Releases all resources
        /// </summary>
        /// <param name="disposing">True if truely disposing</param>
        protected override void Dispose(bool disposing)
        {
            if (this.m_Disposed) return;
            try
            {
                if (disposing)
                {
                    this.m_Model.Participant.Changed["Role"].Remove(this.m_RoleChangedDispatcher.Dispatcher);
                }
            }
            finally
            {
                base.Dispose(disposing);
            }
            this.m_Disposed = true;
        }


        public class SSWidthMenuItem : MenuItem
        {
            private readonly int m_Width;
            private readonly PresenterModel m_Model;
            private readonly EventQueue.PropertyEventDispatcher m_SSFilmStripWidthListener;
            private bool m_Disposed;

            public SSWidthMenuItem(ControlEventQueue dispatcher, PresenterModel model, int width, string text)
                : base(text)
            {
                this.m_Model = model;
                this.m_Width = width;

                this.m_SSFilmStripWidthListener = new EventQueue.PropertyEventDispatcher(dispatcher,
                    new PropertyEventHandler(this.HandleSSFilmStripWidthChanged));
                this.m_Model.ViewerState.Changed["SSFilmStripWidth"].Add(this.m_SSFilmStripWidthListener.Dispatcher);
                this.m_SSFilmStripWidthListener.Dispatcher(this, null);
            }

            protected override void Dispose(bool disposing)
            {
                if (this.m_Disposed) return;
                this.m_Disposed = true;
                try
                {
                    if (disposing)
                    {
                        this.m_Model.ViewerState.Changed["SSFilmStripWidth"].Remove(this.m_SSFilmStripWidthListener.Dispatcher);
                    }
                }
                finally
                {
                    base.Dispose(disposing);
                }
            }

            protected override void OnClick(EventArgs e)
            {
                base.OnClick(e);
                using (Synchronizer.Lock(this.m_Model.ViewerState.SyncRoot))
                    this.m_Model.ViewerState.SSFilmStripWidth = this.m_Width;
            }

            private void HandleSSFilmStripWidthChanged(object sender, PropertyEventArgs e)
            {
                using (Synchronizer.Lock(this.m_Model.ViewerState.SyncRoot))
                    this.Checked = (this.m_Model.ViewerState.SSFilmStripWidth == this.m_Width);
            }

        }


        protected RoleModel Role;

        
        /// <summary>
        /// Handle the role changing
        /// </summary>
        /// <param name="sender">The event sender</param>
        /// <param name="args">The arguments</param>

        private void HandleRoleChanged(object sender, PropertyEventArgs args)
        {
            this.InitializeRole();
        }

        /// <summary>
        /// Sets the <see cref="Role"/> property to the current
        /// <see cref="ParticipantModel.Role">Role</see> of
        /// the <see cref="ParticipantModel"/> of the associated <see cref="PresenterModel"/>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This method should be called from the <see cref="m_EventQueue"/> or
        /// the control's creator thread.
        /// </para>
        /// </remarks>
        protected void InitializeRole()
        {
            using (Synchronizer.Lock(this.m_Model.Participant.SyncRoot))
            {
                this.Role = this.m_Model.Participant.Role;
                this.Visible = (this.m_Model.Participant.Role is InstructorModel);
            }
        }
    }

    public class ToolBarMenu : MenuItem
    {
        /// <summary>
        /// The event queue
        /// </summary>
        protected readonly ControlEventQueue m_EventQueue;

        /// <summary>
        /// The presenter model
        /// </summary>
        private readonly PresenterModel m_Model;

        /// <summary>
        /// True when disposed
        /// </summary>
        private bool m_Disposed;

        public ToolBarMenu(ControlEventQueue dispatcher, PresenterModel model)
            : base(Strings.Toolbar)
        {
            this.m_EventQueue = dispatcher;
            this.m_Model = model;

            this.MenuItems.Add(new ToolBarMenuItem(dispatcher, model, false, Strings.Default));
            this.MenuItems.Add(new ToolBarMenuItem(dispatcher, model, true, Strings.Classmate));
        }

        /// <summary>
        /// Releases all resources
        /// </summary>
        /// <param name="disposing">True if truely disposing</param>
        protected override void Dispose(bool disposing)
        {
            if (this.m_Disposed) return;

            base.Dispose(disposing);

            this.m_Disposed = true;
        }

        public class ToolBarMenuItem : MenuItem
        {
            private readonly bool m_ClassmateMode;
            private readonly PresenterModel m_Model;
            private readonly EventQueue.PropertyEventDispatcher m_ToolBarModeListener;
            private bool m_Disposed;

            public ToolBarMenuItem(ControlEventQueue dispatcher, PresenterModel model, bool mode, string text)
                : base(text)
            {
                this.m_Model = model;
                this.m_ClassmateMode = mode;

                this.m_ToolBarModeListener = new EventQueue.PropertyEventDispatcher(dispatcher,
                    new PropertyEventHandler(this.HandleToolBarModeChanged));
                this.m_Model.ViewerState.Changed["ClassmateMode"].Add(this.m_ToolBarModeListener.Dispatcher);
                this.m_ToolBarModeListener.Dispatcher(this, null);
            }

            protected override void Dispose(bool disposing)
            {
                if (this.m_Disposed) return;
                this.m_Disposed = true;
                try
                {
                    if (disposing)
                    {
                        this.m_Model.ViewerState.Changed["ClassmateMode"].Remove(this.m_ToolBarModeListener.Dispatcher);
                    }
                }
                finally
                {
                    base.Dispose(disposing);
                }
                this.m_Disposed = true;
            }

            protected override void OnClick(EventArgs e)
            {
                base.OnClick(e);
                using (Synchronizer.Lock(this.m_Model.ViewerState.SyncRoot))
                    this.m_Model.ViewerState.ClassmateMode = this.m_ClassmateMode;
            }

            private void HandleToolBarModeChanged(object sender, PropertyEventArgs e)
            {
                using (Synchronizer.Lock(this.m_Model.ViewerState.SyncRoot))
                    this.Checked = (this.m_Model.ViewerState.ClassmateMode == this.m_ClassmateMode);
            }

        }
    }

}
