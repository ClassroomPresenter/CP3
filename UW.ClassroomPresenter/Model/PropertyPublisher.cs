// $Id: PropertyPublisher.cs 943 2006-04-11 05:19:35Z pediddle $

using System;
using System.Collections;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
using System.Runtime.Serialization;

namespace UW.ClassroomPresenter.Model {
    /// <summary>
    /// A base class to ease implementation of objects that publish many properties and events.
    /// <para>
    /// Properties with the <code>[Published]</code> attribute are automatically published.
    /// That is to say, it will be possible for interested parties to subscribe delegates
    /// to listen for events tagged with the name of the property.
    /// </para>
    /// <para>
    /// A <see cref="PropertyPublisher"/> also enforces locking of its properties
    /// using <see cref="Synchronizer"/>.
    /// A client cannot read or write the value of a published property (or at least one whose getter
    /// or setter is implemented with <see cref="GetPublishedProperty"/> or <see cref="SetPublishedProperty"/>)
    /// without first aquiring a lock from the <see cref="Lock"><c>Synchronizer</c></see> instance associated with each
    /// <see cref="PropertyPublisher"/>.
    /// See the documentation for <see cref="GetPublishedProperty"/> and <see cref="SetPublishedProperty"/>
    /// for examples.
    /// </para>
    /// </summary>
    /// <example>
    /// Consider a subclass of <c>PropertyPublisher</c> with a published property <c>MyValueProperty</c>:
    /// <code>
    /// class MyPropertyPublisher : PropertyPublisher {
    ///     private MyType m_MyValueProperty;
    ///
    ///     [Published] MyType MyValueProperty {
    ///         get { this.GetPublishedProperty("MyValueProperty", ref this.m_MyValueProperty); }
    ///         set { this.SetPublishedProperty("MyValueProperty", ref this.m_MyValueProperty, value); }
    ///     }
    /// }
    /// </code>
    /// <para>
    /// Given an instance of <c>MyPropertyPublisher</c> <code>foo</code>, one could subscribe to listen for changes
    /// to the value of <code>foo.MyValueProperty</code> with:
    /// <code>
    /// foo.Changed["MyValueProperty"].Add(new PropertyEventHandler(...));
    /// </code>
    /// </para>
    /// <para>
    /// One can also subscribe to listen to changes <i>before</i> they take effect, with:
    /// <code>
    /// foo.Changing["MyValueProperty"].Remove(new PropertyEventHandler(...));
    /// </code>
    /// </para>
    /// </example>
    /// <remarks>
    /// It is intended that events will be fired whenever the value of a published property changes.
    /// If the property is a collection, events should also be fired whenever items are removed,
    /// added, or changed.  <seealso cref="PropertyCollectionBase"/>
    /// </remarks>
    [Serializable]
    public abstract class PropertyPublisher : IDeserializationCallback {
        [NonSerialized] private EventTable m_ChangingTable;
        [NonSerialized] private EventTable m_ChangedTable;
        [NonSerialized] private bool m_Initialized;
        [NonSerialized] private object m_SyncRoot;
        private readonly PropertyPublisher m_Parent;

        /// <summary>
        /// Creates a new PropertyPublisher object, publishing all properties
        /// of this type that have the <c>[Published]</c> attribute.
        /// <seealso cref="GetType"/> <seealso cref="Type.GetProperties"/>
        /// </summary>
        public PropertyPublisher() : this(null) {}

        /// <summary>
        /// Creates a new PropertyPublisher object, publishing all properties
        /// of this type that have the <c>[Published]</c> attribute.
        /// The new PropertyPublisher shares the same event tables and
        /// <see cref="RWL">ReaderWriterLock</see> as the specified parent.
        /// <seealso cref="GetType"/> <seealso cref="Type.GetProperties"/>
        /// </summary>
        protected PropertyPublisher(PropertyPublisher parent) {
            this.m_Parent = parent;
            this.Initialize();
        }

        void IDeserializationCallback.OnDeserialization(Object sender) {
            this.Initialize();
        }

