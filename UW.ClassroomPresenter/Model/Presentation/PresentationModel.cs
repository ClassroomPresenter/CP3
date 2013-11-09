// $Id: PresentationModel.cs 1776 2008-09-24 00:41:05Z anderson $

using System;

using UW.ClassroomPresenter.Model.Network;

namespace UW.ClassroomPresenter.Model.Presentation {
    /// <summary>
    /// Represents all the objects that are part of an instructor's presentation, this includes all the 
    /// deck traversals (DeckTraversalModel) and Participants (ParticipantModel), as well as any 
    /// Quick Poll (QuickPollModel).
    /// </summary>
    [Serializable]
    public class PresentationModel : PropertyPublisher {
        public static PresentationModel CurrentPresentation = null;

        #region Private Members

        private readonly Guid m_Id;
        private readonly ParticipantModel m_Owner;
        private readonly DeckTraversalCollection m_DeckTraversals;
        private readonly ParticipantCollection m_Participants;

        /// <summary>
        /// The friendly name for this Presentation
        /// </summary>
        private string m_HumanName;

        private bool m_IsUntitledPresentation;

        //Natalie
        //public ClassroomModel m_Classroom;

        #endregion

        #region Constructors

        /// <summary>
        /// Constructor for the PresentationModel
        /// </summary>
        /// <param name="id">The unique identifier for this presentation</param>
        /// <param name="owner">The ParticipantModel of the owner of this presentation</param>
        public PresentationModel(Guid id, ParticipantModel owner) : this(id, owner, null,true) {}

        /// <summary>
        /// Constructor for the PresentationModel
        /// </summary>
        /// <param name="id">The unique identifier for this presentation</param>
        /// <param name="owner">The ParticipantModel of the owner of this presentation</param>
        /// <param name="humanName">The friendly name for this presentation</param>
        public PresentationModel(Guid id, ParticipantModel owner, string humanName):this (id,owner,humanName,false){ }

        public PresentationModel(Guid id, ParticipantModel owner, string humanName, bool isUntitledPresentation) {
            this.m_Id = id;
            this.m_DeckTraversals = new DeckTraversalCollection(this, "DeckTraversals");
            this.m_Participants = new ParticipantCollection(this, "Participants");
            this.m_Owner = owner;
            this.m_HumanName = humanName;
            this.m_QuickPoll = null;
            this.m_IsUntitledPresentation = isUntitledPresentation;
            CurrentPresentation = this;
        }

        #endregion

        #region Utility Functions

        /// <summary>
        /// Retreives the student submission deck
        /// </summary>
        /// <returns>The current student submission deck if it exists, null otherwise</returns>
        public DeckModel GetStudentSubmissionDeck() {
            using( Synchronizer.Lock( this.SyncRoot ) ) {
                // Iterate through all the decks looking for the deck that is of StudentSubmission
                // disposition and is stored as the current submission deck
                foreach( DeckTraversalModel deck in this.DeckTraversals ) {
                    using( Synchronizer.Lock( deck.Deck.SyncRoot ) )
                        if( ((deck.Deck.Disposition & DeckDisposition.StudentSubmission) != 0) && deck.Deck.current_subs )
                            return deck.Deck;
                }
            }
            return null;
        }

        /// <summary>
        /// Retreives the student submission deck
        /// </summary>
        /// <returns>The current student submission deck if it exists, null otherwise</returns>
        public DeckModel GetPublicSubmissionDeck() {
            using (Synchronizer.Lock(this.SyncRoot)) {
                // Iterate through all the decks looking for the deck that is of StudentSubmission
                // disposition and is stored as the current submission deck
                foreach (DeckTraversalModel deck in this.DeckTraversals) {
                    using (Synchronizer.Lock(deck.Deck.SyncRoot))
                        if ((deck.Deck.Disposition & DeckDisposition.PublicSubmission) != 0)
                            return deck.Deck;
                }
            }
            return null;
        }


