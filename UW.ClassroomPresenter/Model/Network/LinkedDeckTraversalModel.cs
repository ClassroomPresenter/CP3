// $Id: LinkedDeckTraversalModel.cs 1880 2009-06-08 19:46:27Z jing $

using System;
using System.Diagnostics;
using System.Threading;

using UW.ClassroomPresenter.Model;
using UW.ClassroomPresenter.Model.Presentation;

namespace UW.ClassroomPresenter.Model.Network {
    /// <summary>
    /// Manages the link between an instructor's DeckTraversalModel and the student's own
    /// notion of the "current slide", and allows the student to select between "linked" and
    /// "unlinked" navigation modes.
    /// </summary>
    /// <remarks>
    /// Two separate DeckTraversalModels are referenced internally.  One is the instructor's
    /// DeckTraversalModel (the "linked" model); the other is a SlideDeckTraversalModel visible
    /// only to the LinkedDeckTraversalModel (the "unlinked" model).
    /// Which of those is the "current" model is chosen by choosing the "linked" or "unlinked"
    /// mode.
    /// </remarks>
    [Serializable]
    public class LinkedDeckTraversalModel : DeckTraversalModel, IDisposable {
        private readonly EventQueue m_EventQueue;
        private readonly EventQueue.PropertyEventDispatcher m_CurrentChangedDispatcher;
        private readonly EventQueue.PropertyEventDispatcher m_NextChangedDispatcher;
        private readonly EventQueue.PropertyEventDispatcher m_PreviousChangedDispatcher;
        private readonly EventQueue.PropertyEventDispatcher m_AbsoluteCurrentSlideIndexChangedDispatcher;
        private readonly EventQueue.PropertyEventDispatcher m_NetworkAssociationChangedDispatcher;
        private readonly EventQueue.PropertyEventDispatcher m_NetworkAssociationRoleChangedDispatcher;
        private readonly EventQueue.PropertyEventDispatcher m_ForcingStudentNavigationLockChangedDispatcher;

        private readonly PresenterModel m_Model;
        private readonly DeckTraversalModel m_Linked;
        private readonly DeckTraversalModel m_Unlinked;
        private DeckTraversalModel m_Active;
        private bool m_Disposed;

        // Published properties:
        private DeckTraversalSelector m_Mode;
        private TableOfContentsModel.Entry m_Current;
        private TableOfContentsModel.Entry m_Next;
        private TableOfContentsModel.Entry m_Previous;
        private int m_AbsoluteCurrentSlideIndex;

        private ParticipantModel m_NetworkAssociation;
        private InstructorModel m_NetworkAssociationRole;
        private bool m_ForcingStudentNavigationLock;

        public LinkedDeckTraversalModel(EventQueue dispatcher, Guid id, PresenterModel model, DeckTraversalModel linked)
            : base(id, linked.Deck) {
            this.m_EventQueue = dispatcher;
            this.m_Model = model;
            this.m_Linked = linked;
            // TODO: Evaluate whether we need to support other types of DeckTraversalModels.
            this.m_Unlinked = new SlideDeckTraversalModel(Guid.NewGuid(), linked.Deck);

            this.m_CurrentChangedDispatcher = new EventQueue.PropertyEventDispatcher(this.m_EventQueue, new PropertyEventHandler(this.HandleCurrentChanged));
            this.m_NextChangedDispatcher = new EventQueue.PropertyEventDispatcher(this.m_EventQueue, new PropertyEventHandler(this.HandleNextChanged));
            this.m_PreviousChangedDispatcher = new EventQueue.PropertyEventDispatcher(this.m_EventQueue, new PropertyEventHandler(this.HandlePreviousChanged));
            this.m_AbsoluteCurrentSlideIndexChangedDispatcher = new EventQueue.PropertyEventDispatcher(this.m_EventQueue, new PropertyEventHandler(this.HandleAbsoluteCurrentSlideIndexChanged));

            // Set this.m_Active and register event listeners via UpdateMode.
            this.m_Mode = DeckTraversalSelector.Linked;
            this.UpdateMode(DeckTraversalSelector.Linked);

            // Since UpdateMode doesn't initialize the event listeners like the Mode setter does, we must do this.
            this.m_CurrentChangedDispatcher.Dispatcher(this, null);
            this.m_NextChangedDispatcher.Dispatcher(this, null);
            this.m_PreviousChangedDispatcher.Dispatcher(this, null);
            this.m_AbsoluteCurrentSlideIndexChangedDispatcher.Dispatcher(this, null);

            // Watch for changes to the current network association.
            // When we're associated with an Instructor, we must obey its ForcingStudentNavigationLock policy.
            this.m_NetworkAssociationChangedDispatcher = new EventQueue.PropertyEventDispatcher(this.m_EventQueue, new PropertyEventHandler(this.HandleNetworkAssociationChanged));
            this.m_NetworkAssociationRoleChangedDispatcher = new EventQueue.PropertyEventDispatcher(this.m_EventQueue, new PropertyEventHandler(this.HandleNetworkAssociationRoleChanged));
            this.m_ForcingStudentNavigationLockChangedDispatcher = new EventQueue.PropertyEventDispatcher(this.m_EventQueue, new PropertyEventHandler(this.HandleForcingStudentNavigationLockChanged));
            this.m_Model.Network.Changed["Association"].Add(this.m_NetworkAssociationChangedDispatcher.Dispatcher);
            this.m_NetworkAssociationChangedDispatcher.Dispatcher(this, null);
        }

