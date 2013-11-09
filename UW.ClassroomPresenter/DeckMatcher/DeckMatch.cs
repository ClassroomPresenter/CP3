// $Id: DeckMatch.cs 775 2005-09-21 20:42:02Z pediddle $

using System;
using System.Collections;
using System.Diagnostics;
using System.Drawing;
using System.Threading;

using UW.ClassroomPresenter.Model;
using UW.ClassroomPresenter.Model.Presentation;

namespace UW.ClassroomPresenter.DeckMatcher {
    /// <summary>
    /// Class that represents a match between slide decks. The class marshals changes to the SourceDeck
    /// onto the Destination Deck
    /// </summary>
    public class DeckMatch : IDisposable {
        // Private variables
        private bool m_Disposed = false;
        private readonly EventQueue m_Sender;
        private readonly EventQueue.PropertyEventDispatcher m_SlideRemovedDispatcher;
        private readonly EventQueue.PropertyEventDispatcher m_SlideAddedDispatcher;
        private readonly EventQueue.PropertyEventDispatcher m_SlideContentAddedDispatcher;

        private readonly DeckModel m_SrcDeck;
        private readonly DeckModel m_DestDeck;

        // The slides contained in the deck
        private readonly ArrayList m_SlideMatches;

        // The table of contents of the deck
        private readonly TableOfContentsMatch m_TableOfContentsMatch;

        // Accessors for the private variables
        public TableOfContentsMatch TableOfContents {
            get { return this.m_TableOfContentsMatch; }
        }
        public DeckModel SourceDeck {
            get { return this.m_SrcDeck; }
        }
        public DeckModel DestinationDeck {
            get { return this.m_DestDeck; }
        }

        /// <summary>
        /// Creates a matching between the two decks and recursively creates matchings amongst all
        /// child objects
        /// </summary>
        /// <param name="sender">The EventQueue to invoke async things on</param>
        /// <param name="srcDeck">The deck to marshal changes from</param>
        /// <param name="destDeck">The deck to marshal changes to</param>
        public DeckMatch( EventQueue sender, DeckModel srcDeck, DeckModel destDeck ) {
            this.m_Sender = sender;
            this.m_SrcDeck = srcDeck;
            this.m_DestDeck = destDeck;

            this.m_SlideMatches = new ArrayList();
            this.m_TableOfContentsMatch = new TableOfContentsMatch( this.m_Sender, this.m_SrcDeck, this.m_DestDeck );

            this.SetupInitialMappings();

            this.m_SlideRemovedDispatcher = new EventQueue.PropertyEventDispatcher(this.m_Sender, new PropertyEventHandler(this.HandleSlideRemoved));
            this.m_SlideAddedDispatcher = new EventQueue.PropertyEventDispatcher(this.m_Sender, new PropertyEventHandler(this.HandleSlideAdded));
            this.m_SlideContentAddedDispatcher = new EventQueue.PropertyEventDispatcher(this.m_Sender, new PropertyEventHandler(this.HandleSlideContentAdded));

            // Lock the deck so no content can be added between registering the event listeners.
            using(Synchronizer.Lock(this.m_SrcDeck.SyncRoot)) {
                this.m_SrcDeck.SlideRemoved += this.m_SlideRemovedDispatcher.Dispatcher;
                this.m_SrcDeck.SlideAdded += this.m_SlideAddedDispatcher.Dispatcher;
                this.m_SrcDeck.SlideContentAdded += this.m_SlideContentAddedDispatcher.Dispatcher;
            }
        }

        #region IDisposable Members

        /// <summary>
        /// Finalizer for this object
        /// </summary>
        ~DeckMatch() {
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
                this.m_TableOfContentsMatch.Dispose();

                // Unregister event listeners in reverse order than they were added.
                this.m_SrcDeck.SlideContentAdded -= this.m_SlideContentAddedDispatcher.Dispatcher;
                this.m_SrcDeck.SlideAdded -= this.m_SlideAddedDispatcher.Dispatcher;
                this.m_SrcDeck.SlideRemoved -= this.m_SlideRemovedDispatcher.Dispatcher;

                // Do this last, after the SlideAdded and SlideRemove handlers have been unregistered,
                // so that m_SlideMatches cannot change during the iteration.
                foreach( SlideMatch match in this.m_SlideMatches )
                    match.Dispose();
            }
            this.m_Disposed = true;
        }

