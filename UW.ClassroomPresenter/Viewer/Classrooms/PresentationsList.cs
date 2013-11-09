// $Id: PresentationsList.cs 1045 2006-07-20 17:44:24Z julenka $

using System;
using System.Collections;
using System.Diagnostics;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using System.Globalization;

using UW.ClassroomPresenter.Model;
using UW.ClassroomPresenter.Model.Network;
using UW.ClassroomPresenter.Model.Presentation;

namespace UW.ClassroomPresenter.Viewer.Classrooms {
    public class PresentationsListView : ListView {
        private readonly ControlEventQueue m_EventQueue;
        private readonly PresenterModel m_Model;
        private readonly ProtocolCollectionHelper m_ProtocolCollectionHelper;
        private bool m_Disposed;

        #region Construction & Destruction

        public PresentationsListView(PresenterModel model) {
            if (model == null)
                throw new ArgumentNullException("model");

            this.m_EventQueue = new ControlEventQueue(this);
            this.m_Model = model;

            this.View = View.Details;
            this.FullRowSelect = true;
            this.GridLines = true;
            this.Sorting = SortOrder.None;
            this.CheckBoxes = false;
            this.MultiSelect = false;
            this.HideSelection = false;

            // TODO: Add icons for presentations.
            // In the mean time, this servers to make the rows big enough to
            // be an easy target for a stylus.
            this.SmallImageList = new ImageList();
            this.SmallImageList.ImageSize = new Size(1, 40);

            this.Font = new Font(this.Font.FontFamily, this.Font.Size * 4 / 3);

            this.Columns.AddRange(new ColumnHeader[] { new ColumnHeader(), new ColumnHeader(), new ColumnHeader() });
            foreach(ColumnHeader column in this.Columns) column.Width = -1;
            this.Columns[0].Text = "Presentations";
            this.Columns[1].Text = "Instructor";
            this.Columns[2].Text = "Participants";

            // Create a handle immediately so the ListViewItems can marshal event handlers to the creator thread.
            this.CreateHandle();

            // Add a default local classroom to the list
            this.Items.Add( new ListViewItem( "New Presentation" ) );
            this.Items[this.Items.Count - 1].Selected = true;

            this.m_ProtocolCollectionHelper = new ProtocolCollectionHelper(this, this.m_Model.Network);
        }

        protected override void Dispose(bool disposing) {
            if(this.m_Disposed) return;

            try {
                if(disposing) {
                    this.m_ProtocolCollectionHelper.Dispose();

                    if(this.SmallImageList != null)
                        this.SmallImageList.Dispose();
                    if(this.LargeImageList != null)
                        this.LargeImageList.Dispose();

                    this.m_EventQueue.Dispose();
                }
            } finally {
                base.Dispose(disposing);
            }

            this.m_Disposed = true;
        }

        #endregion Construction & Destruction


        protected override void OnSizeChanged(EventArgs e) {
            base.OnSizeChanged(e);
            // Resize the column headers so they fill the entire width of the control.
            // This also happens during initialization.
            // FIXME: Need to find a better way to do this, because this happens *after*
            //   the control is resized, confusing the scroll bars and causing re-layout.
            int remainder = this.ClientRectangle.Width;
            for(int i = this.Columns.Count, width; --i >= 1; remainder -= width)
                this.Columns[i].Width = (width = this.FontHeight * this.Columns[i].Text.Length);
            this.Columns[0].Width = Math.Max((this.FontHeight * this.Columns[0].Text.Length), remainder);
        }

        /// <summary>
        /// Helper variable that prevents the selection from being disabled
        /// </summary>
        private bool cancelNextChange = false;

        protected override void OnMouseDown( MouseEventArgs e ) {
            base.OnMouseDown( e );
            if( this.GetItemAt( e.X, e.Y ) == null ) {
                this.cancelNextChange = true;
            }
        }
        protected override void OnMouseUp( MouseEventArgs e ) {
            base.OnMouseUp( e );
            this.cancelNextChange = false;
        }

        /// <summary>
        /// Handle an item being selected/de-selected
        /// </summary>
        /// <param name="e"></param>
        protected override void OnItemSelectionChanged( ListViewItemSelectionChangedEventArgs e ) {
            base.OnItemSelectionChanged( e );

            // Ignore notifications from items which have not yet been added to the Items collection.
            // This is necessary since the control's handle will already have been created as items are being added.
            if( e.ItemIndex >= this.Items.Count )
                return;

            // Select the same item if we tried to select nothing
            if( cancelNextChange == true && e.IsSelected == false ) {
                e.Item.Focused = true;
                e.Item.Selected = true;
                // NOTE: We must already be connected because we were selected before
            } 
        }

