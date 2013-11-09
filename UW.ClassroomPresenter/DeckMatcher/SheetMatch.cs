// $Id: SheetMatch.cs 1611 2008-05-29 02:03:36Z cmprince $

using System;
using System.Collections;
using System.Diagnostics;
using System.Drawing;
using System.Threading;

using UW.ClassroomPresenter.Model;
using UW.ClassroomPresenter.Model.Presentation;

namespace UW.ClassroomPresenter.DeckMatcher {
    /// <summary>
    /// Summary description for SheetMatch.
    /// </summary>
    public abstract class SheetMatch : IDisposable {
        // Private variables
        private bool m_Disposed;
        protected readonly EventQueue m_Sender;

        private readonly EventQueue.PropertyEventDispatcher m_BoundsChangedDispatcher;

        private readonly SheetModel m_SourceSheet;
        private readonly SheetModel m_DestSheet;
//        private readonly SheetMessage.SheetCollection m_Selector;

        // Accessors for the private variables
        public SheetModel SourceSheet {
            get { return this.m_SourceSheet; }
        }
        public SheetModel DestinationSheet {
            get { return this.m_DestSheet; }
        }

        /// <summary>
        /// Construct the sheet model
        /// </summary>
        /// <param name="sender">The event queue for async event handling</param>
        /// <param name="srcSheet">The source sheet model</param>
        /// <param name="dstSheet">The destination sheet model</param>
        /// <param name="selector">Unknown</param>
        public SheetMatch( EventQueue sender, SheetModel srcSheet, SheetModel dstSheet/*, SheetMessage.SheetCollection selector*/ ) {
            this.m_Sender = sender;
            this.m_SourceSheet = srcSheet;
            this.m_DestSheet = dstSheet;

            this.m_BoundsChangedDispatcher = new EventQueue.PropertyEventDispatcher(this.m_Sender, new PropertyEventHandler(this.HandleBoundsChanged));
            this.m_SourceSheet.Changed["Bounds"].Add(this.m_BoundsChangedDispatcher.Dispatcher);
        }

        #region IDisposable Members

        /// <summary>
        /// Finalize this object
        /// </summary>
        ~SheetMatch() {
            this.Dispose(false);
        }

        /// <summary>
        /// Dispose this object
        /// </summary>
        public void Dispose() {
            this.Dispose(true);
            System.GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Dispose of all resources used by this class
        /// </summary>
        /// <param name="disposing">True if we are in the process of disposing, false otherwise</param>
        protected virtual void Dispose(bool disposing) {
            if(this.m_Disposed) return;
            if(disposing) {
                this.m_SourceSheet.Changed["Bounds"].Remove(this.m_BoundsChangedDispatcher.Dispatcher);
            }
            this.m_Disposed = true;
        }

        #endregion IDisposable Members

        protected virtual void HandleBoundsChanged(object sender, PropertyEventArgs args) {
            using( Synchronizer.Lock( this.m_SourceSheet.SyncRoot ) ) {
                using( Synchronizer.Lock( this.m_DestSheet.SyncRoot ) ) {
                    this.m_DestSheet.Bounds = new Rectangle( this.m_SourceSheet.Bounds.Location, this.m_SourceSheet.Bounds.Size );
                }
            }
        }

        public static SheetMatch ForSheet(EventQueue sender, SheetModel src, SheetModel dst ) {
            // FIXME: Create SheetNetworkService classes for sheets other than InkSheetModels.
            if(src is RealTimeInkSheetModel) {
                return new RealTimeInkSheetMatch(sender, (RealTimeInkSheetModel)src, (RealTimeInkSheetModel)dst);
            }
            else if(src is InkSheetModel) {
                return new InkSheetMatch(sender, (InkSheetModel)src, (InkSheetModel)dst);
            }
            else if(src is ImageSheetModel) {
                return null;
            }
            else if(src is TextSheetModel) {
                return null;
            } else if( src is QuickPollSheetModel ) {
                return null;
            } else {
                return null;
            }
        }
    }
}
