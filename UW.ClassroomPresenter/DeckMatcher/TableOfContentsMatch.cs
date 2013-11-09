// $Id: TableOfContentsMatch.cs 775 2005-09-21 20:42:02Z pediddle $

using System;
using System.Collections;
using System.Diagnostics;
using System.Drawing;
using System.Threading;

using UW.ClassroomPresenter.Model;
using UW.ClassroomPresenter.Model.Presentation;

namespace UW.ClassroomPresenter.DeckMatcher {
    /// <summary>
    /// Class that represents a match between tables of contents. The class marshals changes
    /// to the source table of contents onto the Destination tables of contents
    /// </summary>
    public class TableOfContentsMatch : IDisposable {
        private bool m_Disposed;
        private readonly EventQueue m_Sender;
        private readonly DeckModel m_SrcDeck;
        private readonly DeckModel m_DestDeck;
        private readonly EntriesCollectionHelper m_SourceEntriesCollectionHelper;

        private ArrayList m_TableOfContentsEntryMatches = new ArrayList();

        /// <summary>
        /// Constructs a TableOfContentMatch to marshal changes from one table of
        /// contents to the other
        /// </summary>
        /// <param name="sender">The event quene for async calls</param>
        /// <param name="srcDeck">The source deck</param>
        /// <param name="destDeck">The dest deck</param>
        public TableOfContentsMatch( EventQueue sender, DeckModel srcDeck, DeckModel destDeck ) {
            this.m_Sender = sender;
            this.m_SrcDeck = srcDeck;
            this.m_DestDeck = destDeck;

            this.m_SourceEntriesCollectionHelper = new EntriesCollectionHelper( this );
        }

        #region IDisposable Members

        /// <summary>
        /// Finalize this object
        /// </summary>
        ~TableOfContentsMatch() {
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
        protected virtual void Dispose( bool disposing ) {
            if( this.m_Disposed ) return;
            if( disposing ) {
                this.m_SourceEntriesCollectionHelper.Dispose();
            }
            this.m_Disposed = true;
        }

        #endregion

        /// <summary>
        /// Given a table of contents entry in the source table of contents, return the corresponding
        /// match in the destination table of contents
        /// </summary>
        /// <param name="entry">The table of contents entry from the source table of contents</param>
        /// <returns>The corresponding match in the destination table of contents</returns>
        public TableOfContentsModel.Entry MarshalSrcEntry( TableOfContentsModel.Entry entry ) {
            foreach( TableOfContentsEntryMatch match in this.m_TableOfContentsEntryMatches )
                if( entry.Id == match.SourceEntry.Id )
                    return match.DestEntry;
            return null;
        }
        public TableOfContentsModel.Entry MarshalDestEntry( TableOfContentsModel.Entry entry ) {
            foreach( TableOfContentsEntryMatch match in this.m_TableOfContentsEntryMatches )
                if( entry.Id == match.DestEntry.Id )
                    return match.SourceEntry;
            return null;
        }

        /// <summary>
        /// Helper class that lets us keep track of changes to the table of contents of the source deck
        /// </summary>
        private class EntriesCollectionHelper : PropertyCollectionHelper {
            private readonly TableOfContentsMatch m_Owner;

            /// <summary>
            /// Construct the helper
            /// </summary>
            /// <param name="owner">Owner class</param>
            public EntriesCollectionHelper(TableOfContentsMatch owner) : base(owner.m_Sender, owner.m_SrcDeck.TableOfContents, "Entries") {
                this.m_Owner = owner;
                base.Initialize();
            }

            /// <summary>
            /// Gets invoked when an entry is added to the table of contents
            /// </summary>
            /// <param name="index">The index of the entry</param>
            /// <param name="member">The object that was added</param>
            /// <returns>An object</returns>
            protected override object SetUpMember(int index, object member) {
                TableOfContentsModel.Entry entry = ((TableOfContentsModel.Entry) member);

                // Find paired entries by their path from the root
                using( Synchronizer.Lock( this.m_Owner.m_DestDeck.TableOfContents.SyncRoot ) ) {
                    foreach( TableOfContentsModel.Entry e in this.m_Owner.m_DestDeck.TableOfContents.Entries ) {
                        using( Synchronizer.Lock( e.SyncRoot ) ) {
                            using( Synchronizer.Lock( entry.SyncRoot ) ) {
                                int[] srcPath = entry.PathFromRoot;
                                int[] destPath = e.PathFromRoot;
                                bool bMatch = true;
                                if( srcPath.Length == destPath.Length ) {
                                    for( int i=0; i< srcPath.Length; i++ )
                                        if( srcPath[i] != destPath[i] )
                                            bMatch = false;
                                    if( bMatch ) {
                                        this.m_Owner.m_TableOfContentsEntryMatches.Add( new TableOfContentsEntryMatch( entry, e ) );
                                        break;
                                    }
                                }
                            }
                        }
                    }
                }

                // Return non-null so TearDownMember can know whether we succeeded.
                return new object();
            }

            /// <summary>
            /// Gets invoked when an entry is removed from the table of contents
            /// </summary>
            /// <param name="index">The index of the entry</param>
            /// <param name="member">The entry that was removed</param>
            /// <param name="tag">Unused</param>
            protected override void TearDownMember(int index, object member, object tag) {
                if( tag == null ) return;

                TableOfContentsModel.Entry entry = ((TableOfContentsModel.Entry) member);

                // Never delete anything from the local copy, just remove the mapping from our
                // match state
                ArrayList toRemove = new ArrayList();
                foreach( TableOfContentsEntryMatch match in this.m_Owner.m_TableOfContentsEntryMatches )
                    if( entry.Id == match.SourceEntry.Id )
                        toRemove.Add( match );

                foreach( TableOfContentsEntryMatch match in toRemove )
                    this.m_Owner.m_TableOfContentsEntryMatches.Remove( match );
            }
        }

        /// <summary>
        /// Helper class that keeps track of matched pairs of TableOfContents entries
        /// </summary>
        public class TableOfContentsEntryMatch {
            // Private variables
            private readonly TableOfContentsModel.Entry m_SourceEntry;
            private readonly TableOfContentsModel.Entry m_DestEntry;

            // Accessors for the source and destination entries
            public TableOfContentsModel.Entry SourceEntry {
                get { return this.m_SourceEntry; }
            }
            public TableOfContentsModel.Entry DestEntry {
                get { return this.m_DestEntry; }
            }

            /// <summary>
            /// Construct the matching between two entries
            /// </summary>
            /// <param name="srcEntry">The source entry</param>
            /// <param name="dstEntry">The destination entry</param>
            public TableOfContentsEntryMatch( TableOfContentsModel.Entry srcEntry, TableOfContentsModel.Entry dstEntry ) {
                this.m_SourceEntry = srcEntry;
                this.m_DestEntry = dstEntry;
            }
        }
    }
}
