// $Id: NetworkAssociationService.cs 1428 2007-08-28 21:03:56Z fred $

using System;
using System.Collections;
using System.Diagnostics;
using System.Threading;
using System.Windows.Forms;

using UW.ClassroomPresenter.Model;
using UW.ClassroomPresenter.Model.Network;
using UW.ClassroomPresenter.Model.Workspace;
using UW.ClassroomPresenter.Model.Presentation;

using UW.ClassroomPresenter.DeckMatcher;

namespace UW.ClassroomPresenter.Network {
    public class NetworkAssociationService : IDisposable {
        private readonly PresenterModel m_Model;

        private readonly EventQueue m_EventQueue;
        private readonly EventQueue.PropertyEventDispatcher m_AssociationChangedDispatcher;
        private readonly EventQueue.PropertyEventDispatcher m_RoleChangedDispatcher;
        private readonly EventQueue.PropertyEventDispatcher m_CurrentPresentationChangedDispatcher;
        private readonly EventQueue.PropertyEventDispatcher m_CurrentDeckTraversalChangedDispatcher;

        private ParticipantModel m_Association;
        private InstructorModel m_Instructor;
        private PresentationModel m_CurrentPresentation;
        private DeckTraversalsCollectionHelper m_DeckTraversalsCollectionHelper;

        private bool m_Disposed;

        public NetworkAssociationService(EventQueue dispatcher, PresenterModel model) {
            this.m_Model = model;

            this.m_EventQueue = dispatcher;
            this.m_AssociationChangedDispatcher = new EventQueue.PropertyEventDispatcher(this.m_EventQueue, new PropertyEventHandler(this.HandleAssociationChanged));
            this.m_RoleChangedDispatcher = new EventQueue.PropertyEventDispatcher(this.m_EventQueue, new PropertyEventHandler(this.HandleRoleChanged));
            this.m_CurrentPresentationChangedDispatcher = new EventQueue.PropertyEventDispatcher(this.m_EventQueue, new PropertyEventHandler(this.HandleCurrentPresentationChanged));
            this.m_CurrentDeckTraversalChangedDispatcher = new EventQueue.PropertyEventDispatcher(this.m_EventQueue, new PropertyEventHandler(this.HandleCurrentDeckTraversalChanged));

            this.m_Model.Network.Changed["Association"].Add(this.m_AssociationChangedDispatcher.Dispatcher);
            this.m_AssociationChangedDispatcher.Dispatcher(this, null);
        }

        #region IDisposable Members

        ~NetworkAssociationService() {
            this.Dispose(false);
        }

        public void Dispose() {
            this.Dispose(true);
            System.GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing) {
            if(this.m_Disposed) return;
            if(disposing) {
                this.m_Model.Network.Changed["Association"].Remove(this.m_AssociationChangedDispatcher.Dispatcher);
            }
            this.m_Disposed = true;
        }

        #endregion

        private ParticipantModel Association {
            set {
                using(Synchronizer.Lock(this)) {
                    if(this.m_Association == value)
                        return;

                    if(this.m_Association != null) {
                        this.m_Association.Changed["Role"].Remove(this.m_RoleChangedDispatcher.Dispatcher);
                    }

                    this.m_Association = value;

                    if(this.m_Association != null) {
                        this.m_Association.Changed["Role"].Add(this.m_RoleChangedDispatcher.Dispatcher);
                    }

                    this.m_RoleChangedDispatcher.Dispatcher(this, null);
                }
            }
        }

