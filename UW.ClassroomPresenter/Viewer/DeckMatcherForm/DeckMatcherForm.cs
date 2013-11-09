// $Id: DeckMatcherForm.cs 876 2006-02-07 22:16:29Z pediddle $

using System;
using System.Drawing;
using System.Collections;
using System.ComponentModel;
using System.Windows.Forms;

using UW.ClassroomPresenter.Model;
using UW.ClassroomPresenter.Model.Presentation;
using UW.ClassroomPresenter.Model.Workspace;

namespace UW.ClassroomPresenter.Viewer.DeckMatcherForm {

    /// <summary>
    /// Summary description for DeckMatcherForm.
    /// TODO: Can also be listening to changes in the DeckTraversals list and
    /// automatically refresh our lists when there is a change, currently we only
    /// populate the list when we create the form.
    /// </summary>
    public class DeckMatcherForm : Form {
        private readonly PresenterModel m_Model;
        private readonly UnmatchedGroupBox m_UnmatchedControls;
        private readonly MatchedGroupBox m_MatchedControls;

        /// <summary>
        /// Construct the dialog box
        /// </summary>
        /// <param name="model"></param>
        public DeckMatcherForm( PresenterModel model ) {
            this.m_Model = model;

            // Setup the form UI
            this.SuspendLayout();

            this.ClientSize = new Size( 400, 500 );
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "DeckMatcherForm";
            this.Text = "Deck Matcher";

            // Add the controls
            this.Controls.Add( new DeckMatcherDoneButton( new Point(313, 470), 0 ) );
            this.m_UnmatchedControls = new UnmatchedGroupBox( this, new Point(10, 10), new Size(380,260), 1 );
            this.Controls.Add( this.m_UnmatchedControls );
            this.m_MatchedControls = new MatchedGroupBox( this, new Point(10, 280), new Size(380,180), 2 );
            this.Controls.Add( this.m_MatchedControls );

            this.ResumeLayout();

            // Populate the form...
            this.RefreshLists();
        }

        /// <summary>
        /// Refreshes the listboxes to display the correct associations
        /// </summary>
        public void RefreshLists() {
            this.m_UnmatchedControls.BeginUpdate();
            this.m_UnmatchedControls.Clear();

            using( this.m_Model.Workspace.Lock() ) {
                ArrayList local_ids = new ArrayList();
                ArrayList remote_ids = new ArrayList();
                // Get all the linked decks first
                foreach( DeckPairModel model in this.m_Model.Workspace.DeckMatches ) {
                    this.m_MatchedControls.AddMatch( model.LocalDeckTraversal, model.RemoteDeckTraversal );
                    local_ids.Add( model.LocalDeckTraversal.Id );
                    remote_ids.Add( model.RemoteDeckTraversal.Id );
                }

                // Get all the presentation deck traversals
                if( this.m_Model.Workspace.CurrentPresentation != null ) {
                    using( Synchronizer.Lock((~this.m_Model.Workspace.CurrentPresentation).SyncRoot) ) {
                        foreach( DeckTraversalModel model in (~this.m_Model.Workspace.CurrentPresentation).DeckTraversals ) {
                            if( (model.Deck.Disposition & DeckDisposition.Remote) != 0 &&
                                !remote_ids.Contains(model.Id) ) {
                                this.m_UnmatchedControls.AddRemote( model );
                                remote_ids.Add( model.Id );
                            }
                        }
                    }
                }

                // Get all the workspace deck traversals models
                foreach( DeckTraversalModel model in this.m_Model.Workspace.DeckTraversals ) {
                    if( (model.Deck.Disposition & DeckDisposition.Remote) == 0 &&
                        !local_ids.Contains(model.Id) &&
                        !remote_ids.Contains(model.Id) )
                        this.m_UnmatchedControls.AddLocal( model );
                }
            }

            this.m_UnmatchedControls.EndUpdate();
        }

