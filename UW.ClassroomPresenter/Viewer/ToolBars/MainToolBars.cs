using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Forms;
using UW.ClassroomPresenter.Model;

namespace UW.ClassroomPresenter.Viewer.ToolBars
{
    public class MainToolBars
    {
        /// <summary>
        /// The event queue to post messages to
        /// </summary>
        private readonly ControlEventQueue m_EventQueue;
        /// <summary>
        /// The model that this component modifies and interacts with
        /// </summary>
        private readonly PresenterModel m_Model;
        /// <summary>
        /// True when this component has been disposed
        /// </summary>
        private bool m_Disposed;

        public readonly MainToolBar m_MainToolBar;

        public readonly MainToolBar m_MainClassmateToolBar;

        public readonly MainToolBar m_ExtraClassmateToolBar;

        private readonly StylusToolBarButtons m_StylusToolBarButton;

        private readonly SlideToolBarButtons m_SlideToolBarButton;

        private readonly StudentToolBarButtons m_StudentToolBarButton;

        private readonly UndoToolBarButtons m_UndoToolBarButton;

        private readonly InstructorToolBarButtons m_InstructorToolBarButton;

        private readonly DeckNavigationToolBarButtons m_DeckNavigationToolBarButton;

        private readonly EventQueue.PropertyEventDispatcher m_ToolBarModeListener;

        public MainToolBars(PresenterModel model, ControlEventQueue dispatcher) {
            this.m_Model = model;
            this.m_EventQueue = dispatcher;

            this.m_StylusToolBarButton = new StylusToolBarButtons(this.m_Model);
            this.m_SlideToolBarButton = new SlideToolBarButtons(this.m_Model);
            this.m_StudentToolBarButton = new StudentToolBarButtons(this.m_Model);
            this.m_UndoToolBarButton = new UndoToolBarButtons(this.m_Model);
            this.m_InstructorToolBarButton = new InstructorToolBarButtons(this.m_Model);
            this.m_DeckNavigationToolBarButton = new DeckNavigationToolBarButtons(this.m_Model);

            this.m_MainToolBar = new MainToolBar(this.m_Model, this.m_EventQueue);
            this.m_StylusToolBarButton.MakeButtons(this.m_MainToolBar, this.m_EventQueue);
            this.m_MainToolBar.Items.Add(new ToolStripSeparator());
            this.m_MainToolBar.Items.Add(new ToolStripSeparator());
            this.m_SlideToolBarButton.MakeButtons(this.m_MainToolBar, this.m_EventQueue);
            this.m_MainToolBar.Items.Add(new ToolStripSeparator());
            this.m_StudentToolBarButton.MakeButtons(this.m_MainToolBar, this.m_EventQueue);
            this.m_MainToolBar.Items.Add(new ToolStripSeparator());
            this.m_UndoToolBarButton.MakeButtons(this.m_MainToolBar, this.m_EventQueue);
            this.m_MainToolBar.Items.Add(new ToolStripSeparator());
            this.m_MainToolBar.Items.Add(new ToolStripSeparator());
            this.m_InstructorToolBarButton.MakeButtons(this.m_MainToolBar, this.m_EventQueue);
            this.m_MainToolBar.Items.Add(new ToolStripSeparator());
            this.m_DeckNavigationToolBarButton.MakeButtons(this.m_MainToolBar, this.m_EventQueue);

            this.m_MainClassmateToolBar = new MainToolBar(this.m_Model, this.m_EventQueue);
            this.m_ExtraClassmateToolBar = new MainToolBar(this.m_Model, this.m_EventQueue);
            this.m_StylusToolBarButton.MakeButtons(this.m_MainClassmateToolBar, this.m_ExtraClassmateToolBar, this.m_EventQueue);
            this.m_MainClassmateToolBar.Items.Add(new ToolStripSeparator());
            this.m_ExtraClassmateToolBar.Items.Add(new ToolStripSeparator());
            this.m_SlideToolBarButton.MakeButtons(this.m_MainClassmateToolBar, this.m_ExtraClassmateToolBar, this.m_EventQueue);
            this.m_StudentToolBarButton.MakeButtons(this.m_MainClassmateToolBar, this.m_ExtraClassmateToolBar, this.m_EventQueue);
            this.m_UndoToolBarButton.MakeButtons(this.m_MainClassmateToolBar, this.m_ExtraClassmateToolBar, this.m_EventQueue);
            this.m_InstructorToolBarButton.MakeButtons(this.m_MainClassmateToolBar, this.m_ExtraClassmateToolBar, this.m_EventQueue);
            this.m_DeckNavigationToolBarButton.MakeButtons(this.m_MainClassmateToolBar, this.m_ExtraClassmateToolBar, this.m_EventQueue);

            this.m_MainToolBar.Dock = DockStyle.Top;
            this.m_MainClassmateToolBar.Dock = DockStyle.Right;
            this.m_ExtraClassmateToolBar.Dock = DockStyle.Right;

            this.m_Disposed = false;

            this.m_ToolBarModeListener = new EventQueue.PropertyEventDispatcher(this.m_EventQueue, new PropertyEventHandler(this.ToolBarModeChanged));
            this.m_Model.ViewerState.Changed["ClassmateMode"].Add(this.m_ToolBarModeListener.Dispatcher);
            this.m_ToolBarModeListener.Dispatcher(this, null);
        }

        /// <summary>
        /// Disposes of this UI component
        /// </summary>
        /// <param name="disposing">True if we are in the process of disposing all objects</param>
        public void Dispose() {
            if( this.m_Disposed ) return;
            try {
                this.m_MainToolBar.Dispose();
                this.m_MainClassmateToolBar.Dispose();
                this.m_ExtraClassmateToolBar.Dispose();
            } finally {
                this.m_Disposed = true;
            }
        }
        
        private void ToolBarModeChanged(object o, PropertyEventArgs args) {
            using (Synchronizer.Lock(this.m_Model.ViewerState.SyncRoot)) {
                if (this.m_Model.ViewerState.ClassmateMode) {
                    this.m_MainToolBar.Visible = false;
                    this.m_MainClassmateToolBar.Visible = true;
                    this.m_ExtraClassmateToolBar.Visible = true;
                } else {
                    this.m_MainClassmateToolBar.Visible = false;
                    this.m_ExtraClassmateToolBar.Visible = false;
                    this.m_MainToolBar.Visible = true;
                }
            }
        }
    }
}