        private void Initialize() {
            using(Synchronizer.Lock(this)) {
                if(this.m_Initialized)
                    throw new SerializationException("A PropertyPublisher may only be initialized once.");
                this.m_Initialized = true;
            }

            if(this.m_Parent == null) {
                this.m_SyncRoot = this;
                this.m_ChangingTable = new EventTable();
                this.m_ChangedTable = new EventTable();

                foreach(PropertyInfo property in this.GetType().GetProperties()) {
                    object[] attributes = property.GetCustomAttributes(typeof(PublishedAttribute), true);
                    if(attributes.Length >= 1) {
                        PublishedAttribute published = ((PublishedAttribute) attributes[0]);
                        string name = published.Property != null ? published.Property : property.Name;
                        this.m_ChangingTable.Publish(name);
                        this.m_ChangedTable.Publish(name);
                    }
                }
            } else {
                this.m_SyncRoot = this.m_Parent.m_SyncRoot;
                this.m_ChangedTable = this.m_Parent.Changed;
                this.m_ChangingTable = this.m_Parent.Changing;

                foreach(PropertyInfo property in this.GetType().GetProperties()) {
                    object[] attributes = property.GetCustomAttributes(typeof(PublishedAttribute), true);
                    if(attributes.Length >= 1) {
                        PublishedAttribute published = ((PublishedAttribute) attributes[0]);
                        string name = published.Property != null ? published.Property : property.Name;
                        // Getting the table entry will throw an exception if the event is not published by the parent.
                        object foo1 = this.m_Parent.Changing[name];
                        object foo2 = this.m_Parent.Changed[name];
                    }
                }
            }
        }

        /// <summary>
        /// Manages a set of events published by the enclosing <see cref="PropertyPublisher"/>.
        /// </summary>
        public sealed class EventTable {
            private readonly Hashtable m_DelegatesTable = new Hashtable();

            /// <summary>
            /// Gets or sets the PropertyEventHandler delegate associated with the specified tag.
            /// </summary>
            /// <remarks>
            /// The <paramref name="tag"/> is an opaque identifier associated with each property.
            /// In the default implementation of <see cref="PropertyPublisher"/>, the tag is the unqualified
            /// <i>name</i> of the published property (a <c>string</c>).
            /// </remarks>
            public PropertyEventHandlerWrapper this[string property] {
                get {
                    if(!this.m_DelegatesTable.ContainsKey(property)) {
                        throw new ArgumentException("No published event named \"" + property + "\"", "property");
                    } else {
                        return ((PropertyEventHandlerWrapper) this.m_DelegatesTable[property]);
                    }
                }
            }

            /// <summary>
            /// Allows others to subscribe to events with the specified tag.
            /// </summary>
            /// <remarks>
            /// This should only be used by the <see cref="PropertyPublisher"/> constructor.
            /// </remarks>
            internal void Publish(string property) {
                this.m_DelegatesTable[property] = new PropertyEventHandlerWrapper();
            }

            /// <summary>
            /// Allows clients to add and remove delegates to and from the invocation list
            /// of the enclosing <see cref="EventTable"/>'s events.
            /// </summary>
            /// <remarks>
            /// This class mainly serves to ensure proper multithreaded access to the event table.
            /// It would not be acceptable to allow others to set the event table's entries directly
            /// without requiring all clients to <c>lock</c> on the table &mdash; an error-prone practice.
            /// <para>
            /// Recall that the standard C# <c>event</c> declaration provides special <c>+=</c> and <c>-=</c>
            /// syntax for default <c>add</c> and <c>remove</c> methods which handle locking themselves.
            /// Since we are not using <c>event</c> declarations, <see cref="Add"/> and <see cref="Remove"/>
            /// methods assume this functionality, without the benefit of special syntax.
            /// </para>
            /// </remarks>
            public sealed class PropertyEventHandlerWrapper {
                private PropertyEventHandler m_Delegate;

                /// <summary>
                /// A MulticastDelegateWrapper should only be instantiated by the enclosing
                /// <see cref="EventTable"/>.
                /// </summary>
                internal PropertyEventHandlerWrapper() {}

                /// <summary>
                /// Removes the specified delegate from the invocation list.
                /// </summary>
                public void Add(PropertyEventHandler value) {
                    using(Synchronizer.Lock(this)) {
                        this.m_Delegate = ((PropertyEventHandler) PropertyEventHandler.Combine(this.m_Delegate, value));
                    }
                }

