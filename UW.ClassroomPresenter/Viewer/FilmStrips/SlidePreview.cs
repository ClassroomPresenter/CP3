// $Id: SlidePreview.cs 1544 2008-02-15 19:23:17Z lamphare $

using System;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;

using UW.ClassroomPresenter.Model;
using UW.ClassroomPresenter.Viewer.Slides;

namespace UW.ClassroomPresenter.Viewer.FilmStrips {
    /// <summary>
    /// Class the represents the slide preview control
    /// </summary>
    public class SlidePreview : System.Windows.Forms.UserControl {
        internal readonly ControlEventQueue m_EventQueue;
        private readonly EventQueue.PropertyEventDispatcher m_SlidePreviewChangedDispatcher;
        private readonly EventQueue.PropertyEventDispatcher m_SlidePreviewSizeChangedDispatcher;

 
        private readonly Control m_Linked;

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
        internal readonly SlideViewer m_PreviewSlideViewer;

        /// <summary>
        /// Constructs this control
        /// </summary>
        public SlidePreview(PresenterModel model, Control linked) {
            this.m_Model = model;

            this.m_EventQueue = new ControlEventQueue(this);

            this.m_Linked = linked;
            this.m_Linked.SizeChanged += new EventHandler(this.OnLinkedControlSizeChanged);
            this.m_Linked.LocationChanged += new EventHandler(this.OnLinkedControlSizeChanged);
            this.m_Linked.DockChanged += new EventHandler(this.OnLinkedControlSizeChanged);
            //this.OnLinkedControlSizeChanged(this, EventArgs.Empty);

            // Create the control's properties
            this.SuspendLayout();

            this.Name = "SlidePreview";
            this.Visible = false;
            this.BackColor = System.Drawing.Color.Black;
            this.DockPadding.All = 4;

            this.m_PreviewSlideViewer = new MainSlideViewer(this.m_Model, false);
            this.m_PreviewSlideViewer.Dock = DockStyle.Fill;
            //Set the disposition to always be public
            using (Synchronizer.Lock(this.m_PreviewSlideViewer.SlideDisplay.SyncRoot)) {
                this.m_PreviewSlideViewer.SlideDisplay.SheetDisposition = Model.Presentation.SheetDisposition.SecondMonitor;
            }

            //Listen to changes in the role
            this.m_Model.Participant.Changed["Role"].Add(new PropertyEventHandler(this.onRoleChange));
            //Set the initial role
            this.onRoleChange(this, null);

            this.m_SlidePreviewChangedDispatcher = new EventQueue.PropertyEventDispatcher(this.m_EventQueue, new PropertyEventHandler(this.HandleSlidePreviewChanged));
            this.m_Model.ViewerState.Changed["SlidePreviewEnabled"].Add(this.m_SlidePreviewChangedDispatcher.Dispatcher);
            this.m_Model.ViewerState.Changed["SlidePreviewVisible"].Add(this.m_SlidePreviewChangedDispatcher.Dispatcher);
            this.m_SlidePreviewSizeChangedDispatcher = new EventQueue.PropertyEventDispatcher(this.m_EventQueue, new PropertyEventHandler(this.HandleSlidePreviewSizeChanged));
            this.m_Model.ViewerState.Changed["SlidePreviewWidth"].Add(this.m_SlidePreviewSizeChangedDispatcher.Dispatcher);
            this.m_Model.ViewerState.Changed["SlidePreviewHeight"].Add(this.m_SlidePreviewSizeChangedDispatcher.Dispatcher);

       

            this.Controls.Add(this.m_PreviewSlideViewer);

            // Initialize the SlidePreview's visibility.
            this.m_SlidePreviewChangedDispatcher.Dispatcher(this, null);
            this.m_SlidePreviewSizeChangedDispatcher.Dispatcher(this, null);

            this.ResumeLayout();

            // Create the control immediately, or else event queue will never execute anything (chicken and the egg).
            this.CreateHandle();
        }

