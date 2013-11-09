using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Runtime.Serialization;

namespace UW.ClassroomPresenter.Model {
    /// <summary>
    /// Implements an index-based collection to which interested parties can listen
    /// for changes.  In "<code>DEBUG</code>" mode, the collection also enforces that 
    /// callers obtain a <see cref="Synchronizer.Lock">lock</see> on the <see cref="Owner"/>
    /// before invoking any method or accessing any mutable property.
    /// </summary>
    /// <typeparam name="O">
    /// The type of the <see cref="Owner"/>.
    /// </typeparam>
    /// <typeparam name="V">
    /// The type of the collection's members.
    /// </typeparam>
    [Serializable]
    public sealed class Collection<V> : ISynchronizable, ISerializable, IEnumerable<V> {
        private readonly ISynchronizable s;
        private readonly List<V> l;

        private Collection(ISynchronizable sync, List<V> list) {
            Debug.Assert(list != null);
            this.s = sync;
            this.l = list;
        }

        /// <summary>
        /// Creates a new <see cref="Collection"/>.
        /// </summary>
        /// <param name="owner">
        /// The <see cref="Owner"/> which must be <see cref="Synchronizer.Lock">locked</see>
        /// before accessing the collection.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// If <paramref name="owner"/> is <code>null</code>.
        /// </exception>
        public Collection(ISynchronizable sync) : this(sync, new List<V>()) { }

        /// <summary>
        /// Creates a new <see cref="Collection"/>.
        /// </summary>
        /// <param name="owner">
        /// The <see cref="Owner"/> which must be <see cref="Synchronizer.Lock">locked</see>
        /// before accessing the collection.
        /// </param>
        /// <param name="collection">
        /// An enumeration whose members will be copied to the new collection.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// If either <paramref name="owner"/> or <paramref name="collection"/> is <code>null</code>.
        /// </exception>
        public Collection(ISynchronizable sync, IEnumerable<V> collection) : this(sync, new List<V>(collection)) { }

        public object SyncRoot {
            get {
                return this.s != null ? this.s.SyncRoot : this;
            }
        }

        #region ISerializable Members

        private Collection(SerializationInfo info, StreamingContext context)
            : this((ISynchronizable)info.GetValue("s", typeof(ISynchronizable)),
            (List<V>)info.GetValue("l", typeof(V))) { }

        public void GetObjectData(SerializationInfo info, StreamingContext context) {
            info.AddValue("s", this.s);
            info.AddValue("l", this.l);
        }

        #endregion

        /// <summary>
        /// Gets the number of elements in the collection.
        /// </summary>
        public int Count {
            get { return this.l.Count; }
        }

        public V this[int index] {
            get {
                // Assert that a lock is held by the calling thread.
                Synchronizer.AssertLockIsHeld(this.SyncRoot,
                    "A lock must be aquired on the this Collection's SyncRoot"
                    + " before accessing the collection");

                return this.l[index];
            }

            set {
                // Assert that a lock is held by the calling thread.
                Synchronizer.AssertLockIsHeld(this.SyncRoot,
                    "A lock must be aquired on the this Collection's SyncRoot"
                    + " before accessing the collection");

                // Get the current value of the field.
                V old = this.l[index];

                // Fire an event if the values differ (a Changing event is fired before the new value is set).
                bool different = (old != null && !old.Equals(value)) || (old == null && value != null);
                if (different && this.m_Removed != null)
                    this.m_Removed(new RemovedEventArgs(this, index, old));

                // Set the field to the new value.
                this.l[index] = value;

                // Fire an event if the values differ (a Changed event is fired after the new value has been set).
                if (different && this.m_Added != null) {
                    this.m_Added(new AddedEventArgs(this, index, value));
                }
            }
        }

        public void Add(V value) {
            // Assert that a lock is held by the calling thread.
            Synchronizer.AssertLockIsHeld(this.SyncRoot,
                "A lock must be aquired on the this Collection's SyncRoot"
                + " before accessing the collection");

            this.l.Add(value);

            if (this.m_Added != null)
                this.m_Added(new AddedEventArgs(this, this.l.Count - 1, value));
        }

        public void Insert(int index, V item) {
            // Assert that a lock is held by the calling thread.
            Synchronizer.AssertLockIsHeld(this.SyncRoot,
                "A lock must be aquired on the this Collection's SyncRoot"
                + " before accessing the collection");

            this.l.Insert(index, item);

            if (this.m_Added != null)
                this.m_Added(new AddedEventArgs(this, index, item));
        }

        public bool Remove(V value) {
            // Assert that a lock is held by the calling thread.
            Synchronizer.AssertLockIsHeld(this.SyncRoot,
                "A lock must be aquired on the this Collection's SyncRoot"
                + " before accessing the collection");

            int index = this.l.IndexOf(value);
            if (index < 0)
                return false;

            this.RemoveAt(index);

            return true;
        }

        public void RemoveAt(int index) {
            // Assert that a lock is held by the calling thread.
            Synchronizer.AssertLockIsHeld(this.SyncRoot,
                "A lock must be aquired on the this Collection's SyncRoot"
                + " before accessing the collection");

            V value = this.l[index];

            this.l.RemoveAt(index);

            if (this.m_Removed != null)
                this.m_Removed(new RemovedEventArgs(this, index, value));
        }

        public bool Contains(V value) {
            // Assert that a lock is held by the calling thread.
            Synchronizer.AssertLockIsHeld(this.SyncRoot,
                "A lock must be aquired on the this Collection's SyncRoot"
                + " before accessing the collection");

            return this.l.Contains(value);
        }

