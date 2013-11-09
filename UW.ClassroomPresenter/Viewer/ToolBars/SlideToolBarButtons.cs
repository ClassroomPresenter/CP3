// $Id: SlideToolBarButtons.cs 1766 2008-09-14 16:54:14Z lining $

using System;
using System.Diagnostics;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Ink;

using UW.ClassroomPresenter.Model;
using UW.ClassroomPresenter.Model.Presentation;
using UW.ClassroomPresenter.Model.Network;
using UW.ClassroomPresenter.Model.Viewer;
using UW.ClassroomPresenter.Viewer.Slides;

namespace UW.ClassroomPresenter.Viewer.ToolBars {
    /// <summary>
    /// Handles the creation and management of the buttons control slide 
    /// clearing and minimizing/maximizing
    /// </summary>
    public class SlideToolBarButtons {
        /// <summary>
        /// The model that this class modifies
        /// </summary>
        private readonly PresenterModel m_Model;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="model">The model</param>
        public SlideToolBarButtons(PresenterModel model) {
            this.m_Model = model;
        }

        /// <summary>
        /// Makes all the button associated with changing slide properties
        /// </summary>
        /// <param name="parent">The parent ToolStrip</param>
        /// <param name="dispatcher">The event queue</param>
        public void MakeButtons(ToolStrip parent, ControlEventQueue dispatcher) {
            SlideToolBarButton clear, zoom;

            // Add the clear button
            clear = new ClearInkSheetToolBarButton( dispatcher, this.m_Model );
            clear.Image = UW.ClassroomPresenter.Properties.Resources.slideerase;

            // Add the zoom button
            zoom = new ZoomToolBarButton( dispatcher, this.m_Model );
            zoom.Image = UW.ClassroomPresenter.Properties.Resources.minimize;

            // Add the buttons to the parent ToolStrip
            parent.Items.Add(clear);
            parent.Items.Add(new ToolStripSeparator());
            parent.Items.Add(zoom);
        }

        /// <summary>
        /// Makes all the button associated with changing slide properties
        /// </summary>
        /// <param name="main">The main ToolStrip</param>
        /// <param name="extra">The extra ToolStrip</param>
        /// <param name="dispatcher">The event queue</param>
        public void MakeButtons(ToolStrip main, ToolStrip extra, ControlEventQueue dispatcher) {
            SlideToolBarButton clear, zoom;

            // Add the clear button
            clear = new ClearInkSheetToolBarButton( dispatcher, this.m_Model );
            clear.Image = UW.ClassroomPresenter.Properties.Resources.slideerase;

            // Add the zoom button
            zoom = new ZoomToolBarButton( dispatcher, this.m_Model );
            zoom.Image = UW.ClassroomPresenter.Properties.Resources.minimize;
            zoom.AutoSize = false;
            zoom.Width = 54;
            zoom.Height = 44;

            // Add the buttons to the parent ToolStrip
            main.Items.Add(clear);
            extra.Items.Add(zoom);
        }

        #region SlideToolBarButton

        /// <summary>
        /// A utility class which keeps its abstract <see cref="Slide"/> property in
        /// sync with the current <see cref="SlideModel"/> of the current <see cref="TableOfContentsModel.Entry"/>
        /// of the current <see cref="DeckTraversalModel"/> of the <see cref="WorkspaceModel"/>. <!-- Phew! -->
        /// </summary>
        private abstract class SlideToolBarButton : ToolStripButton, DeckTraversalModelAdapter.IAdaptee {
            /// <summary>
            /// The event queue
            /// </summary>
            private readonly ControlEventQueue m_EventQueue;
            /// <summary>
            /// The model that this class interacts with
            /// </summary>
            protected readonly PresenterModel m_Model;
            /// <summary>
            /// The adapter to keep the workspace in sync
            /// </summary>
            private WorkspaceModelAdapter m_Adapter;
            /// <summary>
            /// Flag set to true if this object is disposed
            /// </summary>
            private bool m_Disposed;

            /// <summary>
            /// Constructor
            /// </summary>
            /// <param name="dispatcher">The event queue</param>
            /// <param name="model">The model</param>
            protected SlideToolBarButton(ControlEventQueue dispatcher, PresenterModel model) {
                this.m_EventQueue = dispatcher;
                this.m_Model = model;

                this.m_Adapter = new WorkspaceModelAdapter(this.m_EventQueue, this, this.m_Model);
            }

            /// <summary>
            /// Dispose of the resources used by this element
            /// </summary>
            /// <param name="disposing">True if we are permanently disposing</param>
            protected override void Dispose(bool disposing) {
                if(this.m_Disposed) return;
                try {
                    if(disposing) {
                        this.m_Adapter.Dispose();
                    }
                } finally {
                    base.Dispose(disposing);
                }
                this.m_Disposed = true;
            }