        protected override void OnItemActivate(EventArgs e) {
            base.OnItemActivate(e);

            ParticipantModel association = this.SelectedItems.Count > 0
                ? this.SelectedItems[0].Tag as ParticipantModel : null;

            if( association != null ) {
                using( Synchronizer.Lock( this.m_Model.Participant ) ) {
                    if( this.m_Model.Participant.Role is InstructorModel || this.m_Model.Participant.Role == null )
                        this.m_Model.Participant.Role = new StudentModel( Guid.NewGuid() );
                }

                using( Synchronizer.Lock( this.m_Model.Network.SyncRoot ) ) {
                    this.m_Model.Network.Association = association;
                }
            } else {
                StartJoinButton.StartEmptyPresentation( this.m_Model );
            }
        }


        private class ProtocolCollectionHelper : PropertyCollectionHelper {
            private readonly PresentationsListView m_Parent;

            public ProtocolCollectionHelper(PresentationsListView parent, NetworkModel network) : base(parent.m_EventQueue, network, "Protocols") {
                this.m_Parent = parent;
                base.Initialize();
            }

            protected override void Dispose(bool disposing) {
                try {
                    if (disposing)
                        foreach (ClassroomCollectionHelper helper in this.Tags)
                            helper.Dispose();
                } finally {
                    base.Dispose(disposing);
                }
            }

            protected override object SetUpMember(int index, object member) {
                return new ClassroomCollectionHelper(this.m_Parent, ((ProtocolModel) member));
            }

            protected override void TearDownMember(int index, object member, object tag) {
                if(tag != null)
                    ((ClassroomCollectionHelper) tag).Dispose();
            }
        }


        private class ClassroomCollectionHelper : PropertyCollectionHelper {
            private readonly PresentationsListView m_Parent;

            public ClassroomCollectionHelper(PresentationsListView parent, ProtocolModel protocol) : base(parent.m_EventQueue, protocol, "Classrooms") {
                this.m_Parent = parent;
                base.Initialize();
            }

            protected override void Dispose(bool disposing) {
                try {
                    if (disposing)
                        foreach (PresentationCollectionHelper helper in this.Tags)
                            helper.Dispose();
                } finally {
                    base.Dispose(disposing);
                }
            }

            protected override object SetUpMember(int index, object member) {
                return new PresentationCollectionHelper(this.m_Parent, ((ClassroomModel) member));
            }

            protected override void TearDownMember(int index, object member, object tag) {
                if(tag != null)
                    ((PresentationCollectionHelper) tag).Dispose();
            }
        }


        private class PresentationCollectionHelper : PropertyCollectionHelper {
            private readonly PresentationsListView m_Parent;

            public PresentationCollectionHelper(PresentationsListView parent, ClassroomModel classroom) : base(parent.m_EventQueue, classroom, "Presentations") {
                this.m_Parent = parent;
                base.Initialize();
            }

            protected override void Dispose(bool disposing) {
                try {
                    if (disposing)
                        foreach (PresentationListViewItem item in this.Tags)
                            item.Remove();
                } finally {
                    base.Dispose(disposing);
                }
            }

            protected override object SetUpMember(int index, object member) {
                Debug.Assert(!this.m_Parent.InvokeRequired);

                DisposableListViewItem item = new PresentationListViewItem(this.m_Parent.m_EventQueue, (PresentationModel)member);
                
                this.m_Parent.Items.Add(item);
                return item;
            }

            protected override void TearDownMember(int index, object member, object tag) {
                Debug.Assert(!this.m_Parent.InvokeRequired);

                DisposableListViewItem item = tag as DisposableListViewItem;

                if (item != null) {
                    if( item.Selected ) {
                        this.m_Parent.Items[0].Focused = true;
                        this.m_Parent.Items[0].Selected = true;
                    }

                    item.Remove();
                    item.Dispose();
                }
            }
        }


        #region PresentationListViewItem

        class PresentationListViewItem : DisposableListViewItem {
            private readonly ControlEventQueue m_EventQueue;
            private readonly EventQueue.PropertyEventDispatcher m_HumanNameChangedDispatcher;
            private bool m_Disposed;

            private readonly OwnerSubItem m_OwnerSubItem;
            private readonly ParticipantsSubItem m_ParticipantsSubItem;

            private readonly PresentationModel m_Presentation;

