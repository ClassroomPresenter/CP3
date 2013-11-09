// $Id: PresentationLayout.cs 2233 2013-09-27 22:17:28Z fred $

using System;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;

using UW.ClassroomPresenter.Model;
using UW.ClassroomPresenter.Model.Presentation;

using UW.ClassroomPresenter.Viewer.Inking;
using UW.ClassroomPresenter.Viewer.Touch;
using UW.ClassroomPresenter.Viewer.Slides;
using UW.ClassroomPresenter.Viewer.FilmStrips;
using UW.ClassroomPresenter.Viewer.SecondMonitor;

using Microsoft.Ink;
using Microsoft.StylusInput;
using UW.ClassroomPresenter.Model.Network;
using System.Collections.Generic;
using System.Diagnostics;

namespace UW.ClassroomPresenter.Viewer {
    /// <summary>
    /// A control container which contains a <see cref="FilmStrip"/> and <see cref="MainSlideViewer"/>,
    /// each linked to the current <see cref="DeckTraversalModel"/>
    /// of the <see cref="PresenterModel"/>, and allows the user to draw ink on the <see cref="MainSlideViewer"/>.
    /// </summary>
    /// <remarks>
    /// The <see cref="MainSlideViewer"/> always displays the current slide of the current <see cref="DeckTraversalModel"/>.
    /// This is achieved via a <see cref="MainSlideViewer.DeckTraversalModelAdapter"/> which tracks the current slide.
    /// <para>
    /// The <see cref="FilmStrip"/> displays all the slides in the current <see cref="DeckTraversalModel"/>'s deck, and
    /// also lets the user select the current slide directly.  Clicking on a slide in the <see cref="FilmStrip"/>
    /// updates the <see cref="DeckTraversalModel"/>, which causes the <see cref="MainSlideViewer"/> to display the
    /// new slide.
    /// </para>
    /// <para>
    /// Ink is collected via a <see cref="RealTimeStylus"/> instance attached to the <see cref="MainSlideViewer"/>.
    /// The <see cref="RealTimeStylus"/> sends its ink to <see cref="TransformableDynamicRenderer"/> and
    /// <see cref="InkAnnotationCollector"/> instances, which render ink and add collected ink to the <see cref="RealTimeInkSheetModel"/>
    /// of the <see cref="MainSlideViewer"/>'s current slide.
    /// Ink from the <see cref="RealTimeStylus"/> is also transformed via a <see cref="InkTransformFilter"/>
    /// which applies an inverse transform to that of the <see cref="MainSlideViewer.SlideDisplay"/>.
    /// Finally, the <see cref="DrawingAttributes"/> used when rendering ink are obtained from a <see cref="StylusInputSelector"/>,
    /// which sends information via the <see cref="RealTimeStylus"/> whenever the current <see cref="StylusModel"/> changes.
    /// </para>
    /// </remarks>
    public class ViewerPresentationLayout : UserControl {
        private readonly PresenterModel m_Model;

        // Child controls.
        /*private*/public /*readonly*/ DeckStrip m_DeckStrip;
        private readonly Splitter m_DeckStripSplitter;
        private readonly MainSlideViewer m_MainSlideViewer;
        private readonly SecondMonitorForm m_SecondMonitorForm;
        private readonly SlidePreview m_SlidePreview;

        // RealTimeStylus plugins and adapters.
        private readonly RealTimeStylus m_RealTimeStylus;
        private readonly StylusInputSelector m_StylusInputSelector;
        private readonly TouchGestureHandler m_TouchGestureHandler;
        private readonly InkTransformFilter m_InkTransformFilter;
        private readonly TransformableDynamicRenderer m_TransformableDynamicRenderer;
        private readonly InkAnnotationCollector m_InkAnnotationCollector;
        private readonly InkSheetAdapter m_InkAnnotationCollector_InkSheetAdapter;
        private readonly LassoPlugin m_LassoPlugin;
        private readonly InkSheetAdapter m_LassoPlugin_InkSheetAdapter;
        private readonly EraserPlugin m_EraserPlugin;
        private readonly InkSheetAdapter m_EraserPlugin_InkSheetAdapter;

        // Other adapters.
        private readonly WorkspaceModelAdapter m_WorkspaceModelAdapter;
        private readonly PreviewTraversalModelAdapter m_PreviewTraversalModelAdapter;

        private bool m_Disposed;