        private InstructorModel Instructor {
            set {
                using(Synchronizer.Lock(this)) {
                    if(this.m_Instructor == value)
                        return;

                    if(this.m_Instructor != null) {
                        this.m_Instructor.Changed["CurrentPresentation"].Remove(this.m_CurrentPresentationChangedDispatcher.Dispatcher);
                        this.m_Instructor.Changed["CurrentDeckTraversal"].Remove(this.m_CurrentDeckTraversalChangedDispatcher.Dispatcher);
                    }

                    this.m_Instructor = value;

                    if(this.m_Instructor != null) {
                        this.m_Instructor.Changed["CurrentPresentation"].Add(this.m_CurrentPresentationChangedDispatcher.Dispatcher);
                        this.m_Instructor.Changed["CurrentDeckTraversal"].Add(this.m_CurrentDeckTraversalChangedDispatcher.Dispatcher);
                    }

                    this.m_CurrentPresentationChangedDispatcher.Dispatcher(this, null);
                    this.m_CurrentDeckTraversalChangedDispatcher.Dispatcher(this, null);
                }
            }
        }

        private PresentationModel CurrentPresentation {
            get { return this.m_CurrentPresentation; }
            set {
                using(Synchronizer.Lock(this)) {
                    if(this.m_CurrentPresentation == value)
                        return;

                    if(this.m_CurrentPresentation != null) {
                        this.m_DeckTraversalsCollectionHelper.Dispose();
                        this.m_DeckTraversalsCollectionHelper = null;
                    }

                    this.m_CurrentPresentation = value;

                    if(this.m_CurrentPresentation != null) {
                        this.m_DeckTraversalsCollectionHelper = new DeckTraversalsCollectionHelper(this, this.m_CurrentPresentation);
                    }
                }
            }
        }

        private void HandleAssociationChanged(object sender, PropertyEventArgs args_) {
            using(Synchronizer.Lock(this)) {
                using(Synchronizer.Lock(this.m_Model.Network.SyncRoot)) {
                    this.Association = this.m_Model.Network.Association;
                }
            }
        }

        private void HandleRoleChanged(object sender, PropertyEventArgs args_) {
            using(Synchronizer.Lock(this)) {
                if(this.m_Association != null) {
                    using(Synchronizer.Lock(this.m_Association.SyncRoot)) {
                        this.Instructor = this.m_Association.Role as InstructorModel;
                    }
                } else {
                    this.Instructor = null;
                }
            }
        }

        private void HandleCurrentPresentationChanged(object sender, PropertyEventArgs args_) {
            using(Synchronizer.Lock(this)) {
                if(this.m_Instructor == null) {
                    this.CurrentPresentation = null;
                } else {
                    using(Synchronizer.Lock(this.m_Instructor.SyncRoot)) {
                        this.CurrentPresentation = this.m_Instructor.CurrentPresentation;
                        // Release the lock before proceeding because there is no "natural" parent/child locking order.
                    }
                }

                using(this.m_Model.Workspace.Lock()) {
                    this.m_Model.Workspace.CurrentPresentation.Value = this.CurrentPresentation;
                }
            }
        }

        private void HandleCurrentDeckTraversalChanged(object sender, PropertyEventArgs args_) {
            using (Synchronizer.Lock(this)) {
                DeckTraversalModel current;
                if (this.m_Instructor == null) {
                    current = null;
                }
                else {
                    using (Synchronizer.Lock(this.m_Instructor.SyncRoot)) {
                        current = this.m_Instructor.CurrentDeckTraversal;
                    }
                }

                using (this.m_Model.Workspace.Lock()) {
                    if (current != null) {
                        // Check the workspace to see if there exists a linkedDeckTraversal for this ID
                        foreach (DeckTraversalModel model in this.m_Model.Workspace.DeckTraversals) {
                            if (model.Id == current.Id) {
                                current = model;
                                break;
                            }
                        }
                    }
                }

                // TODO: Only set the current deck traversal if navigation is unlinked.
                bool isInstructor = false;
                using (Synchronizer.Lock(this.m_Model.Participant.SyncRoot)) {
                    if (this.m_Model.Participant.Role is InstructorModel) {
                        isInstructor = true;
                    }
                }

                DeckTraversalModel dtm = null;
                if ((current is LinkedDeckTraversalModel) || (isInstructor) || (current == null)) {
                    dtm = current;
                }
                else {
                    dtm = new LinkedDeckTraversalModel(this.m_EventQueue, current.Id, this.m_Model, current);
                }

                using (this.m_Model.Workspace.Lock()) {
                    this.m_Model.Workspace.CurrentDeckTraversal.Value = dtm;
                }
            }
        }
        /// <summary>
        /// Helper class that listens for additions to the Presentation's DeckTraversals
        /// </summary>
        private class DeckTraversalsCollectionHelper : PropertyCollectionHelper {
            private readonly NetworkAssociationService m_Service;
            private readonly WorkspaceModel m_Bucket;