            public PresentationListViewItem(ControlEventQueue dispatcher, PresentationModel presentation) {
                this.m_EventQueue = dispatcher;
                this.m_Presentation = presentation;
                this.Tag = presentation.Owner;

                this.SubItems.Add(this.m_OwnerSubItem = new OwnerSubItem(this));
                this.SubItems.Add(this.m_ParticipantsSubItem = new ParticipantsSubItem(this));

                this.m_HumanNameChangedDispatcher = new EventQueue.PropertyEventDispatcher(this.m_EventQueue, new PropertyEventHandler(this.HandleHumanNameChanged));
                this.Presentation.Changed["HumanName"].Add(this.m_HumanNameChangedDispatcher.Dispatcher);
                this.m_HumanNameChangedDispatcher.Dispatcher(this.Presentation, null);
            }

            protected override void Dispose(bool disposing) {
                if (!this.m_Disposed) {
                    if (disposing) {
                        this.Presentation.Changed["HumanName"].Remove(this.m_HumanNameChangedDispatcher.Dispatcher);

                        this.m_OwnerSubItem.Dispose();
                        this.m_ParticipantsSubItem.Dispose();
                    }
                    this.m_Disposed = true;
                }
            }

            public PresentationModel Presentation {
                get { return this.m_Presentation; }
            }

            private void HandleHumanNameChanged(object sender, PropertyEventArgs args_) {
                using(Synchronizer.Lock(this.Presentation.SyncRoot)) {
                    if (!this.Presentation.IsUntitledPresentation) this.Text = this.Presentation.HumanName;
                    else this.Text = Strings.UntitledPresentation;
                }
            }


            private class ParticipantsSubItem : DisposableListViewSubItem {
                private readonly PresentationListViewItem m_Parent;
                private readonly EventQueue.PropertyEventDispatcher m_ParticipantsChangedDispatcher;
                private bool m_Disposed;

                public ParticipantsSubItem(PresentationListViewItem parent) : base(parent, null) {
                    this.m_Parent = parent;

                    this.m_ParticipantsChangedDispatcher = new EventQueue.PropertyEventDispatcher(this.m_Parent.m_EventQueue, new PropertyEventHandler(this.HandleParticipantsChanged));
                    this.m_Parent.Presentation.Changed["Participants"].Add(this.m_ParticipantsChangedDispatcher.Dispatcher);
                    this.m_ParticipantsChangedDispatcher.Dispatcher(this.m_Parent.Presentation, null);
                }

                protected override void Dispose(bool disposing) {
                    if(this.m_Disposed) return;
                    if(disposing) {
                        this.m_Parent.Presentation.Changed["Participants"].Remove(this.m_ParticipantsChangedDispatcher.Dispatcher);
                    }
                    this.m_Disposed = true;
                }

                private void HandleParticipantsChanged(object sender_, PropertyEventArgs args) {
                    PresentationModel sender = sender_ as PresentationModel;
                    using(Synchronizer.Lock(sender.SyncRoot)) {
                        if(sender != null)
                            this.Text = sender.Participants.Count.ToString(CultureInfo.CurrentCulture);
                    }
                }
            }


            private class OwnerSubItem : DisposableListViewSubItem {
                private readonly PresentationListViewItem m_Parent;
                private readonly EventQueue.PropertyEventDispatcher m_HumanNameChangedDispatcher;
                private bool m_Disposed;

                public OwnerSubItem(PresentationListViewItem parent) : base(parent, null) {
                    this.m_Parent = parent;
                    this.m_HumanNameChangedDispatcher = new EventQueue.PropertyEventDispatcher(this.m_Parent.m_EventQueue, new PropertyEventHandler(this.HandleHumanNameChanged));
                    this.m_Parent.Presentation.Owner.Changed["HumanName"].Add(this.m_HumanNameChangedDispatcher.Dispatcher);
                    this.m_HumanNameChangedDispatcher.Dispatcher(this, null);
                }

                protected override void Dispose(bool disposing) {
                    if(this.m_Disposed) return;
                    if(disposing) {
                        this.m_Parent.Presentation.Owner.Changed["HumanName"].Remove(this.m_HumanNameChangedDispatcher.Dispatcher);
                    }
                    this.m_Disposed = true;
                }

                private void HandleHumanNameChanged(object sender_, PropertyEventArgs args) {
                    ParticipantModel sender = this.m_Parent.Presentation.Owner;
                    // Lock control first, then lock model second.
                    using(Synchronizer.Lock(this)) {
                        if(sender != null) {
                            using(Synchronizer.Lock(sender.SyncRoot)) {
                                this.Text = sender.HumanName;
                            }
                        }
                    }
                }
            }
        }

        #endregion PresentationListViewItem
    }
}
