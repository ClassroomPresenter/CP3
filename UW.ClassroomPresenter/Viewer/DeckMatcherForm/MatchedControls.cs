// $Id: MatchedControls.cs 776 2005-09-21 20:57:14Z pediddle $

using System;
using System.Drawing;
using System.Collections;
using System.ComponentModel;
using System.Windows.Forms;

using UW.ClassroomPresenter.Model;
using UW.ClassroomPresenter.Model.Presentation;

namespace UW.ClassroomPresenter.Viewer.DeckMatcherForm {
    /// <summary>
    /// This contains the controls that allows unmatching two previously matched slide decks
    /// </summary>
    public class MatchedGroupBox : GroupBox {
        private readonly DeckMatcherForm m_Owner;
        private readonly MatchedListBox m_MatchedListBox;

        /// <summary>
        /// Contructs the group and all its child controls
        /// </summary>
        /// <param name="location">The location of this group</param>
        /// <param name="size">The size of this group</param>
        /// <param name="tabIndex">The tab index of this group</param>
        public MatchedGroupBox( DeckMatcherForm owner, Point location, Size size, int tabIndex ) {
            this.m_Owner = owner;

            this.SuspendLayout();

            // Set the GroupBox properties
            this.Location = location;
            this.Size = size;
            this.TabIndex = tabIndex;
            this.Name = "MatchedGroupBox";
            this.TabStop = false;
            this.Text = "Matched";
            this.Enabled = true;

            // Add the controls
            this.Controls.Add( new UnmatchButton( this,
                new Point(10, this.ClientRectangle.Bottom - 45),
                new Size(this.ClientRectangle.Width-20, 35),
                0 ) );
            this.m_MatchedListBox = new MatchedListBox(
                new Point(10, 20),
                new Size(this.ClientRectangle.Width-20, this.ClientRectangle.Height - 70),
                0 );
            this.Controls.Add( this.m_MatchedListBox );

            this.ResumeLayout();
        }

        #region List Management

        /// <summary>
        /// Clear the List Box
        /// </summary>
        public void Clear() {
            this.m_MatchedListBox.Items.Clear();
        }

        /// <summary>
        /// Supress UI updates until we are finished adding items
        /// </summary>
        public void BeginUpdate() {
            this.m_MatchedListBox.BeginUpdate();
        }

        /// <summary>
        /// Re-enable UI updates because we're finished adding items
        /// </summary>
        public void EndUpdate() {
            this.m_MatchedListBox.EndUpdate();
        }

        /// <summary>
        /// Add a deck traversal to the match list
        /// </summary>
        /// <param name="localDeck">The deck traversal model to add</param>
        /// <param name="remoteDeck">The deck traversal model to add</param>
        public void AddMatch( DeckTraversalModel localDeck, DeckTraversalModel remoteDeck ) {
            this.m_MatchedListBox.Items.Add( new DecksListBoxItem( localDeck, remoteDeck ) );
        }

        /// <summary>
        /// Removes an entry from the list
        /// </summary>
        /// <param name="id">The id of the item to remove</param>
        public void RemoveMatch( Guid localId, Guid remoteId ) {
            ArrayList items = new ArrayList();
            // Find all occurances of the items
            foreach( DecksListBoxItem item in this.m_MatchedListBox.Items ) {
                if( item.LocalId == localId && item.RemoteId == remoteId )
                    items.Add( item );
            }
            // Remove all instances
            foreach( DecksListBoxItem item in items )
                this.m_MatchedListBox.Items.Remove( item );
        }

        /// <summary>
        /// There was a match made, pass the items up to the parent
        /// </summary>
        private void Unmatch() {
            DecksListBoxItem item = this.m_MatchedListBox.SelectedItem as DecksListBoxItem;
            if( item == null )
                return;
            else
                this.m_Owner.Unmatch( item.LocalId, item.RemoteId );
        }

        #endregion

        #region Child Controls

        /// <summary>
        /// The Unmatch button, that will unmatch selected decks in the listbox
        /// </summary>
        public class UnmatchButton : Button {
            private readonly MatchedGroupBox m_Owner;
            /// <summary>
            /// Constructs the unmatch button
            /// </summary>
            /// <param name="location">The location of the button</param>
            /// <param name="size">The size of the button</param>
            /// <param name="tabIndex">The tab index of the button</param>
            public UnmatchButton( MatchedGroupBox owner, Point location, Size size, int tabIndex ) {
                this.m_Owner = owner;
                // Set the button properties
                this.Location = location;
                this.Size = size;
                this.Name = "UnmatchButton";
                this.TabIndex = tabIndex;
                this.Text = "Unmatch";
            }

            /// <summary>
            /// Get the Slidematch from the listview and unmatch the decks
            /// </summary>
            /// <param name="e"></param>
            protected override void OnClick(EventArgs e) {
                this.m_Owner.Unmatch();
                base.OnClick(e);
            }
        }

        /// <summary>
        /// The listbox that contains all the decks that are matched
        /// </summary>
        public class MatchedListBox : System.Windows.Forms.ListBox {
            /// <summary>
            /// Constructs the list box
            /// </summary>
            /// <param name="location">The location of the list box</param>
            /// <param name="size">The size of the list box</param>
            /// <param name="tabIndex">The tab index of the list box</param>
            public MatchedListBox( Point location, Size size, int tabIndex ) {
                // Set the list box properties
                this.Location = location;
                this.Size = size;
                this.Name = "MatchedListBox";
                this.TabIndex = tabIndex;
                this.ScrollAlwaysVisible = true;
                this.SelectionMode = SelectionMode.One;
            }
        }

        private class DecksListBoxItem {
            private readonly Guid m_LocalId;
            private readonly string m_LocalTitle;
            private readonly Guid m_RemoteId;
            private readonly string m_RemoteTitle;

            public Guid LocalId {
                get { return this.m_LocalId; }
            }
            public Guid RemoteId {
                get { return this.m_RemoteId; }
            }

            public DecksListBoxItem( DeckTraversalModel localModel, DeckTraversalModel remoteModel ) {
                this.m_LocalId = localModel.Id;
                this.m_RemoteId = remoteModel.Id;
                this.m_LocalTitle = "<null>";
                this.m_RemoteTitle = "<null>";

                if( remoteModel.Id == localModel.Id ) {
                    this.m_LocalId = Guid.Empty;
                    this.m_LocalTitle = "[self]";
                }
                else {
                    using( Synchronizer.Lock( localModel.Deck.SyncRoot ) ) {
                        this.m_LocalTitle = localModel.Deck.HumanName;
                    }
                }

                using( Synchronizer.Lock( remoteModel.Deck.SyncRoot ) ) {
                    this.m_RemoteTitle = remoteModel.Deck.HumanName;
                }
            }

            public override string ToString() {
                return this.m_LocalTitle + " <----> " + this.m_RemoteTitle;
            }
        }

        #endregion
    }
}
