// $Id: StylusToolBarButton.cs 957 2006-05-30 22:45:49Z cmprince $

using System;
using System.Threading;
using System.Windows.Forms;

using UW.ClassroomPresenter.Model;
using UW.ClassroomPresenter.Model.Stylus;

namespace UW.ClassroomPresenter.Viewer.ToolBars {
    /// <summary>
    /// Button class handling the base class for the stylus buttons
    /// </summary>
    internal class StylusToolBarButton : ToolStripButton {
        /// <summary>
        /// The event queue
        /// </summary>
        private readonly ControlEventQueue m_EventQueue;
        /// <summary>
        /// Property change dispatcher
        /// </summary>
        private readonly EventQueue.PropertyEventDispatcher m_StylusChangedDispatcher;

        /// <summary>
        /// The stylus model to modify
        /// </summary>
        private readonly StylusModel m_Stylus;
        /// <summary>
        /// The presenter model to modify
        /// </summary>
        private readonly PresenterModel m_Model;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="dispatcher">The event queue</param>
        /// <param name="stylus">The stylus model</param>
        /// <param name="model">The presenter model</param>
        public StylusToolBarButton(ControlEventQueue dispatcher, StylusModel stylus, PresenterModel model) {
            this.m_EventQueue = dispatcher;
            this.m_Stylus = stylus;
            this.m_Model = model;

            this.m_StylusChangedDispatcher = new EventQueue.PropertyEventDispatcher(this.m_EventQueue, new PropertyEventHandler(this.HandleStylusChanged));
            this.m_Model.Changed["Stylus"].Add(this.m_StylusChangedDispatcher.Dispatcher);

            // Initialize the Pushed state.
            this.m_StylusChangedDispatcher.Dispatcher(null, null);
        }

        /// <summary>
        /// Dispose of any resources or event listeners
        /// </summary>
        /// <param name="disposing">True if automatically disposing</param>
        protected override void Dispose(bool disposing) {
            try {
                if(disposing) {
                    this.m_Model.Changed["Stylus"].Remove(this.m_StylusChangedDispatcher.Dispatcher);
                }
            } finally {
                base.Dispose(disposing);
            }
        }

        /// <summary>
        /// Handle this button being clicked
        /// </summary>
        /// <param name="args">The arguments</param>
        protected override void OnClick( EventArgs e ) {
            using( Synchronizer.Lock( this.m_Model.SyncRoot ) ) {
                this.m_Model.Stylus = this.m_Stylus;
            }
            base.OnClick( e );
        }

        /// <summary>
        /// Handles the stylus being changed
        /// </summary>
        /// <param name="sender">The event sender</param>
        /// <param name="args">The arguments</param>
        private void HandleStylusChanged(object sender, PropertyEventArgs args) {
            using(Synchronizer.Lock(this.m_Model.SyncRoot)) {
                this.Checked = (this.m_Stylus == this.m_Model.Stylus);
            }
        }
    }
}
