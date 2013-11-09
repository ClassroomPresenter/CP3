// $Id: DeckTraversalMatch.cs 975 2006-06-28 01:02:59Z pediddle $

using System;
using System.Collections;
using System.Diagnostics;
using System.Drawing;
using System.Threading;

using UW.ClassroomPresenter.Model;
using UW.ClassroomPresenter.Model.Presentation;

namespace UW.ClassroomPresenter.DeckMatcher {
    /// <summary>
    /// Class representing a matching between two Deck Traversals, recursively maintains
    /// synchronization amongst all the child objects as well. Marshals changes
    /// between the two deck traversals.
    /// </summary>
    public class DeckTraversalMatch : IDisposable {
        // Private variables
        private bool m_Disposed = false;
        private readonly EventQueue m_Sender;
        private readonly DeckTraversalModel m_SourceDeckTraversal;
        private readonly DeckTraversalModel m_DestDeckTraversal;
        private readonly DeckMatch m_DeckMatch;

        private readonly EventQueue.PropertyEventDispatcher m_CurrentChangedDispatcher;

        // Accessors to the private variables
        public DeckTraversalModel SourceDeckTraversal {
            get { return this.m_SourceDeckTraversal; }
        }
        public DeckTraversalModel DestDeckTraversal {
            get { return this.m_DestDeckTraversal; }
        }

        /// <summary>
        /// Are the two traversals the same
        /// </summary>
        /// <returns>Returns true if they are the same, false otherwise</returns>
        public bool IsSameTarget() {
            if( this.m_SourceDeckTraversal == this.m_DestDeckTraversal ||
                this.m_SourceDeckTraversal.Id == this.m_DestDeckTraversal.Id )
                return true;
            else
                return false;
        }

        /// <summary>
        /// Constructs a new matching between the two given DeckTraversalModels
        /// </summary>
        /// <param name="srcTraversal">The DeckTraversal to marshal changes from</param>
        /// <param name="destTraversal">The DeckTraversal to marshal changes to</param>
        public DeckTraversalMatch( EventQueue sender, DeckTraversalModel srcTraversal, DeckTraversalModel destTraversal ) {
            // Set the members
            this.m_Sender = sender;
            this.m_SourceDeckTraversal = srcTraversal;
            this.m_DestDeckTraversal = destTraversal;

            this.m_CurrentChangedDispatcher = new EventQueue.PropertyEventDispatcher(this.m_Sender, new PropertyEventHandler(this.HandleCurrentChanged));

            if( !this.IsSameTarget() ) {
                // Recursively Create all the child matchings
                this.m_DeckMatch = new DeckMatch( this.m_Sender, this.m_SourceDeckTraversal.Deck, this.m_DestDeckTraversal.Deck );

                // Setup the listeners
                this.m_SourceDeckTraversal.Changed["Current"].Add(this.m_CurrentChangedDispatcher.Dispatcher);

                // Update the initial values
                this.m_Sender.Post(delegate() {
                    this.m_CurrentChangedDispatcher.Dispatcher(this, null);
                });
            }
        }

        #region IDisposable Members

        /// <summary>
        /// Finalizer for this object
        /// </summary>
        ~DeckTraversalMatch() {
            this.Dispose(false);
        }

        /// <summary>
        /// Dispose of this object
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
                if( !this.IsSameTarget() ) {
                    this.m_SourceDeckTraversal.Changed["Current"].Remove(this.m_CurrentChangedDispatcher.Dispatcher);
                    this.m_DeckMatch.Dispose();
                }
            }
            this.m_Disposed = true;
        }

        #endregion

        /// <summary>
        /// If the current slide changes in the source deck traversal, change it to the corresponding slide in
        /// the destination entry.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void HandleCurrentChanged(object sender, PropertyEventArgs args) {
            // Get the source table of contents entry
            TableOfContentsModel.Entry entry = null;
            using( Synchronizer.Lock( this.m_SourceDeckTraversal.SyncRoot ) ) {
                entry = this.m_SourceDeckTraversal.Current;
            }

            // Get the corresponding destination table of contents entry
            TableOfContentsModel.Entry destEntry = this.m_DeckMatch.TableOfContents.MarshalSrcEntry( entry );

            // Set the destination table of contents entry
            using( Synchronizer.Lock( this.m_DestDeckTraversal.SyncRoot ) ) {
                this.m_DestDeckTraversal.Current = destEntry;
            }
        }
    }
}