        #endregion

        /// <summary>
        /// Returns true if two slide models are equal, false otherwise
        /// </summary>
        /// <param name="src">First model</param>
        /// <param name="dst">Second model</param>
        /// <returns>True if two slide models are equal</returns>
        private bool SlidesEqual( SlideModel src, SlideModel dst ) {
            using( Synchronizer.Lock( src.SyncRoot ) ) {
                using( Synchronizer.Lock( dst.SyncRoot ) ) {
                    return (src.Title == dst.Title);
                }
            }
        }

        /// <summary>
        /// Setup the initial mapping between slides in the two decks
        /// </summary>
        private void SetupInitialMappings() {
            using( Synchronizer.Lock( this.m_SrcDeck.SyncRoot ) ) {
                foreach( SlideModel src in this.m_SrcDeck.Slides ) {
                    using( Synchronizer.Lock( this.m_DestDeck.SyncRoot ) ) {
                        bool bAdded = false;
                        foreach( SlideModel dst in this.m_DestDeck.Slides )
                            if( SlidesEqual( dst, src ) ) {
                                // Add the association to the list
                                this.m_SlideMatches.Add( new SlideMatch( this.m_Sender, src, dst ) );
                                bAdded = true;
                                break;
                            }

                        // New slide, perhaps whiteboard, so add one
                        if( !bAdded ) {
                            // CMPRINCE TODO: This need to be done better
                            // Insert the new slide
//                            this.m_DestDeck.InsertSlide(
                            // Add the association to the list
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Event handler that is invoked when a slide is added to the deck
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args_"></param>
        private void HandleSlideAdded(object sender, PropertyEventArgs args_) {
            PropertyCollectionEventArgs args = ((PropertyCollectionEventArgs) args_);
            SlideModel slide = ((SlideModel) args.Added);

            // Get the best associated slide
            bool bAdded = false;
            using( Synchronizer.Lock( this.m_DestDeck.SyncRoot ) ) {
                foreach( SlideModel s in this.m_DestDeck.Slides )
                    if( SlidesEqual( s, slide ) ) {
                        // Add the association to the list
                        this.m_SlideMatches.Add( new SlideMatch( this.m_Sender, slide, s ) );
                        bAdded = true;
                        break;
                    }

                // New slide, perhaps whiteboard, so add one
                if( !bAdded ) {
                    // CMPRINCE TODO: This need to be done better
                    // Insert the new slide
//                    this.m_DestDeck.InsertSlide(
                    // Add the association to the list
                }
            }
        }

        /// <summary>
        /// Event handler that is invoked when a slide is removed from the deck
        /// Never remove a slide, just remove the mappings
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args_"></param>
        private void HandleSlideRemoved(object sender, PropertyEventArgs args_) {
            PropertyCollectionEventArgs args = ((PropertyCollectionEventArgs) args_);
            SlideModel slide = ((SlideModel) args.Removed);

            ArrayList toRemove = new ArrayList();
            foreach( SlideMatch match in this.m_SlideMatches )
                if( match.SourceSlide == slide )
                    toRemove.Add( match );
            foreach( SlideMatch match in toRemove )
                this.m_SlideMatches.Remove( match );
        }

        /// <summary>
        /// Event handler that is invoked when a slide's content has changed
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args_"></param>
        private void HandleSlideContentAdded(object sender, PropertyEventArgs args_) {
            PropertyCollectionEventArgs args = ((PropertyCollectionEventArgs) args_);
            ByteArray hash = ((ByteArray) args.Added);

            ImageHashtable.ImageHashTableItem Content;

            // Get the slide content from the source deck
            using(Synchronizer.Lock(this.SourceDeck.SyncRoot)) {
                Image image = this.SourceDeck.GetSlideContent( hash );

                if( image == null )
                    throw new ArgumentException("The specified ByteArray does not map to slide content in the specified deck.", "hash");

                Content = new ImageHashtable.ImageHashTableItem( hash, image );
            }

            // Add the slide content to the destination deck
            using(Synchronizer.Lock(this.DestinationDeck.SyncRoot)) {
                // Note: If the deck already has content with this hash, nothing will happen.
                this.DestinationDeck.AddSlideContent( Content.key, Content.image );
            }
        }
    }
}