                /// <summary>
                /// Adds the specified delegate to the invocation list.
                /// </summary>
                public void Remove(PropertyEventHandler value) {
                    using(Synchronizer.Lock(this)) {
                        this.m_Delegate = ((PropertyEventHandler) PropertyEventHandler.Remove(this.m_Delegate, value));
                    }
                }

                /// <summary>
                /// Allows the enclosing PropertyPublisher to invoke the wrapped delegate.
                /// This property should not be used by other classes.
                /// <!-- Why can't C# have proper inner classes like Java so we could make this <c>protected</c>? -->
                /// </summary>
                public void Invoke(object sender, PropertyEventArgs args) {
                    PropertyEventHandler handler;
                    using(Synchronizer.Lock(this)) {
                        handler = this.m_Delegate;
                    }

                    if(handler != null)
                        handler(sender, args);
                }
            }
        }

        /// <summary>
        /// Gets the set of events for published properties which are fired whenever the value of
        /// a published property is about to change.
        /// </summary>
        /// <remarks>
        /// Clients should register event listeners with this <c>Changing</c> table when they
        /// need to read the state of a <see cref="PropertyPublisher"/> before its values change,
        /// but only when they cannot rely on <see cref="PropertyChangeEventArgs.OldValue"/> or
        /// <see cref="PropertyCollectionEventArgs.Removed"/> to retrieve the old value at the
        /// time of the event.  Otherwise, clients should register their event listeners with
        /// <see cref="Changed"/> instead.
        /// </remarks>
        public EventTable Changing {
            get { return this.m_ChangingTable; }
        }

        /// <summary>
        /// Gets set of events for published properties which are fired whenever the value of
        /// a published property has changed (after the change is committed).
        /// </summary>
        public EventTable Changed {
            get { return this.m_ChangedTable; }
        }

        /// <summary>
        /// A utility method allowing implementors to quickly set the value of a field,
        /// firing a <c>PropertyChangeEvent</c> if the value changes and enforcing locking
        /// with the <see cref="RWL"/>.
        /// </summary>
        /// <remarks>
        /// Whether the new field value equals the previous value is determined by the <c>==</c> operator.
        /// No event is fired if the new value equals the old value.
        /// <!-- This is intended to eliminate feedback loops from UI controls which update themselves
        /// in response to a property change, causing another event which causes them to set the property
        /// again.  This "double-setting" can't always be easily avoided, so we must "break the cycle"
        /// somewhere. -->
        /// </remarks>
        /// <example>
        /// This method is intended to be used in the <c>set</c> method of a property, as follows:
        /// <code>
        /// private MyType m_MyProperty;
        /// [Published] public MyType MyProperty {
        ///     get { return this.GetPublishedProperty("MyProperty", ref this.m_MyProperty); }
        ///     set { this.SetPublishedProperty("MyProperty", ref this.m_MyProperty, value); }
        /// }
        /// </code>
        /// </example>
        /// <example>
        /// Callers must obtain a lock from the <see cref="Synchronizer"/> of the
        /// <see cref="PropertyPublisher"/> instance whose property they are setting.
        /// (If the application is compiled with <see cref="Debug.Assert"/> enabled,
        /// an assertion error will occur if a lock is not held by the caller.)
        /// If <c>publisher</c> is a <see cref="PropertyPublisher"/> instance with a published
        /// property "<see cref="MyProperty"/>", then aquiring the lock should always be done as follows:
        /// <code>
        /// using(publisher.<see cref="Lock"/>.<see cref="Synchronizer.Lock">Lock</see>()) {
        ///        publisher.MyProperty = newValue;
        /// }
        /// </code>
        /// </example>
        ///
        /// <exception cref="ArgumentException">
        /// <paramref name="tag"/> does not identify a published property of this
        /// <c>PropertyPublisher</c> instance.  <seealso cref="EventTable"/>
        /// </exception>
        protected void SetPublishedProperty<P>(string property, ref P field, P newValue) {
            // Assert that a lock is held by the calling thread.
            Synchronizer.AssertLockIsHeld(this.SyncRoot, "A lock must be aquired on the this PropertyPublisher before setting any published property.");

            // Get the current value of the field.
            object oldValue = field;

            PropertyEventArgs args = null;
            // Fire an event if the values differ (a Changing event is fired before the new value is set).
            if((oldValue != null && !oldValue.Equals(newValue)) || (oldValue == null && newValue != null)) {
                args = new PropertyChangeEventArgs(property, oldValue, newValue);
                this.Changing[property].Invoke(this, args);
            }

            // Set the field to the new value.
            field = newValue;

            // Fire an event if the values differ (a Changed event is fired after the new value has been set).
            if(args != null) {
                this.Changed[property].Invoke(this, args);
            }
        }