        private void onRoleChange(object sender, PropertyEventArgs e) {
            using (Synchronizer.Lock(this.m_Model.Participant.SyncRoot)) {
                using (Synchronizer.Lock(this.m_PreviewSlideViewer.SlideDisplay.SyncRoot)) {
                    this.m_PreviewSlideViewer.SlideDisplay.SheetDisposition = Model.Presentation.SheetDisposition.All | Model.Presentation.SheetDisposition.Background;
                    if (this.m_Model.Participant.Role is Model.Network.InstructorModel) {
                        this.m_PreviewSlideViewer.SlideDisplay.SheetDisposition ^= Model.Presentation.SheetDisposition.Instructor;
                    } else if (this.m_Model.Participant.Role is Model.Network.StudentModel) {
                        this.m_PreviewSlideViewer.SlideDisplay.SheetDisposition ^= Model.Presentation.SheetDisposition.Student;
                    } else if (this.m_Model.Participant.Role is Model.Network.PublicModel) {
                        this.m_PreviewSlideViewer.SlideDisplay.SheetDisposition ^= Model.Presentation.SheetDisposition.Public;
                    }
                }
            }
        }

        //do we have to do this every time?
        public void OnLinkedControlSizeChanged(object sender, EventArgs e) {
            // Determine the best position for the slide preview window
            Point newLocation = new Point(0, 0);
            using (Synchronizer.Lock(this.m_Model.ViewerState.SyncRoot)) {
                switch (this.m_Model.ViewerState.FilmStripAlignment) {
                    case DockStyle.Top:
                        newLocation = new Point(10, this.m_Linked.Location.Y + this.m_Linked.Height);
                        break;
                    case DockStyle.Bottom:
                        newLocation = new Point(10, this.m_Linked.Location.Y - this.Height);
                        break;
                    case DockStyle.Right:
                        newLocation = new Point(this.m_Linked.Location.X - this.Width, 10);
                        break;
                    case DockStyle.Left:
                    default:
                        newLocation = new Point(this.m_Linked.Location.X + this.m_Linked.Width, 10);
                        break;
                }
            }

            // Update the Location of the SlidePreview
            this.Location = newLocation;
        }

        /// <summary>
        /// Dispose of all the event listeners for this control
        /// </summary>
        /// <param name="disposing">true if we are in the process of disposing the object</param>
        protected override void Dispose(bool disposing) {
            if(this.m_Disposed) return;
            try {
                if(disposing) {
                    this.m_Model.ViewerState.Changed["SlidePreviewEnabled"].Remove(this.m_SlidePreviewChangedDispatcher.Dispatcher);
                    this.m_Model.ViewerState.Changed["SlidePreviewVisible"].Remove(this.m_SlidePreviewChangedDispatcher.Dispatcher);
                    this.m_Model.ViewerState.Changed["SlidePreviewWidth"].Remove(this.m_SlidePreviewSizeChangedDispatcher.Dispatcher);
                    this.m_Model.ViewerState.Changed["SlidePreviewHeight"].Remove(this.m_SlidePreviewSizeChangedDispatcher.Dispatcher);
                    this.m_Model.Participant.Changed["Role"].Remove(new PropertyEventHandler(this.onRoleChange));
                    this.m_Linked.SizeChanged -= new EventHandler(this.OnLinkedControlSizeChanged);
                    this.m_Linked.LocationChanged -= new EventHandler(this.OnLinkedControlSizeChanged);
                    this.m_Linked.DockChanged -= new EventHandler(this.OnLinkedControlSizeChanged);


                    this.m_EventQueue.Dispose();
                }
            } finally {
                base.Dispose(disposing);
            }
            this.m_Disposed = true;
        }

        /// <summary>
        /// Event handler for checking the properties the control if we are visible or not
        /// </summary>
        /// <param name="sender">the object invoking us</param>
        /// <param name="args">the arguments to our event</param>
        private void HandleSlidePreviewChanged(object sender, PropertyEventArgs args) {
            // Check if we need to make this control visible or not
            using (Synchronizer.Lock(this.m_Model.ViewerState.SyncRoot)) {
                this.Visible = (this.m_Model.ViewerState.SlidePreviewEnabled && this.m_Model.ViewerState.SlidePreviewVisible);
            }
        }
        /// <summary>
        /// Event handler for checking the properties the control if the size of preview window changed or not
        /// </summary>
        /// <param name="sender">the object invoking us</param>
        /// <param name="args">the arguments to our event</param>
        private void HandleSlidePreviewSizeChanged(object sender, PropertyEventArgs args)
        {
            Size newsize = new Size(400, 300);
            using (Synchronizer.Lock(this.m_Model.ViewerState.SyncRoot))
            {
                newsize = new Size(this.m_Model.ViewerState.SlidePreviewWidth, this.m_Model.ViewerState.SlidePreviewHeight);
            }
            this.Size = newsize;
            this.OnLinkedControlSizeChanged(sender, args);
        }
    }
}
