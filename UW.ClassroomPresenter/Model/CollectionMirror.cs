using System;
using System.Collections.Generic;
using System.Threading;

namespace UW.ClassroomPresenter.Model {

    /// <summary>
    /// Listens for changes to a <see cref="Collection"/> and provides a framework
    /// by which inheriting classes can easily react to those changes.
    /// Changes are serialized with a <see cref="EventQueue"/>.
    /// </summary>
    /// <typeparam name="V">
    /// Type of the <see cref="Collection"/>'s members.
    /// </typeparam>
    /// <typeparam name="T">
    /// Type of the tags returned by <see cref="SetUp"/>.  If <see cref="SetUp"/>
    /// does not need to return any tags, then it is acceptable to set
    /// <typeparamref name="T"/> to "<see cref="IDisposable"/>" and make
    /// <see cref="SetUp"/> set the tags to <code>null</code>.
    /// <see cref="Disposable"/> instances may also be used to conveniently
    /// implement tags that do not themselves implement <see cref="IDisposable"/>.
    /// </typeparam>
    public abstract class CollectionMirror<V, T> : IDisposable
        where T : IDisposable {

        private readonly EventQueue q;
        private readonly Collection<V> c;
        private readonly Dictionary<V, T> t;

        private IDisposable a;
        private IDisposable r;

        private bool m_Initialized;
        private volatile bool m_Disposed;

        /// <summary>
        /// Creates a new <see cref="CollectionMirror"/> that monitors the given collection.
        /// </summary>
        /// <param name="dispatcher">
        /// The <see cref="EventQueue"/> to which calls to <see cref="SetUp"/> 
        /// and <see cref="TearDown"/> will be serialized.
        /// </param>
        /// <param name="collection">
        /// The collection to monitor.
        /// </param>
        public CollectionMirror(EventQueue dispatcher, Collection<V> collection) {
            this.q = dispatcher;
            this.c = collection;
            this.t = new Dictionary<V, T>();
        }

        ~CollectionMirror() {
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
        /// Causes the <see cref="CollectionMirror"/> to stop listening for changes
        /// to the underlying <see cref="Owner">collection</see>, and invokes
        /// <see cref="IDisposable.Dispose()"/> on each of the non-null members
        /// of the <see cref="Tags"/> enumeration.
        /// </summary>
        protected virtual void Dispose(bool disposing) {
            if (this.m_Disposed)
                return;

            this.m_Disposed = true;

            if (disposing) {
                List<T> dispose;
                using (Synchronizer.Lock(this.c.SyncRoot)) {
                    // Disconnect the "added" event listener.
                    IDisposable a = Interlocked.Exchange(ref this.a, null);
                    if (a != null)
                        a.Dispose();

                    // Disconnect the "removed" event listener.
                    IDisposable r = Interlocked.Exchange(ref this.r, null);
                    if (r != null)
                        r.Dispose();

                    // Copy the tags to be disposed and erase the association list.
                    dispose = new List<T>(this.Tags);
                    this.t.Clear();
                }

                // Dispose of the tags *after* releasing the lock on the collection.
                foreach (IDisposable tag in dispose) {
                    if (tag != null)
                        tag.Dispose();
                }
            }
        }

        /// <summary>
        /// Establishes the event listeners that listen for changes to the 
        /// <see cref="Owner">collection</see>, and optionally invokes
        /// <see cref="SetUp"/> for each of the existing members of the collection.
        /// This method must be invoked by inheriting classes in order for
        /// the <see cref="CollectionMirror"/> to function.
        /// </summary>
        /// <remarks>
        /// This functionality is not performed automatically by the 
        /// <see cref="CollectionMirror()"/> constructor so that inheriting
        /// classes may have ample time with which to initialize their own 
        /// state without any possibility of <see cref="SetUp"/> or 
        /// <see cref="TearDown"/> being invoked prematurely.
        /// </remarks>
        /// <param name="synchronize">
        /// If <code>true</code>, then existing members will be <see cref="SetUp">set up</see>.
        /// Otherwise, existing members will be ignored.
        /// </param>
        /// <exception cref="ObjectDisposedException">
        /// If the <see cref="CollectionMirror"/> has been disposed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// If <see cref="Initialize"/> has already been invoked.
        /// </exception>
        protected void Initialize(bool synchronize) {
            using (Synchronizer.Lock(this.c.SyncRoot)) {

                if (this.m_Initialized)
                    throw new InvalidOperationException("The collection mirror has already been initialized.");

                if (this.m_Disposed)
                    throw new ObjectDisposedException("CollectionMirror",
                        "A collection mirror cannot be initialized after it has been disposed.");

                this.m_Initialized = true;

                // Copy existing members, if so desired by the caller.
                if (synchronize) {
                    for (int i = 0; i < this.c.Count; i++) {
                        V member = this.c[i];
                        this.q.Post(delegate() {
                            if (this.m_Disposed)
                                return;
                            T tag;
                            if (this.SetUp(i, member, out tag))
                                this.t[member] = tag;
                        });
                    }
                }

                // Create the listener which invokes SetUp when new members are added.
                this.a = this.c.Listen(
                    this.q,
                    delegate(Collection<V>.AddedEventArgs args) {
                        if (this.m_Disposed)
                            return;
                        T tag, old;
                        // Invoke SetUp and store the tag returned.
                        if (this.SetUp(args.Index, args.Added, out tag)) {
                            // Dispose of any existing tag.  This can happen
                            // if two "equal" members are added to the collection.
                            if (this.t.TryGetValue(args.Added, out old))
                                if (old != null)
                                    old.Dispose();
                            this.t[args.Added] = tag;
                        }
                    });

                // Create the listener which invokes TearDown when members are removed.
                this.r = this.c.Listen(
                    this.q,
                    delegate(Collection<V>.RemovedEventArgs args) {
                        if (this.m_Disposed)
                            return;
                        T tag;
                        // Invoke TearDown, passing any tag which was returned by SetUp.
                        if (this.t.TryGetValue(args.Removed, out tag)) {
                            this.TearDown(args.Index, args.Removed, tag);
                            this.t.Remove(args.Removed);
                        }
                    });
            }
        }

        /// <summary>
        /// Gets an enumeration of all tags that have been returned by <see cref="SetUp"/>.
        /// Tags corresponding to members that have been removed from the 
        /// <see cref="Owner">collection</see> are <emph>not</emph> included.
        /// </summary>
        /// <remarks>
        /// Once the <see cref="CollectionMirror"/> has been <see cref="Dispose()">disposed</see>,
        /// the <see cref="Tags"/> enumeration will be empty.
        /// </remarks>
        protected IEnumerable<T> Tags {
            get { return this.t.Values; }
        }

        /// <summary>
        /// Invoked when a new member has been added to the <see cref="Owner">collection</see>,
        /// </summary>
        /// <remarks>
        /// <see cref="SetUp"/> is also invoked for each existing member of the collection when
        /// the <see cref="CollectionMirror"/> is first
        /// <see cref="Initialize(bool)">initialized</see>, if the <code>synchronize</code> parameter
        /// passed to that method is <code>true</code>.
        /// </remarks>
        /// <param name="index">
        /// The index of the member in the collection <emph>at the time that it was added</emph>.
        /// This might no longer matche the actual current index if the <see cref="EventQueue"/> caused 
        /// some delay during which time the collection was modified.  However, those modifications
        /// will be serialized to the <see cref="EventQueue"/> in the correct order, so that it should
        /// be possible to recover later from any inconsistancy.
        /// </param>
        /// <param name="member">
        /// The member which has been added.
        /// </param>
        /// <param name="tag">
        /// A tag which will be passed to <see cref="TearDown"/> when the member is later removed,
        /// and which will also be <see cref="IDisposable.Dispose()">disposed</see> if the 
        /// <see cref="CollectionMirror"/> is <see cref="Dispose()">disposed</see> before 
        /// <see cref="TearDown"/> is invoked.  The tag may be <code>null</code>.
        /// The tag is ignored if <see cref="SetUp"/> returns <code>false</code>.
        /// <para>
        /// Note that the tag is associated with the <paramref name="member"/> <emph>by equality</emph>,
        /// not by index; therefore, if the collection has dupliate members, the correct tag might not be returned.
        /// If a tag already exists for the given member, it will be <see cref="IDisposable.Dispose()"/>
        /// disposed of and discarded <emph>after</emph> <see cref="SetUp"/> is invoked, unless
        /// <see cref="SetUp"/> returns <code>false</code>, in which case the existing tag will
        /// be left alone and the new tag ignored.
        /// </para>
        /// </param>
        /// <returns>
        /// A value indicating whether <see cref="TearDown"/> should be invoked later when the 
        /// member is removed.  If <code>false</code>, then <see cref="TearDown"/> will
        /// <emph>not</emph> be invoked, and the tag will <emph>not</emph> be stored.
        /// This means that if the tag requires <see cref="IDisposable.Dispose()">disposal</see>,
        /// returning <code>false</code> means it might not be properly disposed.
        /// </returns>
        protected abstract bool SetUp(int index, V member, out T tag);

        /// <summary>
        /// Invoked when a new member has been removed from the <see cref="Owner">collection</see>.
        /// The default implementation <see cref="IDisposable.Dispose()">disposes</see> of the tag
        /// if it is not <code>null</code>.
        /// </summary>
        /// <param name="index">
        /// The index of the member in the collection <emph>at the time that it was removed</emph>.
        /// </param>
        /// <param name="member">
        /// The member which has been removed.
        /// </param>
        /// <param name="tag">
        /// The tag which was returned by <see cref="SetUp"/>, if any, when the 
        /// <paramref name="member"/> was added.
        /// <para>
        /// Note that the tag is associated with the <paramref name="member"/> <emph>by equality</emph>,
        /// not by index; therefore, if the collection has dupliate members, the correct tag might not be returned.
        /// </para>
        /// <para>
        /// Overriding implementations should <see cref="IDisposable.Dispose()">dispose</see> of the tag,
        /// if necessary.
        /// </para>
        /// </param>
        protected virtual void TearDown(int index, V member, T tag) {
            if (tag != null)
                tag.Dispose();
        }

        /// <summary>
        /// A utility <see cref="CollectionMirror"/> implementation whose
        /// <see cref="SetUp"/> and <see cref="TearDown"/> methods invoke
        /// corresponding delegates which may be provided when constructing
        /// the <see cref="Delegator"/>.  This class is intended to be
        /// used in conjunction with anonymous delegates.
        /// </summary>
        public class Delegator : CollectionMirror<V, T> {
            private readonly SetUpDelegate s;
            private new readonly TearDownDelegate t;

            /// <summary>
            /// Creates a new <see cref="Delegator"/> instance which invokes the
            /// given delegates when <see cref="SetUp"/> and <see cref="TearDown"/>
            /// are called.
            /// </summary>
            /// <param name="setup"></param>
            /// The delegate to invoke when <see cref="SetUp"/> is called.
            /// <param name="teardown">
            /// The delegate to invoke when <see cref="TearDown"/> is called.
            /// </param>
            public Delegator(EventQueue dispatcher, Collection<V> collection,
                SetUpDelegate setup, TearDownDelegate teardown)
                : base(dispatcher, collection) {

                this.s = setup;

                this.t = (teardown != null) ? teardown : new TearDownDelegate(base.TearDown);
            }

            /// <summary>
            /// Creates a new <see cref="Delegator"/> instance which invokes
            /// the given delegate when <see cref="SetUp"/> is called, and which
            /// invokes the default <see cref="CollectionMirror.TearDown"/> method.
            /// </summary>
            /// <param name="setup">
            /// The delegate to invoke when <see cref="SetUp"/> is called.
            /// </param>
            public Delegator(EventQueue dispatcher, Collection<V> collection, SetUpDelegate s)
                : this(dispatcher, collection, s, null) { }

            protected override bool SetUp(int index, V member, out T tag) {
                return this.s(index, member, out tag);
            }

            protected override void TearDown(int index, V member, T tag) {
                this.t(index, member, tag);
            }

            public delegate bool SetUpDelegate(int index, V member, out T tag);
            public delegate void TearDownDelegate(int index, V member, T tag);
        }
    }
}
