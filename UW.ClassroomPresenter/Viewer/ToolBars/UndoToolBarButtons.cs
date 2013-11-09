// $Id: UndoToolBarButtons.cs 1766 2008-09-14 16:54:14Z lining $

using System;
using System.Diagnostics;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;

using Microsoft.Ink;

using UW.ClassroomPresenter.Model;
using UW.ClassroomPresenter.Model.Presentation;

using UW.ClassroomPresenter.Viewer.Slides;

namespace UW.ClassroomPresenter.Viewer.ToolBars {
    /// <summary>
    /// Class that places the undo and redo buttons on a ToolStrip
    /// </summary>
    public class UndoToolBarButtons {
        /// <summary>
        /// The presenter model being modified
        /// </summary>
        private readonly PresenterModel m_Model;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="model">The presenter model</param>
        public UndoToolBarButtons(PresenterModel model) {
            this.m_Model = model;
        }

        /// <summary>
        /// Make the correct buttons and add them to the given ToolStrip
        /// </summary>
        /// <param name="parent">The parent ToolStrip</param>
        /// <param name="dispatcher">The event queue</param>
        public void MakeButtons(ToolStrip parent, ControlEventQueue dispatcher) {
            UndoToolBarButton undo = new UndoToolBarButton(dispatcher, this.m_Model);
            RedoToolBarButton redo = new RedoToolBarButton(dispatcher, this.m_Model);
            Bitmap img;

            img = new Bitmap( Image.FromStream( this.GetType().Assembly.GetManifestResourceStream( "UW.ClassroomPresenter.Viewer.ToolBars.Icons.undo.png" ) ) );
            Misc.ImageListHelper.Add( img, parent.ImageList );
            undo.ImageIndex = parent.ImageList.Images.Count - 1;

            img = new Bitmap( Image.FromStream( this.GetType().Assembly.GetManifestResourceStream( "UW.ClassroomPresenter.Viewer.ToolBars.Icons.redo.png" ) ) );
            Misc.ImageListHelper.Add( img, parent.ImageList );
            redo.ImageIndex = parent.ImageList.Images.Count - 1;

            parent.Items.Add(undo);
            parent.Items.Add(redo);
        }

        /// <summary>
        /// Make the correct buttons and add them to the given ToolStrip
        /// </summary>
        /// <param name="mian">The main ToolStrip</param>
        /// <param name="extra">The extra ToolStrip</param>
        /// <param name="dispatcher">The event queue</param>
        public void MakeButtons(ToolStrip main, ToolStrip extra, ControlEventQueue dispatcher) {
            UndoToolBarButton undo = new UndoToolBarButton(dispatcher, this.m_Model);
            RedoToolBarButton redo = new RedoToolBarButton(dispatcher, this.m_Model);
            Bitmap img;

            img = new Bitmap( Image.FromStream( this.GetType().Assembly.GetManifestResourceStream( "UW.ClassroomPresenter.Viewer.ToolBars.Icons.undo.png" ) ) );
            Misc.ImageListHelper.Add( img, main.ImageList );
            undo.ImageIndex = main.ImageList.Images.Count - 1;

            img = new Bitmap( Image.FromStream( this.GetType().Assembly.GetManifestResourceStream( "UW.ClassroomPresenter.Viewer.ToolBars.Icons.redo.png" ) ) );
            Misc.ImageListHelper.Add( img, extra.ImageList );
            redo.ImageIndex = extra.ImageList.Images.Count - 1;

            main.Items.Add(new ToolStripSeparator());
            main.Items.Add(undo);
            extra.Items.Add(new ToolStripSeparator());
            extra.Items.Add(redo);
        }

        #region UndoRedoToolBarButton

        /// <summary>
        /// Class representing the Undo ToolBar Button
        /// </summary>
        private class UndoRedoToolBarButton : ToolStripButton, DeckTraversalModelAdapter.IAdaptee {
            /// <summary>
            /// The event queue
            /// </summary>
            private readonly EventQueue m_EventQueue;
            /// <summary>
            /// Class to keep the workspace in sync
            /// </summary>
            private readonly WorkspaceModelAdapter m_WorkspaceModelAdapter;
            /// <summary>
            /// The presenter model
            /// </summary>
            protected readonly PresenterModel m_Model;
            /// <summary>
            /// Series of actions that occur
            /// </summary>
            protected object[] m_Actors;
            /// <summary>
            /// True when this object is disposed
            /// </summary>
            private bool m_Disposed;

