// $Id: ClassroomsList.cs 1229 2006-10-06 00:04:52Z fred $

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
    /// <summary>
    /// Displays a list of classrooms available via a given protocol,
    /// and allows the user to connect to a classroom by selecting
    /// one or more of them from the list.
    /// </summary>
    public class ClassroomsListView : ListView {
        private readonly ControlEventQueue m_EventQueue;
        private readonly PresenterModel m_Model;
        private readonly ProtocolCollectionHelper m_ProtocolCollectionHelper;
        private bool m_Disposed;
        private ClassroomModel defaultClassroomModel;
        private DisposableListViewItem defaultClassroom;
        private bool m_IgnoreNextClassroomSelectionChanged = false;

        #region Construction & Destruction

        public ClassroomsListView(PresenterModel model) {
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

            // TODO: Add icons for classrooms.
            // In the mean time, this serves to make the rows big enough to
            // be an easy target for a stylus.
            this.SmallImageList = new ImageList();
            this.SmallImageList.ImageSize = new Size(1, 40);

            // Set the font for list view items
            this.Font = new Font(this.Font.FontFamily, this.Font.Size * 4 / 3 );

            this.Columns.AddRange(new ColumnHeader[] { new ColumnHeader(), new ColumnHeader(), new ColumnHeader()});
            foreach(ColumnHeader column in this.Columns) column.Width = -1;
            this.Columns[0].Text = "Classrooms";
            this.Columns[1].Text = "Participants";
            this.Columns[2].Text = "Protocol";

            // Create a handle immediately so the ListViewItems can marshal event handlers to the creator thread.
            this.CreateHandle();

            // Add a default local classroom to the list
            defaultClassroomModel = new ClassroomModel( null, "Disconnected", ClassroomModelType.None );
            defaultClassroom = new ClassroomListViewItem( this.m_EventQueue, defaultClassroomModel );
            this.Items.Add( defaultClassroom );
            defaultClassroom.Selected = true;


            // Set up a helper to add other classrooms
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
                    if(this.StateImageList != null)
                        this.StateImageList.Dispose();

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

            if (m_IgnoreNextClassroomSelectionChanged) {
                m_IgnoreNextClassroomSelectionChanged = false;
                return;
            }

            // Ignore notifications from items which have not yet been added to the Items collection.
            // This is necessary since the control's handle will already have been created as items are being added.
            if( e.ItemIndex >= this.Items.Count ) 
                return;

            // Select the same item if we tried to select nothing
            if( cancelNextChange == true && e.IsSelected == false ) {
                e.Item.Focused = true;
                e.Item.Selected = true;
                // NOTE: We must already be connected because we were selected before
            } else {
                // Disconnect us or connect us to a different classroom
                ClassroomListViewItem item = ((ClassroomListViewItem)this.Items[e.ItemIndex]);
                bool connect = e.IsSelected;
                // Be careful to avoid a stack overflow by only changing when needed.
                // (Note: item.Classroom is an immutable property.)
                using( Synchronizer.Lock( item.Classroom.SyncRoot ) ) {
                    if( connect != item.Classroom.Connected )
                        item.Classroom.Connected = connect;
                }
            }
        }

        private class ProtocolCollectionHelper : PropertyCollectionHelper {
            private readonly ClassroomsListView m_Parent;

            public ProtocolCollectionHelper(ClassroomsListView parent, NetworkModel network) : base(parent.m_EventQueue, network, "Protocols") {
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
            private readonly ClassroomsListView m_Parent;

            public ClassroomCollectionHelper(ClassroomsListView parent, ProtocolModel protocol) : base(parent.m_EventQueue, protocol, "Classrooms") {
                this.m_Parent = parent;
                base.Initialize();
            }

            protected override void Dispose(bool disposing) {
                try {
                    if (disposing)
                        foreach (ListViewItem item in this.Tags)
                            item.Remove();
                } finally {
                    base.Dispose(disposing);
                }
            }

            protected override object SetUpMember(int index, object member) {
                Debug.Assert(!this.m_Parent.InvokeRequired);

                DisposableListViewItem item = new ClassroomListViewItem(this.m_Parent.m_EventQueue, (ClassroomModel) member);
                this.m_Parent.Items.Add(item);
                return item;
            }

            protected override void TearDownMember(int index, object member, object tag) {
                Debug.Assert(!this.m_Parent.InvokeRequired);
                DisposableListViewItem item = tag as DisposableListViewItem;

                if(item != null) {
                    //We don't want dynamic classrooms selection changed events to run when the classroom is removed from the UI because that 
                    //might cause a connected client to be disconnected.  This hack is considered to be "ok for now" because the new
                    //UI design should make it go away.
                    m_Parent.m_IgnoreNextClassroomSelectionChanged = true;
                    item.Checked = false; //If we are checked when we try to remove a dynamic classroom it can except.
                    item.Remove();
                    item.Dispose();
                }
            }
        }


        class ClassroomListViewItem : DisposableListViewItem {
            private readonly ControlEventQueue m_EventQueue;
            private readonly EventQueue.PropertyEventDispatcher m_HumanNameChangedDispatcher;
            private readonly EventQueue.PropertyEventDispatcher m_ConnectedChangedDispatcher;

            private readonly ParticipantsSubItem m_ParticipantsSubItem;
            private readonly ProtocolSubItem m_ProtocolSubItem;
            private bool m_Disposed;

            private readonly ClassroomModel m_Classroom;

            public ClassroomListViewItem(ControlEventQueue dispatcher, ClassroomModel classroom) {
                if(classroom == null)
                    throw new ArgumentNullException("classroom",
                        "The ClassroomModel associated with this ClassroomListViewItem cannot be null.");

                this.m_EventQueue = dispatcher;
                this.m_Classroom = classroom;
                this.Tag = classroom;

                this.m_HumanNameChangedDispatcher = new EventQueue.PropertyEventDispatcher(this.m_EventQueue, new PropertyEventHandler(this.HandleHumanNameChanged));
                this.m_ConnectedChangedDispatcher = new EventQueue.PropertyEventDispatcher(this.m_EventQueue, new PropertyEventHandler(this.HandleConnectedChanged));
                this.Classroom.Changed["HumanName"].Add(this.m_HumanNameChangedDispatcher.Dispatcher);
                this.Classroom.Changed["Connected"].Add(this.m_ConnectedChangedDispatcher.Dispatcher);

                this.SubItems.Add(this.m_ParticipantsSubItem = new ParticipantsSubItem(this));
                this.SubItems.Add(this.m_ProtocolSubItem = new ProtocolSubItem(this));

                this.HandleConnectedChanged(this.Classroom, null);
                this.HandleHumanNameChanged(this.Classroom, null);
            }

            public ClassroomModel Classroom {
                get { return this.m_Classroom; }
            }


            protected override void Dispose(bool disposing) {
                if (!this.m_Disposed) {
                    if (disposing) {
                        Debug.Assert(this.Classroom != null);
                        this.Classroom.Changed["HumanName"].Remove(this.m_HumanNameChangedDispatcher.Dispatcher);
                        this.Classroom.Changed["Connected"].Remove(this.m_ConnectedChangedDispatcher.Dispatcher);

                        this.m_ParticipantsSubItem.Dispose();
                        this.m_ProtocolSubItem.Dispose();
                    }
                    this.m_Disposed = true;
                }
            }

            private void HandleHumanNameChanged(object sender, PropertyEventArgs args_) {
                Debug.Assert(this.Classroom != null);
                using(Synchronizer.Lock(this.Classroom.SyncRoot)) {
                    this.Text = this.Classroom.HumanName;
                }
            }

            private void HandleConnectedChanged(object sender, PropertyEventArgs args_) {
                Debug.Assert(this.Classroom != null);
                using(Synchronizer.Lock(this.Classroom.SyncRoot)) {
                    this.Checked = this.Classroom.Connected;
                }
            }


            private class ParticipantsSubItem : DisposableListViewSubItem {
                private readonly ClassroomListViewItem m_Parent;
                private readonly EventQueue.PropertyEventDispatcher m_ParticipantsChangedDispatcher;
                private bool m_Disposed;

                public ParticipantsSubItem(ClassroomListViewItem parent) : base(parent, null) {
                    this.m_Parent = parent;

                    Debug.Assert(this.m_Parent.Classroom != null);
                    // Note: the parent's classroom is immutable.
                    this.m_ParticipantsChangedDispatcher = new EventQueue.PropertyEventDispatcher(this.m_Parent.m_EventQueue,
                        new PropertyEventHandler(this.HandleParticipantsChanged));
                    this.m_Parent.Classroom.Changed["Participants"].Add(this.m_ParticipantsChangedDispatcher.Dispatcher);
                    this.m_ParticipantsChangedDispatcher.Dispatcher(this.m_Parent.Classroom, null);
                }

                protected override void Dispose(bool disposing) {
                    if(this.m_Disposed) return;
                    if(disposing) {
                        Debug.Assert(this.m_Parent.Classroom != null);
                        this.m_Parent.Classroom.Changed["Participants"].Remove(this.m_ParticipantsChangedDispatcher.Dispatcher);
                    }
                    this.m_Disposed = true;
                }

                private void HandleParticipantsChanged(object sender_, PropertyEventArgs args) {
                    ClassroomModel sender = sender_ as ClassroomModel;
                    using(Synchronizer.Lock(sender.SyncRoot)) {
                        if(sender != null)
                            this.Text = sender.Participants.Count.ToString(CultureInfo.CurrentCulture);
                    }
                }
            }


            private class ProtocolSubItem : DisposableListViewSubItem {
                private readonly ClassroomListViewItem m_Parent;
                private readonly EventQueue.PropertyEventDispatcher m_HumanNameChangedDispatcher;
                private bool m_Disposed;

                public ProtocolSubItem(ClassroomListViewItem parent) : base(parent, null) {
                    this.m_Parent = parent;

                    Debug.Assert(this.m_Parent.Classroom != null);
                    // Note: the parent's classroom is immutable.
                    this.m_HumanNameChangedDispatcher = new EventQueue.PropertyEventDispatcher(this.m_Parent.m_EventQueue,
                        new PropertyEventHandler(this.HandleHumanNameChanged));
                    if( this.m_Parent.Classroom.Protocol != null )
                        this.m_Parent.Classroom.Protocol.Changed["HumanName"].Add(this.m_HumanNameChangedDispatcher.Dispatcher);
                    this.m_HumanNameChangedDispatcher.Dispatcher(this.m_Parent.Classroom, null);
                }

                protected override void Dispose(bool disposing) {
                    if(this.m_Disposed) return;
                    if(disposing) {
                        Debug.Assert(this.m_Parent.Classroom != null);
                        if( this.m_Parent.Classroom.Protocol != null )
                            this.m_Parent.Classroom.Protocol.Changed["HumanName"].Remove( this.m_HumanNameChangedDispatcher.Dispatcher );
                    }
                    this.m_Disposed = true;
                }

                private void HandleHumanNameChanged(object sender_, PropertyEventArgs args) {
                    ProtocolModel sender = sender_ as ProtocolModel;
                    if(sender != null) {
                        using(Synchronizer.Lock(sender.SyncRoot)) {
                            this.Text = sender.HumanName;
                        }
                    }
                }
            }
        }
    }

    internal abstract class DisposableListViewItem : ListViewItem, IDisposable {
        protected DisposableListViewItem() : base() { }

        ~DisposableListViewItem() {
            this.Dispose(false);
        }

        public void Dispose() {
            this.Dispose(true);
            System.GC.SuppressFinalize(this);
        }

        protected abstract void Dispose(bool disposing);
    }

    internal abstract class DisposableListViewSubItem : ListViewItem.ListViewSubItem, IDisposable {
        protected DisposableListViewSubItem(ListViewItem owner, string text) : base(owner, text) {}

        ~DisposableListViewSubItem() {
            this.Dispose(false);
        }

        public void Dispose() {
            this.Dispose(true);
            System.GC.SuppressFinalize(this);
        }

        protected abstract void Dispose(bool disposing);
    }
}