            /// <summary>
            /// Abstract setter for setting the slide
            /// </summary>
            public abstract SlideModel Slide { set; }

            //Unused IAdaptee member
            public System.Drawing.Color DefaultDeckBGColor {
                set {}
            }
            UW.ClassroomPresenter.Model.Background.BackgroundTemplate DeckTraversalModelAdapter.IAdaptee.DefaultDeckBGTemplate {
                set { }
            }
        }

        #endregion

        #region ClearInkSheetToolBarButton

        /// <summary>
        /// Button representing the ability to clear a slide of all user ink
        /// </summary>
        private class ClearInkSheetToolBarButton : SlideToolBarButton {
            /// <summary>
            /// The slide this class operates on
            /// </summary>
            private SlideModel m_Slide;
            /// <summary>
            /// True when this object is disposed
            /// </summary>
            private bool m_Disposed;

            /// <summary>
            /// Constructor
            /// </summary>
            /// <param name="dispatcher">The event queue</param>
            /// <param name="model">The model</param>
            public ClearInkSheetToolBarButton(ControlEventQueue dispatcher, PresenterModel model) : base(dispatcher, model) {
                this.ToolTipText = Strings.EraseAllToolTip;
            }

            /// <summary>
            /// Dispose of this object
            /// </summary>
            /// <param name="disposing"></param>
            protected override void Dispose(bool disposing) {
                if(this.m_Disposed) return;
                try {
                    if(disposing) {
                    }
                } finally {
                    base.Dispose(disposing);
                }
                this.m_Disposed = true;
            }

            /// <summary>
            /// Handle the slide being changed
            /// </summary>
            public override SlideModel Slide {
                set {
                    using(Synchronizer.Lock(this)) {
                        // TODO: Register event listeners to disable the button whenever the slide does not have an ink sheet or any ink.
                        this.m_Slide = value;
                    }
                }
            }

            /// <summary>
            /// Handle the button being clicked
            /// </summary>
            /// <param name="args">The event args</param>
            protected override void OnClick( EventArgs args ) {
                if( this.m_Slide == null ) return;

                using( Synchronizer.Lock( this.m_Slide.SyncRoot ) ) {
                    
                    InkSheetModel inks = null;

                    // TODO: This code is duplicated in InkSheetAdapter.  Extract to a "ActiveInkAnnotationSheet" property of the SlideModel.
                    // Find the *top-most* *local* InkSheetModel in the annotation layer.
                    for (int i = m_Slide.AnnotationSheets.Count - 1; i >= 0; i--) {
                        SheetModel sheet = m_Slide.AnnotationSheets[i];
                        if ((sheet.Disposition & SheetDisposition.Remote) != 0)
                            continue;

                        if (sheet is InkSheetModel) {
                            inks = ((InkSheetModel)sheet);
                        }
                        //else if (sheet is EditableSheetModel ) {
                        //    ///only erase the sheet model if the user made it
                        //    ///(i.e. it is editable. If this is sent from another 
                        //    ///person, don't erase it).
                        //    EditableSheetModel edit_sheet = (EditableSheetModel)sheet;
                        //    if (edit_sheet.IsEditable) {
                        //        m_Slide.AnnotationSheets.Remove(sheet);
                        //    }
                        //}
                    }

                    if (inks != null) {
                        using (Synchronizer.Lock(inks.SyncRoot)) {
                            Strokes strokes = inks.Ink.Strokes;
                            using (Synchronizer.Lock(strokes.SyncRoot)) {
                                int[] ids = new int[inks.Ink.Strokes.Count];
                                for (int j = 0; j < strokes.Count; j++)
                                    ids[j] = strokes[j].Id;

                                StrokesEventArgs sea = new StrokesEventArgs(ids);
                                inks.OnInkDeleting(sea);
                                inks.Ink.DeleteStrokes();
                                inks.OnInkDeleted(sea);
                            }
                        }
                    
                    }
                }

                base.OnClick( args );
            }
        }

        #endregion

        #region ZoomToolBarButton

        /// <summary>
        /// Button allowing the user to change the zoom of a slide
        /// </summary>
        private class ZoomToolBarButton : SlideToolBarButton {
            /// <summary>
            /// Event listener for zoom changing
            /// </summary>
            private readonly EventQueue.PropertyEventDispatcher m_ZoomChangedDispatcher;
            /// <summary>
            /// The role change dispatcher
            /// </summary>
            private readonly EventQueue.PropertyEventDispatcher m_RoleChangedDispatcher;
            /// <summary>
            /// The slide to modify
            /// </summary>
            private SlideModel m_Slide;
            /// <summary>
            /// The next zoom level
            /// </summary>
            private float m_NextZoom;
            /// <summary>
            /// True when disposed
            /// </summary>
            private bool m_Disposed;

