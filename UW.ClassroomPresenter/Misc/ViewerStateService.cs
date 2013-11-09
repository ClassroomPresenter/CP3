// $Id: ViewerStateService.cs 877 2006-02-07 23:24:48Z pediddle $

using System;
using System.Threading;
using UW.ClassroomPresenter.Model.Viewer;

namespace UW.ClassroomPresenter.Misc {

    /// <summary>
    /// Summary description for ViewerStateService.
    /// </summary>
    public class ViewerStateService : IDisposable {

        /// <summary>
        /// Private reference to the <see cref="ViewerStateModel"/> to modify
        /// </summary>
        private readonly ViewerStateModel m_ViewerState;

        /// <summary>
        /// We cant just listen to the DisplayPropertiesChanged event to detect when AllScreens changes
        /// because the AllScreens structure doesn't get updated until after the event handler is invoked.
        /// Instead we poll the property to see when it changes.
        /// </summary>
        private readonly System.Windows.Forms.Timer m_ScreenCheckTimer;

        /// <summary>
        /// Constructs the backend for updating information about the machine state of the
        /// application
        /// </summary>
        /// <param name="viewerState">
        /// The <see cref="ViewerStateModel"/> object to be modified
        /// </param>
        public ViewerStateService( ViewerStateModel viewerState ) {
            // We need access to the viewer state
            m_ViewerState = viewerState;

            // Create the timer to listen for changes to the environment
            this.m_ScreenCheckTimer = new System.Windows.Forms.Timer();
            this.m_ScreenCheckTimer.Interval = 500;
            this.m_ScreenCheckTimer.Enabled = true;
            this.m_ScreenCheckTimer.Tick += new EventHandler( this.OnScreenCheckTimerTick );
        }

        ~ViewerStateService() {
            this.Dispose(false);
        }

        public void Dispose() {
            this.Dispose(true);
            System.GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing) {
            if (disposing) {
                this.m_ScreenCheckTimer.Dispose();
            }
        }

        /// <summary>
        /// Event handler for checking whether the number of screens on the machine has changed
        /// </summary>
        /// <param name="sender">
        /// The timer which sent this event
        /// </param>
        /// <param name="e">
        /// The arguments for this event
        /// </param>
        private void OnScreenCheckTimerTick( object sender, EventArgs e ) {
            // Get the current number of screens
            int currentNumberOfScreens = System.Windows.Forms.Screen.AllScreens.Length;

            // Get Access to the Model
            using(Synchronizer.Lock(m_ViewerState.SyncRoot)) {
                // Update the Number of Screens if Needed
                if( m_ViewerState.NumberOfScreens != currentNumberOfScreens ) {
                    m_ViewerState.NumberOfScreens = currentNumberOfScreens;
                }
            }
        }
    }
}