            public DeckTraversalsCollectionHelper(NetworkAssociationService service, PresentationModel source) : base(service.m_EventQueue, source, "DeckTraversals") {
                this.m_Service = service;
                this.m_Bucket = this.m_Service.m_Model.Workspace;

                base.Initialize();
            }

            protected override void Dispose(bool disposing) {
                if(disposing)
                    foreach(object tag in this.Tags)
                        this.TearDownMember(-1, null, tag);
                base.Dispose(disposing);
            }

            protected override object SetUpMember(int index, object member) {
                using(Synchronizer.Lock(this.m_Service)) {
                    using(this.m_Bucket.Lock()) {
                        DeckTraversalModel traversal = ((DeckTraversalModel) member);

                        if((traversal.Deck.Disposition & DeckDisposition.Remote) != 0) {
                            // A remote deck, perform deck matching
                            // TODO: DeckMatching Should Occur Here
                            // CURRENT IMPLEMENTATION ASSUMES THERE WAS NO MATCH
                            // NOTE: All DeckTraversals need to ability to be linked since with
                            //       deckmatching any deck can become associated with a remote deck.
                            DeckTraversalModel matchedDeckTraversal = null;

                            // Perform the DeckMatching (NOTE: Does Nothing Right Now)
                            matchedDeckTraversal = DeckMatcherService.MatchDeck( traversal );

                            if( matchedDeckTraversal == null ) {
                                // No Matched Deck Found
                                // TODO: Evaluate whether it's a good idea to copy the existing traversal's Id.
                                matchedDeckTraversal = new LinkedDeckTraversalModel(this.m_Service.m_EventQueue, traversal.Id, this.m_Service.m_Model, traversal);

                                // Create the DeckMatch
                                this.m_Bucket.DeckMatches.Add( new DeckPairModel( matchedDeckTraversal, traversal ) );
                            }
                            else {
                                // Create the DeckMatch
                                this.m_Bucket.DeckMatches.Add( new DeckPairModel( matchedDeckTraversal, traversal ) );
                                // NOTE: Don't have to add to the workspace because it's already there.
                                // NOTE: But should we remove and replace with a LinkedDeckTraversalModel??
                            }

                            traversal = matchedDeckTraversal;
                        }
                        else {
                            // Not a remote deck, just add to the workspace
                            this.m_Bucket.DeckTraversals.Add(traversal);
                        }

                        return traversal;
                    }
                }
            }

            /// <summary>
            /// TODO: Check if the removed deck-traversal was part of a deck match, and if so do the right thing.
            /// </summary>
            /// <param name="index"></param>
            /// <param name="member"></param>
            /// <param name="tag"></param>
            protected override void TearDownMember(int index, object member, object tag) {
                if(tag != null) {
                    using(Synchronizer.Lock(this.m_Service)) {
                        DeckTraversalModel traversal = ((DeckTraversalModel) tag);
                        using(this.m_Bucket.Lock()) {
                            this.m_Bucket.DeckTraversals.Remove(traversal);

                            DeckTraversalModel linked = null;

                            // Check if there is a LinkedDeckTraversalModel with the same ID
                            foreach( DeckTraversalModel model in this.m_Bucket.DeckTraversals ) {
                                if( model != null &&
                                    model is LinkedDeckTraversalModel &&
                                    model.Id == traversal.Id ) {
                                    linked = model;
                                    break;
                                }
                            }

                            if(linked != null) {
                                linked.Dispose();
                            }
                        }
                    }
                }
            }
        }
    }
}