        public int IndexOf(V value) {
            // Assert that a lock is held by the calling thread.
            Synchronizer.AssertLockIsHeld(this.SyncRoot,
                "A lock must be aquired on the this Collection's SyncRoot"
                + " before accessing the collection");

            return this.l.IndexOf(value);
        }

        public int IndexOf(V value, int index) {
            // Assert that a lock is held by the calling thread.
            Synchronizer.AssertLockIsHeld(this.SyncRoot,
                "A lock must be aquired on the this Collection's SyncRoot"
                + " before accessing the collection");

            return this.l.IndexOf(value, index);
        }

        public int IndexOf(V value, int index, int count) {
            // Assert that a lock is held by the calling thread.
            Synchronizer.AssertLockIsHeld(this.SyncRoot,
                "A lock must be aquired on the this Collection's SyncRoot"
                + " before accessing the collection");

            return this.l.IndexOf(value, index, count);
        }

        public IEnumerator<V> GetEnumerator() {
            foreach (V value in this.l) {
                // Assert that a lock is held by the calling thread.
                Synchronizer.AssertLockIsHeld(this.SyncRoot,
                    "A lock must be aquired on the this Collection's SyncRoot"
                    + " before accessing the collection");

                // Yield the next value to the enumeration.
                yield return value;
            }
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() {
            return this.GetEnumerator();
        }

        /// <summary>
        /// A global lock which protects critical sections while adding and removing
        /// event listeners.  This lock should never be accessed outside of this class,
        /// and no other lock should be acquired inside of the critical section.
        /// This is as opposed to using the "default" event add/remove methods which
        /// lock the Collection instance, but which could therefore be prone to deadlock
        /// by external influence.
        /// </summary>
        private static readonly object c_EventHandlerLock = new object();

        private AddedEventHandler m_Added;
        private RemovedEventHandler m_Removed;

        public event AddedEventHandler Added {
            add {
                using (Synchronizer.Lock(c_EventHandlerLock)) {
                    this.m_Added = (AddedEventHandler)Delegate.Combine(this.m_Added, value);
                }
            }

            remove {
                using (Synchronizer.Lock(c_EventHandlerLock)) {
                    this.m_Added = (AddedEventHandler)Delegate.Remove(this.m_Added, value);
                }
            }
        }

        public event RemovedEventHandler Removed {
            add {
                using (Synchronizer.Lock(c_EventHandlerLock)) {
                    this.m_Removed = (RemovedEventHandler)Delegate.Combine(this.m_Removed, value);
                }
            }

            remove {
                using (Synchronizer.Lock(c_EventHandlerLock)) {
                    this.m_Removed = (RemovedEventHandler)Delegate.Remove(this.m_Removed, value);
                }
            }
        }

        public IDisposable Listen(EventQueue dispatcher, AddedEventHandler handler) {
            // The AddedEventDispatcher constructor adds an event handler to the Added event.
            return new AddedEventDispatcher(this, dispatcher, handler);
        }

        public IDisposable Listen(EventQueue dispatcher, RemovedEventHandler handler) {
            // The RemovedEventDispatcher constructor adds an event handler to the Removed event.
            return new RemovedEventDispatcher(this, dispatcher, handler);
        }

        private class AddedEventDispatcher : IDisposable {
            private readonly Collection<V> Collection;
            private AddedEventHandler Handler;

            public AddedEventDispatcher(Collection<V> collection, EventQueue dispatcher, AddedEventHandler handler) {
                this.Collection = collection;

                this.Handler = dispatcher != null
                    ? delegate(Collection<V>.AddedEventArgs args) { dispatcher.Post(delegate() { handler(args); }); }
                    : handler;

                this.Collection.Added += this.Handler;
            }

            public void Dispose() {
                AddedEventHandler handler = Interlocked.Exchange(ref this.Handler, null);
                this.Collection.Added -= handler;
            }
        }

        private class RemovedEventDispatcher : IDisposable {
            private readonly Collection<V> Collection;
            private RemovedEventHandler Handler;

            public RemovedEventDispatcher(Collection<V> collection, EventQueue dispatcher, RemovedEventHandler handler) {
                this.Collection = collection;

                this.Handler = dispatcher != null
                    ? delegate(Collection<V>.RemovedEventArgs args) { dispatcher.Post(delegate() { handler(args); }); }
                    : handler;

                this.Collection.Removed += this.Handler;
            }

            public void Dispose() {
                RemovedEventHandler handler = Interlocked.Exchange(ref this.Handler, null);
                this.Collection.Removed -= handler;
            }
        }

        public delegate void AddedEventHandler(AddedEventArgs value);
        public delegate void RemovedEventHandler(RemovedEventArgs value);

        public class AddedEventArgs : EventArgs {
            private readonly Collection<V> m_Sender;
            private readonly V m_Added;
            private readonly int m_Index;

            public AddedEventArgs(Collection<V> sender, int index, V added) {
                this.m_Sender = sender;
                this.m_Added = added;
                this.m_Index = index;
            }

            public Collection<V> Sender {
                get { return this.m_Sender; }
            }

            public V Added {
                get { return this.m_Added; }
            }

            public int Index {
                get { return this.m_Index; }
            }
        }

        public class RemovedEventArgs : EventArgs {
            private readonly Collection<V> m_Sender;
            private readonly V m_Removed;
            private readonly int m_Index;

            public RemovedEventArgs(Collection<V> sender, int index, V removed) {
                this.m_Sender = sender;
                this.m_Removed = removed;
                this.m_Index = index;
            }

            public Collection<V> Sender {
                get { return this.m_Sender; }
            }

            public V Removed {
                get { return this.m_Removed; }
            }

            public int Index {
                get { return this.m_Index; }
            }
        }
    }

    [Serializable]
    public delegate Collection<V> CollectionReference<O, V>(O owner);
}
