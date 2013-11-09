// $Id: SecondMonitorForm.cs 1493 2007-11-09 00:04:29Z linnell $

using System;
using System.Windows.Forms;
using System.Threading;

using UW.ClassroomPresenter.Viewer.Slides;
using UW.ClassroomPresenter.Model;

namespace UW.ClassroomPresenter.Viewer.SecondMonitor {

    /// <summary>
    /// Form representing a secondary monitor view of the main slide being displayed
    /// </summary>
    public class SecondMonitorForm : Form {
        /// <summary>
        /// Has the control been disposed
        /// </summary>
        private bool m_Disposed;

        /// <summary>
        /// Local Reference to the Presenter Model
        /// </summary>
        private readonly PresenterModel m_Model;

        /// <summary>
        /// SlideViewer that displays the second monitor view
        /// </summary>
        private readonly SlideViewer m_SecondMonitorSlideViewer;

        /// <summary>
        /// This keeps track of the current slide and updates the view accordingly
        /// </summary>
        private readonly WorkspaceModelAdapter m_WorkspaceModelAdapter;

        /// <summary>
        /// Creates a new instance of a second monitor form
        /// </summary>
        /// <param name="model">
        /// Presenter model to display the second monitor form on
        /// </param>
        public SecondMonitorForm( PresenterModel model ) {
            // Assign the model
            this.m_Model = model;

            // Layout the control
            this.SuspendLayout();

            // Set the parameters of the form
            this.AccessibleDescription = "secondMonitorForm";
            this.AccessibleName = "secondMonitorForm";
            this.BackColor = System.Drawing.Color.White;
            this.Enabled = false;
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
            this.ShowInTaskbar = false;
            this.Visible = false;
            this.DesktopBounds = Screen.PrimaryScreen.Bounds;
            this.Name = "SecondMonitorForm";
            this.Text = "Classroom Presenter 3 Dual-Monitor Display";

            // Create the second monitor view
            this.m_SecondMonitorSlideViewer = new SecondMonitorSlideViewer(this.m_Model);
            this.m_SecondMonitorSlideViewer.Dock = DockStyle.Fill;
            //Set the disposition to always be public
            using(Synchronizer.Lock(this.m_SecondMonitorSlideViewer.SlideDisplay.SyncRoot)) {
                this.m_SecondMonitorSlideViewer.SlideDisplay.SheetDisposition = Model.Presentation.SheetDisposition.SecondMonitor | Model.Presentation.SheetDisposition.All | Model.Presentation.SheetDisposition.Background | Model.Presentation.SheetDisposition.Public;
            }

            // Attach the traversal and rendering of the second monitor to the model
            this.m_WorkspaceModelAdapter = new WorkspaceModelAdapter(this.m_SecondMonitorSlideViewer.SlideDisplay.EventQueue, this.m_SecondMonitorSlideViewer, this.m_Model);

            // Add the control
            this.Controls.Add( this.m_SecondMonitorSlideViewer );

            this.ResumeLayout();

            // Keep track of changes to the number of monitors and whether the second monitor is enabled
            this.m_Model.ViewerState.Changed["NumberOfScreens"].Add(new PropertyEventHandler(this.HandleViewerStateChanged));
            this.m_Model.ViewerState.Changed["SecondMonitorEnabled"].Add(new PropertyEventHandler(this.HandleViewerStateChanged));
            this.HandleViewerStateChanged(this.m_Model.ViewerState, null);
        }

        /// <summary>
        /// Disposes of the resources associated with this control
        /// </summary>
        /// <param name="disposing">
        /// True if we are in the process of disposing of the object
        /// </param>
        protected override void Dispose(bool disposing) {
            if(this.m_Disposed) return;
            try {
                if(disposing) {
                    this.m_Model.ViewerState.Changed["NumberOfScreens"].Remove(new PropertyEventHandler(this.HandleViewerStateChanged));
                    this.m_Model.ViewerState.Changed["SecondMonitorEnabled"].Remove(new PropertyEventHandler(this.HandleViewerStateChanged));
                    this.m_WorkspaceModelAdapter.Dispose();
                    this.m_SecondMonitorSlideViewer.Dispose();
                }
            }
            finally {
                base.Dispose(disposing);
            }
            this.m_Disposed = true;
        }

        /// <summary>
        /// Event handler that is invoked when variables related to the display
        /// of the second monitor are changed
        /// </summary>
        /// <param name="sender">
        /// The control that sent this message
        /// </param>
        /// <param name="args">
        /// Arguments to the event handler
        /// </param>
        private void HandleViewerStateChanged(object sender, PropertyEventArgs args) {
            if(this.InvokeRequired)
                this.BeginInvoke(new MethodInvoker(this.HandleViewerStateChangedHelper));
            else this.HandleViewerStateChangedHelper();
        }

        /// <summary>
        /// Private implementation to handle enabling and disabling of the
        /// secondary monitor view
        /// </summary>
        private void HandleViewerStateChangedHelper() {
            using(Synchronizer.Lock(this.m_Model.ViewerState.SyncRoot)) {
                // Check if we need to make this control visible
                if( this.m_Model.ViewerState.SecondMonitorEnabled &&
                    this.m_Model.ViewerState.NumberOfScreens > 1 ) {
                    foreach( Screen s in Screen.AllScreens ) {
                        if( !s.Primary ) {
                            // Make Visible
                            this.Visible = true;
                            this.Enabled = true;
                            this.Bounds = s.Bounds;
                            return;
                        }
                    }
                }

                // If there is no usable screen or if the second screen is disabled, hide the control.
                // FIXME: Disable the slide viewer (and WorkspaceModelAdapter) so no resources are wasted.
                this.Visible = false;
                this.Enabled = false;
                this.Bounds = new System.Drawing.Rectangle(0,0,1,1);
            }
        }
    }
}
