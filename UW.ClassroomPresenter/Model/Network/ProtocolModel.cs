// $Id: ProtocolModel.cs 1043 2006-07-19 22:48:12Z julenka $

using System;
using UW.ClassroomPresenter.Network;

namespace UW.ClassroomPresenter.Model.Network {
    [Serializable]
    public class ProtocolModel : PropertyPublisher {
        private readonly ClassroomCollection m_Classrooms;
        private readonly ConnectionManager m_Manager;

        // Published properties:
        private string m_HumanName;

        public ProtocolModel(ConnectionManager manager) : this(manager, null) {}

        public ProtocolModel(ConnectionManager manager, string humanName) {
            this.m_Manager = manager;
            this.m_Classrooms = new ClassroomCollection(this, "Classrooms");
            this.m_HumanName = null;
        }

        public ConnectionManager ConnectionManager {
            get { return this.m_Manager; }
        }

        [Published] public string HumanName {
            get { return this.GetPublishedProperty("HumanName", ref this.m_HumanName); }
            set { this.SetPublishedProperty("HumanName", ref this.m_HumanName, value); }
        }

        #region Classrooms

        [Published] public ClassroomCollection Classrooms {
            get { return this.m_Classrooms; }
        }

        public class ClassroomCollection : PropertyCollectionBase {
            internal ClassroomCollection(PropertyPublisher owner, string property) : base(owner, property) {
            }

            public ClassroomModel this[int index] {
                get { return ((ClassroomModel) List[index]); }
                set { List[index] = value; }
            }

            public int Add(ClassroomModel value) {
                return List.Add(value);
            }

            public int IndexOf(ClassroomModel value) {
                return List.IndexOf(value);
            }

            public void Insert(int index, ClassroomModel value) {
                List.Insert(index, value);
            }

            public void Remove(ClassroomModel value) {
                List.Remove(value);
            }

            public bool Contains(ClassroomModel value) {
                return List.Contains(value);
            }

            protected override void OnValidate(Object value) {
                if(!typeof(ClassroomModel).IsInstanceOfType(value))
                    throw new ArgumentException("Value must be of type ClassroomModel.", "value");
            }
        }

        #endregion Classrooms
    }
}