        /// <summary>
        /// A utility method allowing implementors to quickly get the value of a field,
        /// enforcing locking with the <see cref="RWL"/>.
        /// </summary>
        /// </summary>
        /// <example>
        /// Callers must aquire a lock from the <see cref="Lock"/> of the
        /// <see cref="PropertyPublisher"/> instance whose property they are getting.
        /// (If the application is compiled with <see cref="Debug.Assert"/> enabled,
        /// an assertion error will occur if a lock is not held by the caller.)
        /// If <c>publisher</c> is a <see cref="PropertyPublisher"/> instance with a published
        /// property "<see cref="MyProperty"/>", then aquiring the lock should be done as follows:
        /// <code>
        /// using(publisher.<see cref="Lock"/>.<see cref="Synchronizer.Lock">Lock</see>() {
        ///        publisher.MyProperty = newValue;
        /// }
        /// </code>
        /// <exception cref="ArgumentException">
        /// <paramref name="tag"/> does not identify a published property of this
        /// <c>PropertyPublisher</c> instance.  <seealso cref="EventTable"/>
        /// </exception>
        [DebuggerStepThrough]
        protected P GetPublishedProperty<P>(object tag, ref P field) {
            // Assert that an appropriate lock is held by the calling thread.
            Synchronizer.AssertLockIsHeld(this.SyncRoot, "A lock must be aquired on the this PropertyPublisher before getting the value of any published property.");

            return field;
        }

        /// <summary>
        /// Gets the object on which users must lock before accessing published properties.
        /// </summary>
        public object SyncRoot {
            [DebuggerStepThrough]
            get { return this.m_SyncRoot; }
        }

        /// <summary>
        /// Used by subclasses of <c>PropertyPublisher</c> to automatically publish properties.
        /// Properties with this attribute will be automatically published.
        /// </summary>
        [AttributeUsage(AttributeTargets.Property, AllowMultiple=false, Inherited=true)]
        public class PublishedAttribute : Attribute {
            private readonly string m_Property;

            public PublishedAttribute() : this(null) {}

            public PublishedAttribute(string property) {
                this.m_Property = property;
            }

            public string Property {
                get { return this.m_Property; }
            }
        }

        /// <summary>
        /// Used by subclasses of <c>PropertyPublisher</c> to automatically flag properties to be saved.
        /// Properties with this attribute will be automatically saved.
        /// </summary>
        [AttributeUsage(AttributeTargets.Property, AllowMultiple=false, Inherited=true)]
        public class SavedAttribute : Attribute {
            private readonly string m_Property;

            public SavedAttribute() : this(null) {}

            public SavedAttribute( string property ) {
                this.m_Property = property;
            }

            public string Property {
                get { return this.m_Property; }
            }
        }

        /// <summary>
        /// A utility class for easy notification of inserts, removes, and sets of a collection property.
        /// </summary>
        [Serializable]
        public abstract class PropertyCollectionBase : CollectionBase {
            private readonly PropertyPublisher m_Owner;
            private readonly string m_Property;

            // FIXME!: Enforce reader locks for all read operations.
            //   This might require reimplementing CollectionBase.

            public PropertyCollectionBase(PropertyPublisher owner, string property) {
                this.m_Property = property;
                this.m_Owner = owner;
            }

            protected override void OnClear() {
                Synchronizer.AssertLockIsHeld(this.m_Owner.SyncRoot, "A lock must be aquired on the parent PropertyPublisher before modifying the collection.");

                base.OnClear();
                if(this.m_Owner != null) {
                    this.m_Owner.Changing[this.m_Property].Invoke(this.m_Owner, new PropertyChangeEventArgs(this.m_Property, this, this));
                }
            }

