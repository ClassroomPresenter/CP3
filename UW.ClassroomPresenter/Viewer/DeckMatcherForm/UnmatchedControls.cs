// $Id: UnmatchedControls.cs 776 2005-09-21 20:57:14Z pediddle $

using System;
using System.Drawing;
using System.Collections;
using System.ComponentModel;
using System.Windows.Forms;

using UW.ClassroomPresenter.Model;
using UW.ClassroomPresenter.Model.Presentation;

namespace UW.ClassroomPresenter.Viewer.DeckMatcherForm {
    /// <summary>
    /// This contains the controls that allow matching of two unmatched slide decks
    /// </summary>
    public class UnmatchedGroupBox : GroupBox {
        private readonly DeckMatcherForm m_Owner;
        private readonly LocalListBox m_Local;
        private readonly RemoteListBox m_Remote;

        /// <summary>
        /// Constructs the group box
        /// </summary>
        /// <param name="location">The location of the group box</param>
        /// <param name="size">The size of the group box</param>
        /// <param name="tabIndex">The tab index of the group box</param>
        public UnmatchedGroupBox( DeckMatcherForm owner, Point location, Size size, int tabIndex ) {
            this.m_Owner = owner;

            this.SuspendLayout();

            // Set the group box properties
            this.Location = location;
            this.Size = size;
            this.TabIndex = tabIndex;
            this.Name = "UnmatchedGroupBox";
            this.TabStop = false;
            this.Text = "Unmatched";
            this.Enabled = true;

            // Add the controls
            this.Controls.Add( new MatchButton( this,
                new Point(10, this.ClientRectangle.Bottom - 45),
                new Size(this.ClientRectangle.Width-20, 35),
                0 ) );
            this.m_Local = new LocalListBox(
                new Point(10, 40),
                new Size((this.ClientRectangle.Width/2)-20, this.ClientRectangle.Height - 95),
                1 );
            this.Controls.Add( this.m_Local );
            this.m_Remote = new RemoteListBox(
                new Point(10+(this.ClientRectangle.Width/2), 40),
                new Size((this.ClientRectangle.Width/2)-20, this.ClientRectangle.Height - 95),
                2 );
            this.Controls.Add( this.m_Remote );
            this.Controls.Add( new LabelHelper( new Point(10,25), new Size(75,15), "Local") );
            this.Controls.Add( new LabelHelper( new Point(10+(this.ClientRectangle.Width/2),25), new Size(75,15), "Remote") );

            this.ResumeLayout();
        }

        #region List Management

        /// <summary>
        /// Clear the List Boxes
        /// </summary>
        public void Clear() {
            this.m_Local.Items.Clear();
            this.m_Remote.Items.Clear();
            this.m_Local.Items.Add( new DeckListBoxItem() );
        }

        /// <summary>
        /// Supress UI updates until we are finished adding items
        /// </summary>
        public void BeginUpdate() {
            this.m_Local.BeginUpdate();
            this.m_Remote.BeginUpdate();
        }

        /// <summary>
        /// Re-enable UI updates because we're finished adding items
        /// </summary>
        public void EndUpdate() {
            // Add the <self> item to the local list box
            this.m_Local.EndUpdate();
            this.m_Remote.EndUpdate();
        }

        /// <summary>
        /// Add a deck traversal to the local deck list
        /// </summary>
        /// <param name="toAdd">The deck traversal model to add</param>
        public void AddLocal( DeckTraversalModel toAdd ) {
            this.m_Local.Items.Add( new DeckListBoxItem( toAdd ) );
        }

        /// <summary>
        /// Add a deck traversal to the remote deck list
        /// </summary>
        /// <param name="toAdd">The deck traversal model to add</param>
        public void AddRemote( DeckTraversalModel toAdd ) {
            this.m_Remote.Items.Add( new DeckListBoxItem( toAdd ) );
        }

        /// <summary>
        /// Removes an entry from the local list
        /// </summary>
        /// <param name="id">The id of the item to remove</param>
        public void RemoveLocal( Guid id ) {
            this.RemoveItem( id, this.m_Local );
        }

        /// <summary>
        /// Removes an entry from the remote list
        /// </summary>
        /// <param name="id">The id of the item to remove</param>
        public void RemoveRemote( Guid id ) {
            this.RemoveItem( id, this.m_Remote );
        }