            /// <summary>
            /// Constructor
            /// </summary>
            /// <param name="dispatcher">The event queue</param>
            /// <param name="model">The model</param>
            public UndoRedoToolBarButton(EventQueue dispatcher, PresenterModel model) {
                this.m_EventQueue = dispatcher;
                this.m_Model = model;

                // TODO: Dynamically change the ToolTipText depending on what type of change will be undone.
                this.ToolTipText = Strings.UndoTooltipText;

                this.m_Model.Undo.Update += new EventHandler(this.HandleUndoableChanged);
                this.m_WorkspaceModelAdapter = new WorkspaceModelAdapter(dispatcher, this, this.m_Model);
            }

            /// <summary>
            /// Dispose of all allocated resources
            /// </summary>
            /// <param name="disposing">True if permanently disposing</param>
            protected override void Dispose(bool disposing) {
                if(this.m_Disposed) return;
                try {
                    if(disposing) {
                        this.m_Model.Undo.Update -= new EventHandler(this.HandleUndoableChanged);
                        this.m_WorkspaceModelAdapter.Dispose();
                    }
                } finally {
                    base.Dispose(disposing);
                }
                this.m_Disposed = true;
            }

            /// <summary>
            /// Handles undoing changing
            /// </summary>
            /// <param name="sender">The event sender</param>
            /// <param name="e">The arguments</param>
            private void HandleUndoableChanged(object sender, EventArgs e) {
                this.m_EventQueue.Post(new EventQueue.EventDelegate(this.HandleUndoStackChanged));
            }

            /// <summary>
            /// Handles the undo stack changing
            /// </summary>
            protected virtual void HandleUndoStackChanged() {
                using(Synchronizer.Lock(this.m_Model.Undo.SyncRoot)) {
                    this.Enabled = this.m_Model.Undo.IsUndoable(this.m_Actors);
                }
            }

            /// <summary>
            /// Keeps the slide in sync
            /// </summary>
            SlideModel DeckTraversalModelAdapter.IAdaptee.Slide {
                set {
                    using(Synchronizer.Lock(this.m_Model.Undo.SyncRoot)) {
                        this.m_Actors = new object[] { value };
                        this.HandleUndoableChanged(this, EventArgs.Empty);
                    }
                }
            }

            //Unused IAdaptee member
            System.Drawing.Color DeckTraversalModelAdapter.IAdaptee.DefaultDeckBGColor {
                set {}
            }
            UW.ClassroomPresenter.Model.Background.BackgroundTemplate DeckTraversalModelAdapter.IAdaptee.DefaultDeckBGTemplate  {
                set { }
            }
        }

        #endregion

        #region UndoToolBarButton

        /// <summary>
        /// The Undo Button
        /// </summary>
        private class UndoToolBarButton : UndoRedoToolBarButton {
            /// <summary>
            /// Constructor
            /// </summary>
            /// <param name="dispatcher">The event queue</param>
            /// <param name="model">The model</param>
            public UndoToolBarButton( EventQueue dispatcher, PresenterModel model )
                : base( dispatcher, model ) {
                // TODO: Dynamically change the ToolTipText depending on what type of change will be redone.
                this.ToolTipText = Strings.UndoTooltipText;
            }

            /// <summary>
            /// Handles this button being pressed
            /// </summary>
            /// <param name="args">The event args</param>
            protected override void OnClick( EventArgs args ) {
                using( Synchronizer.Lock( this.m_Model.Undo.SyncRoot ) ) {
                    if( this.m_Model.Undo.IsUndoable( this.m_Actors ) )
                        this.m_Model.Undo.UndoOne( this.m_Actors );
                }
                base.OnClick( args );
            }
        }

        #endregion

        #region RedoToolBarButton

        /// <summary>
        /// The Redo Button
        /// </summary>
        private class RedoToolBarButton : UndoRedoToolBarButton {
            /// <summary>
            /// Constructor
            /// </summary>
            /// <param name="dispatcher">The event queue</param>
            /// <param name="model">The model</param>
            public RedoToolBarButton(EventQueue dispatcher, PresenterModel model) : base(dispatcher, model) {
                // TODO: Dynamically change the ToolTipText depending on what type of change will be redone.
                this.ToolTipText = Strings.RedoTooltipText;
            }

            /// <summary>
            /// Handle this button being pressed
            /// </summary>
            /// <param name="args">The event args</param>
            protected override void OnClick( EventArgs args ) {
                using(Synchronizer.Lock(this.m_Model.Undo.SyncRoot)) {
                    if(this.m_Model.Undo.IsRedoable(this.m_Actors))
                        this.m_Model.Undo.RedoOne(this.m_Actors);
                }
                base.OnClick( args );
            }

            /// <summary>
            /// Set events as being redoable
            /// </summary>
            protected override void HandleUndoStackChanged() {
                using(Synchronizer.Lock(this.m_Model.Undo.SyncRoot)) {
                    this.Enabled = this.m_Model.Undo.IsRedoable(this.m_Actors);
                }
            }
        }

        #endregion
    }
}