        /// <summary>
        /// Retreives the quick poll deck
        /// </summary>
        /// <returns>The current quick poll deck if it exists, null otherwise</returns>
        public DeckModel GetQuickPollDeck() {
            using( Synchronizer.Lock( this.SyncRoot ) ) {
                // Iterate through all the decks looking for the deck that is of QuickPoll
                // disposition and is stored as the current poll deck
                foreach( DeckTraversalModel deck in this.DeckTraversals ) {
                    using( Synchronizer.Lock( deck.Deck.SyncRoot ) )
                        if( ((deck.Deck.Disposition & DeckDisposition.QuickPoll) != 0) && deck.Deck.current_poll )
                            return deck.Deck;
                }
            }
            return null;
        }

        #endregion

        #region Id

        /// <summary>
        /// Guid to uniquely represent this PresentationModel
        /// </summary>
        public Guid Id {
            get { return this.m_Id; }
        }

        #endregion

        #region HumanName

        /// <summary>
        /// Public property publisher for the Human Name
        /// </summary>
        [Published]
        public string HumanName {
            get { return this.GetPublishedProperty("HumanName", ref this.m_HumanName); }
            set { this.SetPublishedProperty("HumanName", ref this.m_HumanName, value); }
        }

        #endregion

        #region IsUntitledPresentation

        public bool IsUntitledPresentation
        {
            get { return this.m_IsUntitledPresentation; }
            set { this.m_IsUntitledPresentation = value; }
        }

        #endregion

        #region DeckTraversals

        /// <summary>
        /// Public property publisher for the collection of decks
        /// </summary>
        [Published] public DeckTraversalCollection DeckTraversals {
            get { return this.m_DeckTraversals; }
        }

        /// <summary>
        /// This class represents a collection of DeckTraversal objects and exports and event whenever
        /// something is added or removed from it.
        /// </summary>
        [Serializable]
        public class DeckTraversalCollection : PropertyCollectionBase {
            /// <summary>
            /// Internal constructor
            /// </summary>
            /// <param name="owner">The object which the events are published from</param>
            /// <param name="property">The string given to the published events</param>
            internal DeckTraversalCollection(PropertyPublisher owner, string property) : base(owner, property) {
            }

            /// <summary>
            /// Indexer
            /// </summary>
            /// <param name="index">The index into the array</param>
            /// <returns>The DeckTraversalModel at this index</returns>
            public DeckTraversalModel this[int index] {
                get { return ((DeckTraversalModel) List[index]); }
                set { List[index] = value; }
            }

            /// <summary>
            /// Add a new DeckTraversalModel to the collection
            /// </summary>
            /// <param name="value">The DeckTraversalModel to add</param>
            /// <returns>Returns the index at which the object was added</returns>
            public int Add(DeckTraversalModel value) {
                return List.Add(value);
            }

            /// <summary>
            /// Returns the index of the given object
            /// </summary>
            /// <param name="value">The DeckTraversalModel to find the index of</param>
            /// <returns>The index of the object, or -1 otherwise</returns>
            public int IndexOf(DeckTraversalModel value) {
                return List.IndexOf(value);
            }

            /// <summary>
            /// Inserts a DeckTraversalModel into the given index in the collection
            /// </summary>
            /// <param name="index">The index to insert into</param>
            /// <param name="value">The DeckTraversalModel to insert</param>
            public void Insert(int index, DeckTraversalModel value) {
                List.Insert(index, value);
            }

            /// <summary>
            /// Remove the given DeckTraversalModel
            /// </summary>
            /// <param name="value">The DeckTraversalModel to remove</param>
            public void Remove(DeckTraversalModel value) {
                List.Remove(value);
            }

            /// <summary>
            /// Check to see if the given DeckTraversalModel is contained in the collection
            /// </summary>
            /// <param name="value">The DeckTraversalModel to check for</param>
            /// <returns>Returns true if the collection contains the 
            /// DeckTraversalModel, false otherwise</returns>
            public bool Contains(DeckTraversalModel value) {
                return List.Contains(value);
            }