        private EventQueue m_EventQueue;
        private RoleSynchronizer m_RoleSync;

        
        /// <summary>
        /// Creates a new <see cref="ViewerPresentationLayout"/> instance and instantiates all of the child controls.
        /// </summary>
        /// <param name="model">The model whose <see cref="WorkspaceModel.CurrentDeckTraversal"/> property will
        /// be used to display the current slide and deck.</param>
        public ViewerPresentationLayout(PresenterModel model) {
            this.Name = "ViewerPresentationLayout";
            this.m_Model = model;

            this.m_EventQueue = new ControlEventQueue(this);

            m_Model.Workspace.CurrentDeckTraversal.ListenAndInitialize(m_EventQueue, new Property<DeckTraversalModel>.EventHandler(this.HandleTraversalChanged));

            // Create the film strip, docked to the right side of the container.
            this.m_DeckStrip = new DeckStrip(this.m_Model);
            /// Filmstrip docking
            this.m_DeckStrip.Dock = DockStyle.Right;

            this.m_MainSlideViewer = new MainSlideViewer(this.m_Model, true);

            // Make the deck strip resizable with a LinkedSplitter.
            // The splitter automatically sets its dock to be the same as the FilmStrip and 
            // keeps z-order correct with respect to the FilmStrip and the MainSlideViewer.
            this.m_DeckStripSplitter = new LinkedSplitter(this.m_DeckStrip, this.m_MainSlideViewer);

            // Create the MainSlideViewer, which occupies the remaining space in the container.
            this.m_MainSlideViewer.Dock = DockStyle.Fill;

            #region RealTimeStylus Initialization
            // Create a new RealTimeStylus, which will process ink drawn on the MainSlideViewer.
            this.m_RealTimeStylus = new RealTimeStylus(this.m_MainSlideViewer, true);

            try {
                // Enable touch input for the real time stylus
                this.m_RealTimeStylus.MultiTouchEnabled = true;
                this.m_TouchGestureHandler = new TouchGestureHandler(this.m_Model, this.m_EventQueue);
                this.m_RealTimeStylus.SyncPluginCollection.Add(this.m_TouchGestureHandler);
            }
            catch { }

            // Make sure the TransformableDynamicRenderer and InkAnnotationCollector
            // find out whenever the current StylusModel changes.
            this.m_StylusInputSelector = new StylusInputSelector(this.m_Model, this.m_RealTimeStylus);
            this.m_RealTimeStylus.SyncPluginCollection.Add(this.m_StylusInputSelector);

            // Scale the ink to the inverse of the MainSlideViewer's transform.
            // This keeps the ink's coordinates correct when the MainSlideViewer's transform
            // is applied again later by the TransformableDynamicRenderer and InkAnnotationCollector.
            this.m_InkTransformFilter = new InkTransformFilter(this.m_MainSlideViewer.SlideDisplay);
            this.m_RealTimeStylus.SyncPluginCollection.Add(this.m_InkTransformFilter);

            // Create a *synchronous* TransformableDynamicRenderer, which will render ink received directly
            // from the RealTimeStylus on a high-priority thread.
            this.m_TransformableDynamicRenderer = new TransformableDynamicRendererLinkedToDisplay(this.m_MainSlideViewer, this.m_MainSlideViewer.SlideDisplay);
            this.m_TransformableDynamicRenderer.Enabled = true;
            this.m_RealTimeStylus.SyncPluginCollection.Add(this.m_TransformableDynamicRenderer);

            // Don't dynamically render ink on the main slide viewer twice.  The MainSlideViewer's RealTimeInkSheetRenderer's
            // own TransformableDynamicRenderer would render the ink on a low-priority asynchronous thread, which is
            // fine for secondary slide viewers and the FilmStrip, but not fine for the MainSlideViewer.
            using (Synchronizer.Lock(this.m_MainSlideViewer.SlideDisplay.SyncRoot)) {
                this.m_MainSlideViewer.SlideDisplay.RenderLocalRealTimeInk = false;
            }

            // Create an InkAnnotationCollector and wrap it in an InkSheetAdapter,
            // which sends ink from the RealTimeStylus to the RealTimeInkSheetModel
            // of the MainSlideViewer's current slide.
            this.m_InkAnnotationCollector = new InkAnnotationCollector();
            this.m_InkAnnotationCollector_InkSheetAdapter = new InkSheetAdapter(this.m_MainSlideViewer.SlideDisplay, this.m_InkAnnotationCollector);
            this.m_RealTimeStylus.AsyncPluginCollection.Add(this.m_InkAnnotationCollector);

            this.m_LassoPlugin = new LassoPlugin(this.m_MainSlideViewer, this.m_MainSlideViewer.SlideDisplay);
            this.m_LassoPlugin_InkSheetAdapter = new InkSheetAdapter(this.m_MainSlideViewer.SlideDisplay, this.m_LassoPlugin);
            this.m_RealTimeStylus.SyncPluginCollection.Add(this.m_LassoPlugin);

            this.m_EraserPlugin = new EraserPlugin(this.m_MainSlideViewer.SlideDisplay);
            this.m_EraserPlugin_InkSheetAdapter = new InkSheetAdapter(this.m_MainSlideViewer.SlideDisplay, this.m_EraserPlugin);
            this.m_RealTimeStylus.SyncPluginCollection.Add(this.m_EraserPlugin);

            // Now that all the plugins have been added, enable the RealTimeStylus.
            this.m_RealTimeStylus.Enabled = true;
            #endregion RealTimeStylus Initialization

            // Create a DeckTraversalModelAdapter, which causes the MainSlideViewer to always display the
            // current slide of the current deck of the PresenterModel's current DeckTraversalModel.
            this.m_WorkspaceModelAdapter = new WorkspaceModelAdapter(this.m_MainSlideViewer.SlideDisplay.EventQueue, this.m_MainSlideViewer, this.m_Model);

            // Create the Slide Preview control
            this.m_SlidePreview = new SlidePreview(this.m_Model, this.m_DeckStripSplitter);
            this.m_PreviewTraversalModelAdapter
                = new PreviewTraversalModelAdapter(this.m_SlidePreview.m_EventQueue, this.m_SlidePreview.m_PreviewSlideViewer, this.m_Model);

            // Create the Second Monitor Form, this displays the slide on the secondary display
            this.m_SecondMonitorForm = new SecondMonitorForm(this.m_Model);
            this.ParentChanged += new EventHandler(OnParentChanged);

            // Create the Role Synchronizer for the MainSlideViewer
            this.m_RoleSync = new RoleSynchronizer(this.m_MainSlideViewer, this.m_Model.Participant);

            #region Add Controls
            // Add the SlidePreview Control
            this.Controls.Add(this.m_SlidePreview);

            // Now, add the controls in reverse order of their docking priority.
            // The MainSlideViewer gets added first so its Fill dock-style will be effective.
            this.Controls.Add(this.m_MainSlideViewer);

            // Next, dock the FilmStripSplitter and the FilmStrip in reverse order,
            // so that the FilmStripSplitter will be farther away from the edge of the container.
            this.Controls.Add(this.m_DeckStripSplitter);
            this.Controls.Add(this.m_DeckStrip);


            #endregion Add Controls
        }