        // Create a matching
        public void Match( Guid LocalDeckId, Guid RemoteDeckId ) {
            DeckTraversalModel remoteModel = null;
            DeckTraversalModel localModel = null;

            using( this.m_Model.Workspace.Lock() ) {
                // Look up the RemoteDeckId
                using( Synchronizer.Lock( (~this.m_Model.Workspace.CurrentPresentation).SyncRoot ) ) {
                    foreach( DeckTraversalModel m in (~this.m_Model.Workspace.CurrentPresentation).DeckTraversals ) {
                        if( m.Id == RemoteDeckId ) {
                            remoteModel = m;
                            break;
                        }
                    }
                }

                // Look up the LocalDeckId
                if( LocalDeckId == Guid.Empty )
                    localModel = remoteModel;
                else {
                    foreach( DeckTraversalModel m in this.m_Model.Workspace.DeckTraversals ) {
                        if( m.Id == LocalDeckId ) {
                            localModel = m;
                            break;
                        }
                    }
                }
            }

            // Ensure the model actually exists
            if( localModel == null || remoteModel == null)
                return;

            // Update the List UI
            this.m_MatchedControls.AddMatch( localModel, remoteModel );
            if( LocalDeckId == Guid.Empty )
                this.m_UnmatchedControls.RemoveLocal( RemoteDeckId );
            else
                this.m_UnmatchedControls.RemoveLocal( LocalDeckId );
            this.m_UnmatchedControls.RemoveRemote( RemoteDeckId );

            // Add a matching to the Matched Deck's List
            using( this.m_Model.Workspace.Lock() ) {
                this.m_Model.Workspace.DeckMatches.Add( new DeckPairModel( localModel, remoteModel ) );
            }
        }

        // Destroy a matching
        public void Unmatch( Guid LocalDeckId, Guid RemoteDeckId ) {
            DeckTraversalModel remoteModel = null;
            DeckTraversalModel localModel = null;

            using( this.m_Model.Workspace.Lock() ) {
                // Look up the RemoteDeckId
                using( Synchronizer.Lock( (~this.m_Model.Workspace.CurrentPresentation).SyncRoot ) ) {
                    foreach( DeckTraversalModel m in (~this.m_Model.Workspace.CurrentPresentation).DeckTraversals ) {
                        if( m.Id == RemoteDeckId ) {
                            remoteModel = m;
                            break;
                        }
                    }
                }

                // Look up the LocalDeckId
                if( LocalDeckId == Guid.Empty )
                    localModel = remoteModel;
                else {
                    foreach( DeckTraversalModel m in this.m_Model.Workspace.DeckTraversals ) {
                        if( m.Id == LocalDeckId ) {
                            localModel = m;
                            break;
                        }
                    }
                }
            }

            // Ensure the model actually exists
            if( localModel == null || remoteModel == null)
                return;

            // Add a matching to the Matched Deck's List
            this.m_MatchedControls.RemoveMatch( LocalDeckId, RemoteDeckId );

            // Prevent Duplicates from being inserted into the local list
            if( LocalDeckId == Guid.Empty )
                this.m_UnmatchedControls.RemoveLocal( RemoteDeckId );
            else {
                this.m_UnmatchedControls.RemoveLocal( LocalDeckId );
                this.m_UnmatchedControls.AddLocal( localModel );
            }

            // Return the item to the remote list
            this.m_UnmatchedControls.RemoveRemote( RemoteDeckId );
            this.m_UnmatchedControls.AddRemote( remoteModel );

            // Add a matching to the Matched Deck's List
            using( this.m_Model.Workspace.Lock() ) {
                DeckPairModel toRemove = new DeckPairModel( localModel, remoteModel );
                if( this.m_Model.Workspace.DeckMatches.Contains( toRemove ) )
                    this.m_Model.Workspace.DeckMatches.Remove( toRemove );
            }
        }

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        protected override void Dispose( bool disposing ) {
            base.Dispose( disposing );
        }

        #region Buttons

        /// <summary>
        /// The OK Button for the Properties Form
        /// </summary>
        public class DeckMatcherDoneButton : Button {
            /// <summary>
            /// Constructs the Done Button
            /// </summary>
            /// <param name="location">The location of the button</param>
            /// <param name="tabIndex">The tab index of the button</param>
            public DeckMatcherDoneButton( Point location, int tabIndex ) {
                this.DialogResult = System.Windows.Forms.DialogResult.OK;
                this.Location = location;
                this.Name = "DeckMatcherDoneButton";
                this.TabIndex = tabIndex;
                this.Text = "Done";
            }

            /// <summary>
            /// When clicked doing any final tasks, then close the form
            /// </summary>
            /// <param name="e">Click parameters</param>
            protected override void OnClick(EventArgs e) {
                // Update the model
                base.OnClick(e);
            }
        }

        #endregion
    }
}