            protected override void OnClearComplete() {
                Synchronizer.AssertLockIsHeld(this.m_Owner.SyncRoot, "A lock must be aquired on the parent PropertyPublisher before modifying the collection.");

                base.OnClearComplete();
                if(this.m_Owner != null) {
                    this.m_Owner.Changed[this.m_Property].Invoke(this.m_Owner, new PropertyChangeEventArgs(this.m_Property, this, this));
                }
            }

            protected override void OnInsert(int index, Object value) {
                Synchronizer.AssertLockIsHeld(this.m_Owner.SyncRoot, "A lock must be aquired on the parent PropertyPublisher before modifying the collection.");

                base.OnInsert(index, value);
                if(this.m_Owner != null) {
                    this.m_Owner.Changing[this.m_Property].Invoke(this.m_Owner, new PropertyCollectionEventArgs(this.m_Property, index, null, value));
                }
            }

            protected override void OnInsertComplete(int index, Object value) {
                Synchronizer.AssertLockIsHeld(this.m_Owner.SyncRoot, "A lock must be aquired on the parent PropertyPublisher before modifying the collection.");

                base.OnInsertComplete(index, value);
                if(this.m_Owner != null) {
                    this.m_Owner.Changed[this.m_Property].Invoke(this.m_Owner, new PropertyCollectionEventArgs(this.m_Property, index, null, value));
                }
            }

            protected override void OnRemove(int index, Object value) {
                Synchronizer.AssertLockIsHeld(this.m_Owner.SyncRoot, "A lock must be aquired on the parent PropertyPublisher before modifying the collection.");

                base.OnRemove(index, value);
                if(this.m_Owner != null) {
                    this.m_Owner.Changing[this.m_Property].Invoke(this.m_Owner, new PropertyCollectionEventArgs(this.m_Property, index, value, null));
                }
            }

            protected override void OnRemoveComplete(int index, Object value) {
                Synchronizer.AssertLockIsHeld(this.m_Owner.SyncRoot, "A lock must be aquired on the parent PropertyPublisher before modifying the collection.");

                base.OnRemoveComplete(index, value);
                if(this.m_Owner != null) {
                    this.m_Owner.Changed[this.m_Property].Invoke(this.m_Owner, new PropertyCollectionEventArgs(this.m_Property, index, value, null));
                }
            }

            protected override void OnSet(int index, Object oldValue, Object newValue) {
                Synchronizer.AssertLockIsHeld(this.m_Owner.SyncRoot, "A lock must be aquired on the parent PropertyPublisher before modifying the collection.");

                base.OnSet(index, oldValue, newValue);
                if(oldValue != newValue && this.m_Owner != null) {
                    this.m_Owner.Changing[this.m_Property].Invoke(this.m_Owner, new PropertyCollectionEventArgs(this.m_Property, index, oldValue, newValue));
                }
            }

            protected override void OnSetComplete(int index, Object oldValue, Object newValue) {
                Synchronizer.AssertLockIsHeld(this.m_Owner.SyncRoot, "A lock must be aquired on the parent PropertyPublisher before modifying the collection.");

                base.OnSetComplete(index, oldValue, newValue);
                if(oldValue != newValue && this.m_Owner != null) {
                    this.m_Owner.Changed[this.m_Property].Invoke(this.m_Owner, new PropertyCollectionEventArgs(this.m_Property, index, oldValue, newValue));
                }
            }
        }
    }

    /// <summary>
    /// The delegate type used by <see cref="PropertyPublisher.Changing"/> and <see cref="PropertyPublisher.Changed"/>.
    /// </summary>
    public delegate void PropertyEventHandler(object sender, PropertyEventArgs args);

    /// <summary>
    /// The base class for arguments to events fired before and after a published property's value changes.
    /// </summary>
    /// <seealso cref="PropertyPublisher.Changing"/>
    /// <seealso cref="PropertyPublisher.Changed"/>
    /// <seealso cref="PropertyChangeEventArgs"/>
    /// <seealso cref="PropertyCollectionEventArgs"/>
    public abstract class PropertyEventArgs : EventArgs {
        private readonly string m_Property;