        /// <summary>
        /// Helper function that removes all instances of an
        /// item from the given ListBox control
        /// </summary>
        /// <param name="id">The guid of the item to remove</param>
        /// <param name="control">The list box to remove the item(s) from</param>
        private void RemoveItem( Guid id, ListBox control ) {
            if( id == Guid.Empty )
                return;

            ArrayList items = new ArrayList();
            // Find all occurances of the items
            foreach( DeckListBoxItem item in control.Items ) {
                if( item.Id == id )
                    items.Add( item );
            }
            // Remove all instances
            foreach( DeckListBoxItem item in items )
                control.Items.Remove( item );
        }

        /// <summary>
        /// There was a match made, pass the items up to the parent
        /// </summary>
        private void Match() {
            DeckListBoxItem localItem = this.m_Local.SelectedItem as DeckListBoxItem;
            DeckListBoxItem remoteItem = this.m_Remote.SelectedItem as DeckListBoxItem;
            if( localItem == null || remoteItem == null )
                return;
            else
                this.m_Owner.Match( localItem.Id, remoteItem.Id );
        }

        #endregion

        #region Child Controls

        /// <summary>
        /// The Match button, which matches two selected slide decks
        /// </summary>
        public class MatchButton : Button {
            private readonly UnmatchedGroupBox m_Owner;
            /// <summary>
            /// Constructs the button
            /// </summary>
            /// <param name="location">The location of the button</param>
            /// <param name="size">The size of the button</param>
            /// <param name="tabIndex">The tab index of the button</param>
            public MatchButton( UnmatchedGroupBox owner, Point location, Size size, int tabIndex ) {
                this.m_Owner = owner;

                // Set the button properties
                this.Location = location;
                this.Size = size;
                this.Name = "MatchButton";
                this.TabIndex = tabIndex;
                this.Text = "Match";
            }

            /// <summary>
            /// Matches the two decks
            /// </summary>
            /// <param name="e">The click parameters</param>
            protected override void OnClick(EventArgs e) {
                this.m_Owner.Match();
                // Update the model
                base.OnClick(e);
            }
        }

        /// <summary>
        /// A helper class that lets us build a label from the constructor
        /// </summary>
        public class LabelHelper : Label {
            /// <summary>
            /// Construct the label
            /// </summary>
            /// <param name="location">The location of the label</param>
            /// <param name="size">The size of the label</param>
            /// <param name="text">The text string of the label</param>
            public LabelHelper( Point location, Size size, string text ) {
                // Set the label properties
                this.Location = location;
                this.Size = size;
                this.Name = text;
                this.Text = text;
            }
        }

        /// <summary>
        /// The list of local slide decks that are unmatched
        /// </summary>
        public class LocalListBox : System.Windows.Forms.ListBox {
            /// <summary>
            /// Construct the list box
            /// </summary>
            /// <param name="location">The location of the list box</param>
            /// <param name="size">The size of the list box</param>
            /// <param name="tabIndex">The tab index of the list box</param>
            public LocalListBox( Point location, Size size, int tabIndex ) {
                // Set the list box properties
                this.Location = location;
                this.Size = size;
                this.Name = "LocalListBox";
                this.TabIndex = tabIndex;
                this.ScrollAlwaysVisible = true;
                this.SelectionMode = SelectionMode.One;
            }
        }
        /// <summary>
        /// The list of remote slide decks that are unmatched
        /// </summary>
        public class RemoteListBox : System.Windows.Forms.ListBox {
            /// <summary>
            /// Construct the list box
            /// </summary>
            /// <param name="location">The location of the list box</param>
            /// <param name="size">The size of the list box</param>
            /// <param name="tabIndex">The tab index of the list box</param>
            public RemoteListBox( Point location, Size size, int tabIndex ) {
                // Set the list box properties
                this.Location = location;
                this.Size = size;
                this.Name = "RemoteListBox";
                this.TabIndex = tabIndex;
                this.ScrollAlwaysVisible = true;
                this.SelectionMode = SelectionMode.One;
            }
        }

        private class DeckListBoxItem {
            private readonly Guid m_ModelId;
            private readonly string m_Title;

            public Guid Id {
                get { return this.m_ModelId; }
            }

            public DeckListBoxItem( DeckTraversalModel model ) {
                this.m_ModelId = model.Id;
                this.m_Title = "[null]";
                using( Synchronizer.Lock( model.Deck.SyncRoot ) ) {
                    this.m_Title = model.Deck.HumanName;
                }
            }

            public DeckListBoxItem() {
                this.m_ModelId = Guid.Empty;
                this.m_Title = "[self]";
            }

            public override string ToString() {
                return m_Title;
            }
        }

        #endregion
    }
}