        /// <summary>
        /// When the decktraversal changes, we need to continue monitoring its
        /// linked status for the deckstrip adding, and so we add/remove the appropriate 
        /// event listeners
        /// </summary>
        /// <param name="args"></param>
        private void HandleTraversalChanged(Property<DeckTraversalModel>.EventArgs args) {
            //Refresh our event listeners
            if (args != null && args.Old != null) {
                using (Synchronizer.Lock(args.Old.SyncRoot)) {
                    if (args.Old is LinkedDeckTraversalModel) {
                        ((LinkedDeckTraversalModel)(args.Old)).Changed["Mode"].Remove(
                            new PropertyEventHandler(this.HandleLinkedChanged));
                    }
                }
            }
            bool doHandleLinkedChanged = false;
            using (Synchronizer.Lock(this.m_Model.Workspace.CurrentDeckTraversal.SyncRoot)) {
                if ((this.m_Model.Workspace.CurrentDeckTraversal.Value is LinkedDeckTraversalModel) && (this.m_Model.Workspace.CurrentDeckTraversal.Value != null)) {
                    this.m_Model.Workspace.CurrentDeckTraversal.Value.Changed["Mode"].Add(
                        new PropertyEventHandler(this.HandleLinkedChanged));
                    doHandleLinkedChanged = true;
                }
            }

            if (doHandleLinkedChanged)
                HandleLinkedChanged(null, null);
        }