        protected PropertyEventArgs(string property) {
            this.m_Property = property;
        }

        /// <summary>
        /// The name of the changed property.
        /// </summary>
        public string Property {
            get { return this.m_Property; }
        }
    }

    /// <summary>
    /// An instance of <see cref="PropertyChangeEventArgs"/> is passed to <see cref="PropertyEventHandler"/>
    /// delegates whenever the <i>value</i> of a published property changes.
    /// </summary>
    /// <remarks>
    /// Note: An instance of <see cref="PropertyChangeEventArgs"/> may even be passed to delegates
    /// listening to <i>collection</i> properties, for example if the collection is cleared or set
    /// to an entirely new collection.  Therefore, it is not safe to assume that a collection property
    /// will only pass <see cref="PropertyCollectionEventArgs"/>.
    /// </remarks>
    public class PropertyChangeEventArgs : PropertyEventArgs {
        private readonly object m_OldValue;
        private readonly object m_NewValue;

        /// <summary>
        /// Instantiates a new <see cref="PropertyChangeEventArgs"/>.
        /// </summary>
        public PropertyChangeEventArgs(string property, object oldValue, object newValue) : base(property) {
            this.m_OldValue = oldValue;
            this.m_NewValue = newValue;
        }

        /// <summary>
        /// Gets the old value of the property.
        /// </summary>
        public object OldValue {
            get { return this.m_OldValue; }
        }

        /// <summary>
        /// Gets the new value of the property.
        /// </summary>
        public object NewValue {
            get { return this.m_NewValue; }
        }
    }

    /// <summary>
    /// An instance of <see cref="PropertyChangeEventArgs"/> is passed to <see cref="PropertyEventHandler"/>
    /// delegates whenever a member of a <i>collection</i> property is added, removed, or changed.
    /// </summary>
    /// <remarks>
    /// Note: An instance of <see cref="PropertyCollectionEventArgs"/> might <i>not</i> be passed to delegates
    /// listening to <i>collection</i> properties, for example if the collection is cleared or set
    /// to an entirely new collection.  Therefore, it is not safe to assume that a collection property
    /// will only pass <see cref="PropertyCollectionEventArgs"/>.
    /// </remarks>
    public class PropertyCollectionEventArgs : PropertyEventArgs {
        private readonly int m_Index;
        private readonly object m_Removed;
        private readonly object m_Added;

        /// <summary>
        /// Instantiates a new <see cref="PropertyCollectionEventArgs"/>.
        /// </summary>
        public PropertyCollectionEventArgs(string property, int index, object removed, object added) : base(property) {
            this.m_Index = index;
            this.m_Removed = removed;
            this.m_Added = added;
        }

        /// <summary>
        /// Gets the index of the collection member which changed, or returns <c>-1</c> if the index is unknown.
        /// </summary>
        public int Index {
            get { return this.m_Index; }
        }

        /// <summary>
        /// Gets the member which was removed from the collection, or <c>null</c> if nothing was removed.
        /// </summary>
        /// <remarks>
        /// When a member is <i>changed</i> (ie., via the <c>Set</c> method of a collection), its old will
        /// be returned by <see cref="Removed"/> and its new value by <see cref="Added"/>.
        /// </remarks>
        /// <remarks>
        /// No provisions are made to distinguish a <c>null</c> member being removed from a collection
        /// which supports <c>null</c> entries.
        /// </remarks>
        public object Removed {
            get { return this.m_Removed; }
        }

        /// <summary>
        /// Gets the member which was added from the collection, or <c>null</c> if nothing was added.
        /// </summary>
        /// <remarks>
        /// When a member is <i>changed</i> (ie., via the <c>Set</c> method of a collection), its old will
        /// be returned by <see cref="Removed"/> and its new value by <see cref="Added"/>.
        /// </remarks>
        /// <remarks>
        /// No provisions are made to distinguish a <c>null</c> member being added to a collection
        /// which supports <c>null</c> entries.
        /// </remarks>
        public object Added {
            get { return this.m_Added; }
        }
    }
}