        public DeckTraversalModel LinkedModel {
            get { return this.m_Linked; }
        }

        protected override void Dispose(bool disposing) {
            if (this.m_Disposed) return;
            try {
                if (disposing) {
                    this.UnregisterActiveEventListeners();
                    this.SetNetworkAssociation(null);
                }
            } finally {
                base.Dispose(disposing);
            }
            this.m_Disposed = true;
        }

        private void RegisterActiveEventListeners() {
            this.m_Active.Changed["Current"].Add(this.m_CurrentChangedDispatcher.Dispatcher);
            this.m_Active.Changed["Previous"].Add(this.m_PreviousChangedDispatcher.Dispatcher);
            this.m_Active.Changed["Next"].Add(this.m_NextChangedDispatcher.Dispatcher);
            this.m_Active.Changed["AbsoluteCurrentSlideIndex"].Add(this.m_AbsoluteCurrentSlideIndexChangedDispatcher.Dispatcher);
        }

        private void UnregisterActiveEventListeners() {
            this.m_Active.Changed["Current"].Remove(this.m_CurrentChangedDispatcher.Dispatcher);
            this.m_Active.Changed["Previous"].Remove(this.m_PreviousChangedDispatcher.Dispatcher);
            this.m_Active.Changed["Next"].Remove(this.m_NextChangedDispatcher.Dispatcher);
            this.m_Active.Changed["AbsoluteCurrentSlideIndex"].Remove(this.m_AbsoluteCurrentSlideIndexChangedDispatcher.Dispatcher);
        }

        [Published]
        public DeckTraversalSelector Mode {
            get { return this.GetPublishedProperty("Mode", ref this.m_Mode); }
            set {
                if (((DeckTraversalSelector)this.m_Mode) == value) return;
                // Ignore requests to change the mode when 
                if (this.m_ForcingStudentNavigationLock && value != DeckTraversalSelector.Linked) return;

                this.UpdateMode(value);

                this.SetPublishedProperty("Mode", ref this.m_Mode, value);

                this.m_CurrentChangedDispatcher.Dispatcher(this, null);
                this.m_NextChangedDispatcher.Dispatcher(this, null);
                this.m_PreviousChangedDispatcher.Dispatcher(this, null);
                this.m_AbsoluteCurrentSlideIndexChangedDispatcher.Dispatcher(this, null);
            }
        }

        [Published]
        public override TableOfContentsModel.Entry Current {
            get { return this.GetPublishedProperty("Current", ref this.m_Current); }
            set {
                Synchronizer.AssertLockIsHeld(this.SyncRoot);

                if (this.Mode == DeckTraversalSelector.Unlinked) {
                    Debug.Assert(Object.ReferenceEquals(this.m_Active, this.m_Unlinked));

                    using (Synchronizer.Lock(this.m_Unlinked.SyncRoot)) {
                        this.m_Unlinked.Current = value;
                    }
                }
            }
        }

        [Published]
        public override TableOfContentsModel.Entry Next {
            get { return this.GetPublishedProperty("Next", ref this.m_Next); }
        }

        [Published]
        public override TableOfContentsModel.Entry Previous {
            get { return this.GetPublishedProperty("Previous", ref this.m_Previous); }
        }

        [Published]
        public override int AbsoluteCurrentSlideIndex {
            get { return this.GetPublishedProperty("AbsoluteCurrentSlideIndex", ref this.m_AbsoluteCurrentSlideIndex); }
        }

        private void UpdateMode(DeckTraversalSelector value) {
            if (this.m_Active != null) {
                this.UnregisterActiveEventListeners();
            }

            switch (value) {
                case DeckTraversalSelector.Linked:
                    this.m_Active = this.m_Linked;
                    break;
                case DeckTraversalSelector.Unlinked:
                    this.m_Active = this.m_Unlinked;
                    break;
                default:
                    throw new ArgumentException("Unknown enumeration value for DeckTraversalSelector: " + value.ToString());
            }

            this.RegisterActiveEventListeners();
        }