            /// <summary>
            /// Overrides a function that validates that an object is of the type that this collection 
            /// represents, in this case a DeckTraversalModel.
            /// </summary>
            /// <param name="value">The object to validate</param>
            protected override void OnValidate(Object value) {
                if(!typeof(DeckTraversalModel).IsInstanceOfType(value))
                    throw new ArgumentException("Value must be of type DeckTraversalModel.", "value");
            }
        }

        #endregion DeckTraversals

        #region QuickPolling

        /// <summary>
        /// One and Only One Quick Poll at a time for a Presentation
        /// </summary>
        [NonSerialized]
        private QuickPollModel m_QuickPoll;

        /// <summary>
        /// Public property publisher for the Quick Poll 
        /// </summary>
        [Published] public QuickPollModel QuickPoll {
            get { return this.GetPublishedProperty("QuickPoll", ref this.m_QuickPoll); }
            set { this.SetPublishedProperty("QuickPoll", ref this.m_QuickPoll, value); }
        }

        #endregion

        #region Participants

        /// <summary>
        /// Get the ParticipantModel of the owner of this Presentation
        /// </summary>
        public ParticipantModel Owner {
            get { return this.m_Owner; }
        }

        /// <summary>
        /// Public property publisher for the collection of participants who are part of this presentation
        /// </summary>
        [Published] public ParticipantCollection Participants {
            get { return this.m_Participants; }
        }


        /// <summary>
        /// This class represents a collection of ParticipantModel objects and exports any event whenever
        /// something is added or removed from it.
        /// </summary>
        [Serializable]
        public class ParticipantCollection : PropertyCollectionBase {
            /// <summary>
            /// Internal constructor
            /// </summary>
            /// <param name="owner">The object which the events are published from</param>
            /// <param name="property">The string given to the published events</param>
            internal ParticipantCollection( PropertyPublisher owner, string property )
                : base( owner, property ) {
            }

            /// <summary>
            /// Indexer
            /// </summary>
            /// <param name="index">The index into the array</param>
            /// <returns>The ParticipantModel at this index</returns>
            public ParticipantModel this[int index] {
                get { return ((ParticipantModel) List[index]); }
                set { List[index] = value; }
            }

            /// <summary>
            /// Add a new ParticipantModel to the collection
            /// </summary>
            /// <param name="value">The ParticipantModel to add</param>
            /// <returns>Returns the index at which the object was added</returns>
            public int Add( ParticipantModel value ) {
                return List.Add(value);
            }

            /// <summary>
            /// Returns the index of the given object
            /// </summary>
            /// <param name="value">The ParticipantModel to find the index of</param>
            /// <returns>The index of the object, or -1 otherwise</returns>
            public int IndexOf( ParticipantModel value ) {
                return List.IndexOf(value);
            }

            /// <summary>
            /// Inserts a ParticipantModel into the given index in the collection
            /// </summary>
            /// <param name="index">The index to insert into</param>
            /// <param name="value">The ParticipantModel to insert</param>
            public void Insert( int index, ParticipantModel value ) {
                List.Insert(index, value);
            }

            /// <summary>
            /// Remove the given ParticipantModel
            /// </summary>
            /// <param name="value">The ParticipantModel to remove</param>
            public void Remove( ParticipantModel value ) {
                List.Remove(value);
            }

            /// <summary>
            /// Check to see if the given ParticipantModel is contained in the collection
            /// </summary>
            /// <param name="value">The ParticipantModel to check for</param>
            /// <returns>Returns true if the collection contains the ParticipantModel, false otherwise</returns>
            public bool Contains( ParticipantModel value ) {
                return List.Contains(value);
            }

            /// <summary>
            /// Overrides a function that validates that an object is of the type that this collection 
            /// represents, in this case a ParticipantModel.
            /// </summary>
            /// <param name="value">The object to validate</param>
            protected override void OnValidate( Object value ) {
                if(!typeof(ParticipantModel).IsInstanceOfType(value))
                    throw new ArgumentException("Value must be of type ParticipantModel.", "value");
            }
        }

        #endregion Participants
    }
}