        /// <summary>
        /// when the linked status of a deck changes, we need to add/remove
        /// the deckstrip as appropriate
        /// </summary>
        /// <param name="o"></param>
        /// <param name="args"></param>
        private void HandleLinkedChanged(object o, PropertyEventArgs args) {
            //When called as a result of the Mode property change, we can get the new mode from the PropertyEventArgs.
            //We prefer to use the args in order to avoid lock order issues.
            LinkedDeckTraversalModel.DeckTraversalSelector newMode = LinkedDeckTraversalModel.DeckTraversalSelector.Unknown;
            if (args != null) {
                PropertyChangeEventArgs pcea = args as PropertyChangeEventArgs;
                if (pcea != null) {
                    newMode = (LinkedDeckTraversalModel.DeckTraversalSelector)pcea.NewValue;
                }
            }

            //If called with null args, get the mode from m_Model.
            if (newMode == LinkedDeckTraversalModel.DeckTraversalSelector.Unknown) {
                DeckTraversalModel dtm = null;
                using (Synchronizer.Lock(this.m_Model.Workspace.CurrentDeckTraversal.SyncRoot)) {
                    dtm = this.m_Model.Workspace.CurrentDeckTraversal.Value;
                }
                using (Synchronizer.Lock(dtm.SyncRoot)) {
                    if (dtm is LinkedDeckTraversalModel) {
                        newMode = ((LinkedDeckTraversalModel)dtm).Mode;
                    }
                }
            }

            if (newMode != LinkedDeckTraversalModel.DeckTraversalSelector.Unknown) {
                RoleModel role;
                using (Synchronizer.Lock(m_Model.Participant.SyncRoot)) {
                    role = m_Model.Participant.Role;
                }
                // if the deck is locked and we are a student, remove the deckstrip and
                // deckstripsplitter from the controls.  Note that this function will only ever 
                //be called on LinkedDeckTraversals.
                if (((role is StudentModel) || (role is PublicModel)) &&
                    (newMode == LinkedDeckTraversalModel.DeckTraversalSelector.Linked)) {
                    this.Controls.Remove(this.m_DeckStrip);
                    this.Controls.Remove(this.m_DeckStripSplitter);
                    //this.m_DeckStrip.Visible = false;
                    //this.m_DeckStripSplitter.Visible = false;
                }
                else if (!this.Controls.Contains(m_DeckStripSplitter)) {
                    // otherwise, add the controls, if they aren't already added.
                    this.Controls.Add(this.m_DeckStripSplitter);
                    this.Controls.Add(this.m_DeckStrip);
                    //this.m_DeckStripSplitter.Visible = true;
                    //this.m_DeckStrip.Visible = true;
                }
            }
        }
        

        protected override void Dispose(bool disposing) {
            if(this.m_Disposed) return;
            try {
                if(disposing) {
                    // Dispose of child controls first so they still have access to the window handle.
                    // If we let base.Dispose() do it, then it's too late.
                    this.m_DeckStrip.Dispose();
                    this.m_DeckStripSplitter.Dispose();
                    this.m_MainSlideViewer.Dispose();
                    this.m_SecondMonitorForm.Dispose();

                    this.m_RealTimeStylus.Dispose();
                    this.m_StylusInputSelector.Dispose();
                    this.m_InkTransformFilter.Dispose();
                    this.m_TransformableDynamicRenderer.Dispose();
                    this.m_InkAnnotationCollector.Dispose();
                    this.m_InkAnnotationCollector_InkSheetAdapter.Dispose();
                    this.m_WorkspaceModelAdapter.Dispose();
                    this.m_LassoPlugin.Dispose();
                    this.m_LassoPlugin_InkSheetAdapter.Dispose();
                    this.m_EraserPlugin.Dispose();
                    this.m_EraserPlugin_InkSheetAdapter.Dispose();

                    this.m_RoleSync.Dispose();
                }
            } finally {
                base.Dispose(disposing);
            }
            this.m_Disposed = true;
        }

        /// <summary>
        /// Need to assign the second monitor view as an owned form of the parent,
        /// this hack does it.
        /// </summary>
        private void OnParentChanged(object sender, EventArgs e) {
            if( this.ParentForm != null )
                this.ParentForm.AddOwnedForm( this.m_SecondMonitorForm );
        }

        #region Properties
        public MainSlideViewer MainSlideView {
            get { return this.m_MainSlideViewer; }
        }
        #endregion

    }
}
