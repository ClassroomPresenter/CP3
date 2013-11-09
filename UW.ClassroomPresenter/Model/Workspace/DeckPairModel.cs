// $Id: DeckPairModel.cs 775 2005-09-21 20:42:02Z pediddle $

using System;

using UW.ClassroomPresenter.Model;
using UW.ClassroomPresenter.Model.Presentation;

namespace UW.ClassroomPresenter.Model.Workspace {

    /// <summary>
    /// Summary description for DeckPairModel.
    /// </summary>
    public class DeckPairModel : PropertyPublisher {
        public bool SameDeckTraversal {
            get { return (this.m_Local.Id == this.m_Remote.Id); }
        }

        private readonly DeckTraversalModel m_Local;
        public DeckTraversalModel LocalDeckTraversal {
            get { return this.m_Local; }
        }

        private readonly DeckTraversalModel m_Remote;
        public DeckTraversalModel RemoteDeckTraversal {
            get { return this.m_Remote; }
        }

        /// <summary>
        /// Constructor for the DeckPairModel object
        /// </summary>
        /// <param name="local">The local version of the deck</param>
        /// <param name="remote">The remote version of the deck</param>
        public DeckPairModel( DeckTraversalModel local, DeckTraversalModel remote ) {
            this.m_Local = local;
            this.m_Remote = remote;
        }

        // Implement equality of objects
        public override bool Equals(object obj) {
            if( obj is DeckPairModel && obj != null ) {
                if( (((DeckPairModel)obj).m_Local.Id == this.m_Local.Id) &&
                    (((DeckPairModel)obj).m_Remote.Id == this.m_Remote.Id) )
                    return true;
                else
                    return false;
            }
            else
                return false;
        }

        public override int GetHashCode() {
            int result = 0;
            if( m_Local != null )
                result += m_Local.Id.GetHashCode();
            if( m_Remote != null )
                result += m_Remote.Id.GetHashCode();
            return result;
        }
    }

    /// <summary>
    /// A Collection of DeckPair objects
    /// </summary>
    public class DeckPairCollection : PropertyPublisher.PropertyCollectionBase {
        internal DeckPairCollection(PropertyPublisher owner, string property) : base(owner, property) {
        }

        public DeckPairModel this[int index] {
            get { return ((DeckPairModel) List[index]); }
            set { List[index] = value; }
        }

        public int Add(DeckPairModel value) {
            return List.Add(value);
        }

        public int IndexOf(DeckPairModel value) {
            return List.IndexOf(value);
        }

        public void Insert(int index, DeckPairModel value) {
            List.Insert(index, value);
        }

        public void Remove(DeckPairModel value) {
            List.Remove(value);
        }

        public bool Contains(DeckPairModel value) {
            return List.Contains(value);
        }

        protected override void OnValidate(Object value) {
            if(!typeof(DeckPairModel).IsInstanceOfType(value))
                throw new ArgumentException("Value must be of type DeckPairModel.", "value");
        }
    }
}
