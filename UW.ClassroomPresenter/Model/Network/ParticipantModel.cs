// $Id: ParticipantModel.cs 1499 2007-11-30 19:58:18Z fred $

using System;
using System.Runtime.Serialization;

using UW.ClassroomPresenter.Network.Groups;

namespace UW.ClassroomPresenter.Model.Network {
    [Serializable]
    public class ParticipantModel : PropertyPublisher {
        private Guid m_Id;

        // Published properties:
        private string m_HumanName; // string
        private readonly GroupCollection m_Groups;
        private RoleModel m_Role; // RoleModel
        
        public ParticipantModel(Guid guid) : this(guid, null, null) {}

        public ParticipantModel(Guid guid, string humanName) : this(guid, null, humanName) {}

        public ParticipantModel(Guid guid, RoleModel role) : this(guid, role, null) {}

        public ParticipantModel(Guid guid, RoleModel role, string humanName) {
            this.m_Id = guid;
            this.m_Role = role;
            this.m_HumanName = humanName;
            this.m_Groups = new GroupCollection(this, "Groups");
            using( Synchronizer.Lock( this.SyncRoot ) ) {
                // Always add all participants to the AllParticipant group
                this.Groups.Add( Group.AllParticipant );

                // All participants also each belong to their own SingletonGroup.
                this.Groups.Add(new SingletonGroup(this));
            }
            if (PresenterModel.TheInstance != null) {
                PresenterModel.TheInstance.VersionExchange.CreateVersionExchange(this);
            }
        }

        public Guid Guid {
            get { return this.m_Id; }
            set { this.m_Id = value; }
        }

        [Published] public string HumanName {
            get { return this.GetPublishedProperty("HumanName", ref this.m_HumanName); }
            set { this.SetPublishedProperty("HumanName", ref this.m_HumanName, value); }
        }

        [Published] public RoleModel Role {
            get { return this.GetPublishedProperty("Role", ref this.m_Role); }
            set { this.SetPublishedProperty("Role", ref this.m_Role, value); }
        }

        #region GroupCollection

        [Published]
        public GroupCollection Groups {
            get { return this.m_Groups; }
        }

        public class GroupCollection : PropertyCollectionBase {
            internal GroupCollection( PropertyPublisher owner, string property )
                : base( owner, property ) {
            }

            public Group this[int index] {
                get { return ((Group)List[index]); }
                set { List[index] = value; }
            }

            public int Add( Group value ) {
                return List.Add( value );
            }

            public int IndexOf( Group value ) {
                return List.IndexOf( value );
            }

            public void Insert( int index, Group value ) {
                List.Insert( index, value );
            }

            public void Remove( Group value ) {
                List.Remove( value );
            }

            public bool Contains( Group value ) {
                return List.Contains( value );
            }

            protected override void OnValidate( Object value ) {
                if( !typeof( Group ).IsInstanceOfType( value ) )
                    throw new ArgumentException( "Value must be of type GroupModel.", "value" );
            }
        }

        #endregion
    }
}
