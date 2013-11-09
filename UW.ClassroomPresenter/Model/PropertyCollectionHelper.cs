// $Id: PropertyCollectionHelper.cs 975 2006-06-28 01:02:59Z pediddle $

using System;
using System.Collections;
using System.Diagnostics;
using System.Reflection;
using System.Threading;

namespace UW.ClassroomPresenter.Model {
    /// <summary>
    /// A utility class for managing members of a collection belong to a <see cref="PropertyPublisher"/> instance.
    /// </summary>
    public abstract class PropertyCollectionHelper : IDisposable {
        private readonly EventQueue m_EventQueue;
        private readonly PropertyPublisher m_Owner;
        private readonly string m_Property;
        private readonly Hashtable m_TagTable;

        private bool m_Initialized;
        private bool m_Disposed;

        private EventQueue.PropertyEventDispatcher m_HandlePropertyChangingDispatch;
        private EventQueue.PropertyEventDispatcher m_HandlePropertyChangedDispatch;


        /// <summary>
        /// Creates a new <see cref="PropertyPublisher"/> instance.
        /// </summary>
        /// <remarks>
        /// Subclasses must call <see cref="Initialize"/> in order to register event listeners and
        /// set up existing members of the collection.
        /// </remarks>
        /// <param name="owner">The <see cref="PropertyPublisher"/> to whom the collection belongs; see <see cref="Owner"/>.</param>
        /// <param name="property">The name of the published property of the <see cref="Owner"/> which returns the <see cref="Collection"/>.</param>
        public PropertyCollectionHelper(EventQueue dispatcher, PropertyPublisher owner, string property) {
            this.m_EventQueue = dispatcher;
            this.m_Owner = owner;
            this.m_Property = property;
            this.m_TagTable = Hashtable.Synchronized(new Hashtable());
        }

        ~PropertyCollectionHelper() {
            this.Dispose(false);
        }

