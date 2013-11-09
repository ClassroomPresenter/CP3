using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Threading;

namespace UW.ClassroomPresenter.Model {
    [Serializable]
    public class Property<V> : ISynchronizable, ISerializable {
        private readonly ISynchronizable s;
        private V v;

        public Property() : this(null, default(V)) { }

        public Property(V value) : this(null, value) { }

        public Property(ISynchronizable sync) : this(sync, default(V)) { }

        public Property(ISynchronizable sync, V value) {
            this.s = sync;
            this.v = value;
        }

        public object SyncRoot {
            get {
                return this.s != null ? this.s.SyncRoot : this;
            }
        }

        #region ISerializable Members

        private Property(SerializationInfo info, StreamingContext context)
            : this((ISynchronizable)info.GetValue("s", typeof(ISynchronizable)),
            (V)info.GetValue("v", typeof(V))) { }

        /// <summary>
        /// Serializes the parent object and current value of this Property.
        /// </summary>
        /// <remarks>
        /// Does <emph>not</emph> serialize event listeners or other transient fields of the property.
        /// </remarks>
        /// <param name="info"></param>
        /// <param name="context"></param>
        void ISerializable.GetObjectData(SerializationInfo info, StreamingContext context) {
            info.AddValue("s", this.s);
            info.AddValue("v", this.v);
        }

        #endregion

        public static implicit operator V(Property<V> p) {
            return p.Value;
        }

        public static V operator ~(Property<V> p) {
            return p.Value;
        }

        public V Value {
            get {
                // Assert that a lock is held by the calling thread.
                Synchronizer.AssertLockIsHeld(this.SyncRoot,
                    "A synchronizerlock must be aquired on the this Property's SyncRoot"
                    + " before accessing the property's value.");

                return this.v;
            }

            set {
                // Assert that a lock is held by the calling thread.
                Synchronizer.AssertLockIsHeld(this.SyncRoot,
                    "A synchronizer lock must be aquired on the this Property's SyncRoot"
                    + " before accessing the property's value.");

                // Get the current value of the field.
                V old = this.v;

                // Set the field to the new value.
                this.v = value;

                // Fire an event if the values differ (a Changed event is fired after the new value has been set).
                if (this.c != null &&
                    ((old != null && !old.Equals(value)) || (old == null && value != null))) {
                    this.c(new EventArgs(this, old, value));
                }
            }
        }

        /// <summary>
        /// The "Changed" event handler.
        /// </summary>
        private EventHandler c;

        /// <summary>
        /// A global lock which protects critical sections while adding and removing
        /// event listeners.  This lock should never be accessed outside of this class,
        /// and no other lock should be acquired inside of the critical section.
        /// This is as opposed to using the "default" event add/remove methods which
        /// lock the Property instance, but which could therefore be prone to deadlock
        /// by external influence.
        /// </summary>
        private static readonly object c_EventHandlerLock = new object();

        private event EventHandler Changed {
            add {
                using (Synchronizer.Lock(c_EventHandlerLock)) {
                    this.c = (EventHandler)Delegate.Combine(this.c, value);
                }
            }

            remove {
                using (Synchronizer.Lock(c_EventHandlerLock)) {
                    this.c = (EventHandler)Delegate.Remove(this.c, value);
                }
            }
        }

        public IDisposable Listen(EventQueue dispatcher, EventHandler handler) {
            // The EventDispatcher constructor adds an event handler to the Changed event.
            return new EventDispatcher(this, dispatcher, handler);
        }

        public IDisposable ListenAndInitialize(EventQueue dispatcher, EventHandler handler) {
            // The EventDispatcher constructor adds an event handler to the Changed event.
            EventDispatcher wrapper = new EventDispatcher(this, dispatcher, handler);
            wrapper.Fire();
            return wrapper;
        }

        private class EventDispatcher : IDisposable {
            private readonly Property<V> Property;
            private readonly EventQueue Dispatcher;
            private EventHandler Handler;

            public EventDispatcher(Property<V> property, EventQueue dispatcher, EventHandler handler) {
                this.Property = property;
                this.Dispatcher = dispatcher;
                this.Handler = dispatcher != null
                    ? delegate(Property<V>.EventArgs args) { dispatcher.Post(delegate() { handler(args); }); }
                    : handler;

                this.Property.Changed += this.Handler;
            }

            public void Fire() {
                if (this.Dispatcher != null)
                    this.Dispatcher.Post(delegate() {
                        using (Synchronizer.Lock(this.Property.SyncRoot))
                            this.Handler(new EventArgs(this.Property, default(V), this.Property.Value));
                    });
                else using (Synchronizer.Lock(this.Property.SyncRoot))
                        this.Handler(new EventArgs(this.Property, default(V), this.Property.Value));
            }

            public void Dispose() {
                EventHandler handler = Interlocked.Exchange(ref this.Handler, null);
                this.Property.Changed -= handler;
            }
        }

        public delegate void EventHandler(EventArgs value);

        /// <summary>
        /// The base class for arguments to events fired before and after a published property's value changes.
        /// </summary>
        /// <seealso cref="PropertyPublisher.Changed"/>
        public class EventArgs : System.EventArgs {
            private readonly Property<V> m_Sender;
            private readonly V m_Old;
            private readonly V m_New;

            public EventArgs(Property<V> sender, V old, V value) {
                this.m_Sender = sender;
                this.m_Old = old;
                this.m_New = value;
            }

            public Property<V> Sender {
                get { return this.m_Sender; }
            }

            public V Old {
                get { return this.m_Old; }
            }

            public V New {
                get { return this.m_New; }
            }
        }
    }

    [Serializable]
    public delegate Property<V> PropertyReference<O, V>(O owner);
}
