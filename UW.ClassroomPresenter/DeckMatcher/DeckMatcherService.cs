// $Id: DeckMatcherService.cs 1018 2006-07-14 00:34:38Z pediddle $

using System;
using System.Collections;
using UW.ClassroomPresenter.Model;
using UW.ClassroomPresenter.Model.Workspace;
using UW.ClassroomPresenter.Model.Network;
using UW.ClassroomPresenter.Model.Presentation;

namespace UW.ClassroomPresenter.DeckMatcher {
    /// <summary>
    /// Listens to changes in the DeckPairsCollection and creates the physical DeckTraversalMatch objects for the matchings
    /// </summary>
    public class DeckMatcherService : IDisposable {
        private readonly EventQueue m_Sender;
        private readonly PresenterModel m_Model;
        private readonly WorkspaceModel m_Workspace;
        private readonly DeckPairCollectionHelper m_DeckPairCollectionHelper;
        private readonly ArrayList m_DeckTraversalMatchCollection;

        /// <summary>
        /// Creates a deck matching service that listens for decks to be added and matches them automatically
        /// and/or manually.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="local"></param>
        public DeckMatcherService( EventQueue sender, PresenterModel model ) {
            this.m_Sender = sender;
            this.m_Model = model;
            this.m_Workspace = model.Workspace;
            this.m_DeckTraversalMatchCollection = new ArrayList();

            this.m_DeckPairCollectionHelper = new DeckPairCollectionHelper( this );
        }

        ~DeckMatcherService() {
            this.Dispose(false);
        }

        public void Dispose() {
            this.Dispose(true);
            System.GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing) {
            if (disposing) {
                this.m_DeckPairCollectionHelper.Dispose();
            }
        }

        // Match decks...
        public static DeckTraversalModel MatchDeck( DeckTraversalModel toMatch ) {
            return null;
        }

        /// <summary>
        /// Listens for adds and removals to the deckmatchings and build the appropriate DeckMatch objects
        /// </summary>
        private class DeckPairCollectionHelper : CollectionMirror<DeckPairModel, IDisposable> {
            private readonly DeckMatcherService m_Service;

            public DeckPairCollectionHelper( DeckMatcherService service ) : base(service.m_Sender, service.m_Workspace.DeckMatches) {
                this.m_Service = service;
                base.Initialize(true);
            }

            /// <summary>
            /// Setup the change event marshallers when a new matching is added to the set of DeckMatches
            /// </summary>
            /// <param name="index">The item index in the collection</param>
            /// <param name="member">The item added to the collection</param>
            /// <returns>The object that was added</returns>
            protected override bool SetUp(int index, DeckPairModel pair, out IDisposable tag) {
                // Create the matching and setup the change marshallers
                using(Synchronizer.Lock(this.m_Service)) {
                    DeckTraversalMatch match = new DeckTraversalMatch( this.m_Service.m_Sender, pair.RemoteDeckTraversal, pair.LocalDeckTraversal );
                    if( !this.m_Service.m_DeckTraversalMatchCollection.Contains( match ) )
                        this.m_Service.m_DeckTraversalMatchCollection.Add( match );
                }

                // Replace the existing deck with a linked deck traversal or add a new linked deck traversal
                using(this.m_Service.m_Workspace.Lock()) {
                    DeckTraversalModel linked = null;
                    bool bCurrent = false;
                    if( pair.SameDeckTraversal ) {
                        linked = new LinkedDeckTraversalModel(this.m_Service.m_Sender, pair.RemoteDeckTraversal.Id, this.m_Service.m_Model, pair.RemoteDeckTraversal);
                    }
                    else {
                        // Remove the old item and replace with a linked deck traversal model
                        // NOTE: We need to remember if the deck we're replacing was the current one,
                        //       so we can make the new deck also the current one
                        bCurrent = (~this.m_Service.m_Workspace.CurrentDeckTraversal == pair.LocalDeckTraversal);
                        this.m_Service.m_Workspace.DeckTraversals.Remove( pair.LocalDeckTraversal );
                        linked = new LinkedDeckTraversalModel(this.m_Service.m_Sender, pair.LocalDeckTraversal.Id, this.m_Service.m_Model, pair.LocalDeckTraversal);
                    }

                    // Add the deck traversal to the workspace
                    if( linked != null )
                        this.m_Service.m_Workspace.DeckTraversals.Add( linked );
                    if( bCurrent )
                        this.m_Service.m_Workspace.CurrentDeckTraversal.Value = linked;
                }

                tag = null;
                return true;
            }

            /// <summary>
            /// Remove a DeckTraversalMatch from the collection when a DeckPair is removed from the model
            /// </summary>
            /// <param name="index">The index of the object that was removed</param>
            /// <param name="member">Not sure, unused??</param>
            /// <param name="tag">The object that was removed</param>
            protected override void TearDown(int index, DeckPairModel pair, IDisposable tag) {
                using(Synchronizer.Lock(this.m_Service)) {
                    ArrayList toRemove = new ArrayList();

                    // Find the objects that need to be removed
                    foreach( DeckTraversalMatch match in this.m_Service.m_DeckTraversalMatchCollection ) {
                        if( match != null &&
                            match.DestDeckTraversal.Id == pair.LocalDeckTraversal.Id &&
                            match.SourceDeckTraversal.Id == pair.RemoteDeckTraversal.Id ) {
                            toRemove.Add( match );
                        }
                    }

                    // Remove the objects and destroy them
                    foreach( DeckTraversalMatch match in toRemove ) {
                        this.m_Service.m_DeckTraversalMatchCollection.Remove( match );
                        match.Dispose();
                    }

                    // Remove the DeckTraversal from the workspace if the local Deck is remote
                    using(this.m_Service.m_Workspace.Lock()) {

                        // Find all the local Traversals with the given ID
                        ArrayList localTraversals = new ArrayList();
                        foreach( DeckTraversalModel model in this.m_Service.m_Workspace.DeckTraversals ) {
                            if( model is LinkedDeckTraversalModel &&
                                model.Id == pair.LocalDeckTraversal.Id )
                                localTraversals.Add( model );
                        }

                        // Remove the DeckTraversal from the workspace if the local Deck is remote
                        if( (pair.LocalDeckTraversal.Deck.Disposition & DeckDisposition.Remote) != 0 ) {
                            if( localTraversals.Contains( this.m_Service.m_Workspace.CurrentDeckTraversal ) )
                                this.m_Service.m_Workspace.CurrentDeckTraversal.Value = null;
                            // Remove the DeckTraversals from the workspace
                            foreach( DeckTraversalModel model in localTraversals ) {
                                this.m_Service.m_Workspace.DeckTraversals.Remove( model );
                                model.Dispose();
                            }
                        }
                        else {
                            // Replace the local linked model with the local unlinked version...
                            // Add the model into the workspace
                            if( localTraversals.Count > 0 ) {
                                this.m_Service.m_Workspace.DeckTraversals.Add( ((LinkedDeckTraversalModel)localTraversals[0]).LinkedModel );
                                if( localTraversals.Contains( this.m_Service.m_Workspace.CurrentDeckTraversal ) )
                                    this.m_Service.m_Workspace.CurrentDeckTraversal.Value = ((LinkedDeckTraversalModel)localTraversals[0]).LinkedModel;
                            }
                            // Remove all the models from the workspace
                            foreach( DeckTraversalModel model in localTraversals ) {
                                this.m_Service.m_Workspace.DeckTraversals.Remove( model );
                                model.Dispose();
                            }
                        }
                    }
                }
            }
        }
    }
}