            /// <summary>
            /// Constructor
            /// </summary>
            /// <param name="dispatcher">The event queue</param>
            /// <param name="model">The model</param>
            public ZoomToolBarButton(ControlEventQueue dispatcher, PresenterModel model) : base(dispatcher, model) {
                this.m_ZoomChangedDispatcher = new EventQueue.PropertyEventDispatcher(dispatcher, new PropertyEventHandler(this.HandleZoomChanged));
                this.m_RoleChangedDispatcher = new EventQueue.PropertyEventDispatcher( dispatcher, new PropertyEventHandler( this.HandleRoleChanged ) );
                this.m_Model.Participant.Changed["Role"].Add( this.m_RoleChangedDispatcher.Dispatcher );
            }

            /// <summary>
            /// Dispose of any used resources
            /// </summary>
            /// <param name="disposing">True if automatically disposing</param>
            protected override void Dispose(bool disposing) {
                if(this.m_Disposed) return;
                try {
                    if(disposing) {
                        this.m_Model.Participant.Changed["Role"].Remove( this.m_RoleChangedDispatcher.Dispatcher );
                    }
                } finally {
                    base.Dispose(disposing);
                }
                this.m_Disposed = true;
            }

            /// <summary>
            /// Updates the current slide
            /// </summary>
            public override SlideModel Slide {
                set {
                    using(Synchronizer.Lock(this)) {
                        if(this.m_Slide != null) {
                            this.m_Slide.Changed["Zoom"].Remove(this.m_ZoomChangedDispatcher.Dispatcher);
                        }

                        this.m_Slide = value;

                        if(this.m_Slide != null) {
                            this.m_Slide.Changed["Zoom"].Add(this.m_ZoomChangedDispatcher.Dispatcher);
                        }

                        this.m_ZoomChangedDispatcher.Dispatcher(this, null);
                    }
                }
            }

            /// <summary>
            /// Handle the zoom property being changed
            /// </summary>
            /// <param name="sender">The event sender</param>
            /// <param name="args">The arguments</param>
            private void HandleZoomChanged(object sender, PropertyEventArgs args) {
                // Disable the button if:
                // (1) There is no slide, or
                // (2) The slide is remote (the instructor's Zoom will overwrite the local zoom,
                //     so it is confusing and pointless to be able to zoom locally).
                if(this.m_Slide == null) {
                    this.Enabled = this.Checked = false;
                } else {
                    using(Synchronizer.Lock(this.m_Slide.SyncRoot)) {
                        this.m_NextZoom = this.m_Slide.Zoom <= ViewerStateModel.ZOOM_FACTOR ? 1f : ViewerStateModel.ZOOM_FACTOR;
                        this.Checked = this.m_NextZoom > ViewerStateModel.ZOOM_FACTOR;
                        this.Enabled = ((this.m_Slide.Disposition & SlideDisposition.Remote) == 0)
                            || ((this.m_Slide.Disposition & SlideDisposition.StudentSubmission) != 0);
                    }
                }

                this.ToolTipText = (this.Checked)
                    ? Strings.RestoreSlideSize
                    : Strings.MinimizeCurrentSlide;
            }

            /// <summary>
            /// Handle the role changing
            /// </summary>
            /// <param name="sender">The event sender</param>
            /// <param name="args">The arguments</param>
            private void HandleRoleChanged( object sender, PropertyEventArgs args ) {
                this.InitializeRole();
            }

            /// <summary>
            /// Sets the <see cref="Role"/> property to the current
            /// <see cref="ParticipantModel.Role">Role</see> of
            /// the <see cref="ParticipantModel"/> of the associated <see cref="PresenterModel"/>.
            /// </summary>
            /// <remarks>
            /// This method should be called once from a subclass's constructor,
            /// and may be called any number of times thereafter.
            /// <para>
            /// This method should be called from the <see cref="m_EventQueue"/> or
            /// the control's creator thread.
            /// </para>
            /// </remarks>
            protected void InitializeRole() {
                using( Synchronizer.Lock( this.m_Model.Participant.SyncRoot ) ) {
                    this.Visible = (this.m_Model.Participant.Role is InstructorModel);
                }
            }

            /// <summary>
            /// Handle the button being clicked
            /// </summary>
            /// <param name="args">The event args</param>
            protected override void OnClick( EventArgs args ) {
                if(!(this.Enabled)) return;

                using(Synchronizer.Lock(this.m_Slide.SyncRoot)) {
                    this.m_Slide.Zoom = this.m_NextZoom;
                }
            }
        }

        #endregion
    }
}