        private void HandleCurrentChanged(object sender, PropertyEventArgs args) {
            using (Synchronizer.Lock(this.SyncRoot)) {
                using (Synchronizer.Lock(this.m_Active.SyncRoot)) {
                    this.SetPublishedProperty("Current", ref this.m_Current, this.m_Active.Current);

                    // Also set the unlinked traversal's current slide so when the student 
                    // unlinks he won't immediately jump to the wrong slide.  Note that if 
                    // the student unlinks, changes slides, and then links and unlinks again
                    // before the instructor also changes slides, the student *will* jump
                    // back to the slide he went to while unlinked.  I think this behavior
                    // is desirable.
                    if (this.m_Active == this.m_Linked) {
                        using (Synchronizer.Lock(this.m_Unlinked.SyncRoot)) {
                            this.m_Unlinked.Current = this.m_Active.Current;
                        }
                    }
                }
            }
        }

        private void HandleNextChanged(object sender, PropertyEventArgs args) {
            using (Synchronizer.Lock(this.SyncRoot)) {
                using (Synchronizer.Lock(this.m_Active.SyncRoot)) {
                    this.SetPublishedProperty("Next", ref this.m_Next,
                        (this.Mode != DeckTraversalSelector.Linked) ? this.m_Active.Next : null);
                }
            }
        }

        private void HandlePreviousChanged(object sender, PropertyEventArgs args) {
            using (Synchronizer.Lock(this.SyncRoot)) {
                using (Synchronizer.Lock(this.m_Active.SyncRoot)) {
                    this.SetPublishedProperty("Previous", ref this.m_Previous,
                        (this.Mode != DeckTraversalSelector.Linked) ? this.m_Active.Previous : null);
                }
            }
        }

        private void HandleAbsoluteCurrentSlideIndexChanged(object sender, PropertyEventArgs args) {
            using (Synchronizer.Lock(this.SyncRoot)) {
                using (Synchronizer.Lock(this.m_Active.SyncRoot)) {
                    this.SetPublishedProperty("AbsoluteCurrentSlideIndex", ref this.m_AbsoluteCurrentSlideIndex, this.m_Active.AbsoluteCurrentSlideIndex);
                }
            }
        }

        private void HandleNetworkAssociationChanged(object sender, PropertyEventArgs args) {
            using (Synchronizer.Lock(this.m_Model.Network.SyncRoot))
                this.SetNetworkAssociation(this.m_Model.Network.Association);
        }

        private void SetNetworkAssociation(ParticipantModel participant) {
            // Remove any old event listeners.
            if (this.m_NetworkAssociation != null) {
                this.m_NetworkAssociation.Changed["Role"].Remove(this.m_NetworkAssociationRoleChangedDispatcher.Dispatcher);
            }

            this.m_NetworkAssociation = participant;

            // Watch for changes to the participant's Role, so we can know if/when we're associated with an Instructor.
            if (this.m_NetworkAssociation != null) {
                this.m_NetworkAssociation.Changed["Role"].Add(this.m_NetworkAssociationRoleChangedDispatcher.Dispatcher);
                this.m_NetworkAssociationRoleChangedDispatcher.Dispatcher(this, null);
            } else {
                this.SetNetworkAssociationRole(null);
            }
        }

        private void HandleNetworkAssociationRoleChanged(object sender, PropertyEventArgs args) {
            if (this.m_NetworkAssociation != null)
                using (Synchronizer.Lock(this.m_NetworkAssociation.SyncRoot))
                    this.SetNetworkAssociationRole(this.m_NetworkAssociation.Role);
            else this.SetNetworkAssociationRole(null);
        }

        private void SetNetworkAssociationRole(RoleModel role) {
            // Remove any old event listeners.
            if (this.m_NetworkAssociationRole != null) {
                this.m_NetworkAssociationRole.Changed["ForcingStudentNavigationLock"].Remove(this.m_ForcingStudentNavigationLockChangedDispatcher.Dispatcher);
            }

            this.m_NetworkAssociationRole = role as InstructorModel;

            // If we're now associated with an instructor, we must obey its ForcingStudentNavigationLock policy.
            // So we register event listeners to watch for changes to that property.
            if (this.m_NetworkAssociationRole != null) {
                this.m_NetworkAssociationRole.Changed["ForcingStudentNavigationLock"].Add(this.m_ForcingStudentNavigationLockChangedDispatcher.Dispatcher);
                this.m_ForcingStudentNavigationLockChangedDispatcher.Dispatcher(this, null);
            }
        }

        private void HandleForcingStudentNavigationLockChanged(object sender, PropertyEventArgs args) {
            if (this.m_NetworkAssociationRole != null) {
                using (Synchronizer.Lock(this.m_NetworkAssociationRole.SyncRoot))
                    this.m_ForcingStudentNavigationLock = this.m_NetworkAssociationRole.ForcingStudentNavigationLock;
            } else {
                this.m_ForcingStudentNavigationLock = false;
            }

            if (this.m_ForcingStudentNavigationLock)
                using (Synchronizer.Lock(this.SyncRoot))
                    this.Mode = DeckTraversalSelector.Linked;
        }

        public enum DeckTraversalSelector {
            Linked = 0,
            Unlinked,
            Unknown,
        }

        public enum NavigationSelector {
            Full = 0,
            Visited,
            None,
            Unknown,
        }
    }
}