        /// <summary>
        /// Implements <see cref="IDisposable.Dispose"/>.
        /// <seealso cref="Dispose(bool)"/>.
        /// </summary>
        public void Dispose() {
            this.Dispose(true);
            System.GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Releases any resources used by the <see cref="PropertyCollectionHelper"/>
        /// </summary>
        /// <remarks>
        /// Subclasses should override this method if resources allocated by <see cref="SetUpMember"/>
        /// need to be released.  The <see cref="Tags"/> collection might be useful for doing this.
        /// </remarks>
        protected virtual void Dispose(bool disposing) {
            if(disposing) {
                using(Synchronizer.Lock(this.m_Owner.SyncRoot)) {
                    // There's nothing to do if we haven't been initialized yet or if we've already been disposed.
                    if(!this.m_Initialized || this.m_Disposed)

                    // Tear down event listeners.
                    // FIXME: Don't remove the listeners if RegisterEventListeners has not been executed.
                    this.m_Owner.Changing[this.Property].Remove(this.m_HandlePropertyChangingDispatch.Dispatcher);
                    this.m_Owner.Changed[this.Property].Remove(this.m_HandlePropertyChangedDispatch.Dispatcher);

                    this.m_Disposed = true;
                }
            }
        }

        /// <summary>
        /// Subclasses must call this method in their constructor in order to set up event listeners
        /// and to invoke <see cref="SetUpMember"/> on each of the members already present in the
        /// collection when the <see cref="PropertyCollectionHelper"/> is initialized.
        /// </summary>
        /// <remarks>
        /// This method is not called by the base class constructor, in order to allow subclasses to perform
        /// any necessary initialization in their own constructors before calls to <see cref="SetUpMember"/> are made.
        /// </remarks>
        /// <exception cref="InvalidOperationException">
        /// If <see cref="Initialize"/> is called more than once.
        /// </exception>
        protected void Initialize() {
            // A lock is necessary in order to read the items from this.Collection,
            // and also to synchronize on the PropertyCollectionHelper instead of lock(this).
            // We can't use lock(this) because the lock ordering cannot be guaranteed.
            using (Synchronizer.Lock(this.m_Owner.SyncRoot)) {
                if (this.m_Initialized)
                    throw new InvalidOperationException("The PropertyCollectionHelper has already been initialized.");
                this.m_Initialized = true;

                // Set up event listeners.
                this.m_HandlePropertyChangingDispatch = new EventQueue.PropertyEventDispatcher(this.m_EventQueue, new PropertyEventHandler(this.HandlePropertyChanging));
                this.m_HandlePropertyChangedDispatch = new EventQueue.PropertyEventDispatcher(this.m_EventQueue, new PropertyEventHandler(this.HandlePropertyChanged));

                // Call HandlePropertyChanged to set up each existing member in the collection.
                object collection = this.Collection;
                PropertyEventArgs args = new PropertyChangeEventArgs(this.Property, collection, collection);
                this.m_HandlePropertyChangedDispatch.Dispatcher(this.m_Owner, args);

                // Register the event listeners after setting up members so no member can get set up twice.
                // (Not really a ThreadStart, but we just need a no-args delegate type.)
                this.m_EventQueue.Post(delegate() {
                    using (Synchronizer.Lock(this.m_Owner.SyncRoot)) {
                        if (!this.m_Disposed) {
                            this.m_Owner.Changing[this.Property].Add(this.m_HandlePropertyChangingDispatch.Dispatcher);
                            this.m_Owner.Changed[this.Property].Add(this.m_HandlePropertyChangedDispatch.Dispatcher);
                        }
                    }
                });
            }
        }

        /// <summary>
        /// This method is invoked whenever a new member is added to the collection,
        /// or when <see cref="InitializeMembers"/> is called.
        /// </summary>
        /// <remarks>
        /// Subclasses must override this method to implement the main functionality
        /// of a <see cref="PropertyCollectionHelper"/> instance.
        /// <para>
        /// If the implementation needs to allocate any long-lasting resources, a reference to said resources
        /// should be returned as a "tag".  The return value of <see cref="SetUpMember"/> will be passed
        /// as the <c>tag</c> parameter to <see cref="TearDownMember"/>.
        /// </para>
        /// </remarks>
        /// <param name="index">The index of the new member, or <c>-1</c> if the index is unknown.</param>
        /// <param name="member">The member itself.</param>
        /// <returns>A tag which is passed to <see cref="TearDownMember"/> when the member is later removed.</returns>
        protected abstract object SetUpMember(int index, object member);

        /// <summary>
        /// This method is invoked whenever an member is removed from the collection.
        /// </summary>
        /// <remarks>
        /// Subclasses must override this method to implement the main functionality
        /// of a <see cref="PropertyCollectionHelper"/> instance.
        /// <para>
        /// Note that <see cref="TearDownMember"/> is <em>not</em> invoked when the <see cref="PropertyCollectionHelper"/>
        /// is <see cref="Dispose(bool)">disposed</see>.  Subclasses must override <see cref="Dispose(bool)"/>
        /// if resources allocated by <see cref="SetUpMember"/> need to be released.
        /// </para>
        /// </remarks>
        /// <param name="index">The index of the member being removed, or <c>-1</c> if it is unknown.</param>
        /// <param name="member">The member being removed.</param>
        /// <param name="tag">The tag returned by <see cref="SetUpMember"/> when the member was first added.</param>
        protected abstract void TearDownMember(int index, object member, object tag);

        /// <summary>
        /// The name of the collection property of the <see cref="Owner"/>.
        /// </summary>
        /// <remarks>
        /// Used by <see cref="Collection"/> to obtain the underlying collection via reflection.
        /// </remarks>
        public string Property {
            get { return this.m_Property; }
        }

        /// <summary>
        /// The <see cref="PropertyPublisher"/> instance to which this <see cref="PropertyCollectionHelper"/> is listening.
        /// </summary>
        public PropertyPublisher Owner {
            get { return this.m_Owner; }
        }

        /// <summary>
        /// The set of all tags returned by <see cref="SetUpMember"/> (including <c>null</c> values).
        /// </summary>
        protected ICollection Tags {
            get { return this.m_TagTable.Values; }
        }

        /// <summary>
        /// Uses reflection to obtain the underlying collection named by <see cref="Property"/> of the <see cref="Owner"/>.
        /// </summary>
        private ICollection Collection {
            get {
                Type t = this.m_Owner.GetType();

                return ((ICollection) (t.InvokeMember(
                    this.Property,
                    BindingFlags.Public | BindingFlags.NonPublic
                    | BindingFlags.Instance | BindingFlags.GetProperty,
                    null,
                    this.Owner,
                    null)));
            }
        }

        /// <summary>
        /// Called by the <see cref="EventQueue"/> when the underlying collection is about to change.
        /// </summary>
        protected virtual void HandlePropertyChanging(object sender, PropertyEventArgs args_) {
            PropertyCollectionEventArgs args = args_ as PropertyCollectionEventArgs;

            if((args == null) || (args.Removed == null && args.Added == null)) {
                // Behave as if each member were removed from the collection.
                using(Synchronizer.Lock(this.m_Owner.SyncRoot)) {
                    foreach(object member in this.Collection)
                        this.m_HandlePropertyChangedDispatch.Dispatcher(sender,
                            new PropertyCollectionEventArgs(this.Property, -1, member, null));
                }
            }
        }

        /// <summary>
        /// Called by the <see cref="EventQueue"/> when the underlying collection has changed.
        /// </summary>
        protected virtual void HandlePropertyChanged(object sender, PropertyEventArgs args_) {
            PropertyCollectionEventArgs args = args_ as PropertyCollectionEventArgs;

            if((args == null) || (args.Removed == null && args.Added == null)) {
                // Behave as if each member were added in sequence.
                using(Synchronizer.Lock(this.m_Owner.SyncRoot)) {
                    int index = 0;
                    foreach(object member in this.Collection)
                        this.m_HandlePropertyChangedDispatch.Dispatcher(sender,
                            new PropertyCollectionEventArgs(this.Property, index++, null, member));
                }
            }

            else {
                if(args.Removed != null) {
                    this.TearDownMember(args.Index, args.Removed, this.m_TagTable[args.Removed]);
                    this.m_TagTable.Remove(args.Removed);
                }

                if(args.Added != null) {
                    this.m_TagTable[args.Added]
                        = this.SetUpMember(args.Index, args.Added);
                }
            }
        }
    }
}
