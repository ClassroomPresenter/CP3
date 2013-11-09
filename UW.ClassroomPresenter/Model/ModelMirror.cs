using System;
using System.Collections.Generic;
using System.Text;
using System.Reflection;

namespace UW.ClassroomPresenter.Model {
    /// <summary>
    /// Copies changes between <see cref="Property">Properties</see>
    /// and <see cref="Collection">Collections</see>.
    /// </summary>
    public class ModelMirror {
        private readonly EventQueue q;

        private readonly List<IDisposable> l;

        private volatile bool m_Disposed;
        private volatile bool m_Enabled;

        /// <summary>
        /// Creates a new <see cref="ModelMirror"/>.
        /// </summary>
        /// <param name="dispatcher">
        /// The <see cref="EventQueue"/> on which changes across all
        /// links are serialized.  It is presumed that such events will
        /// be executed by the <see cref="EventQueue"/> on the same thread.
        /// </param>
        public ModelMirror(EventQueue dispatcher) {
            this.q = dispatcher;
            this.l = new List<IDisposable>();
            this.m_Enabled = true;
        }

        ~ModelMirror() {
            this.Dispose(false);
        }

        /// <summary>
        /// Immediately causes all links to stop reacting to events,
        /// and disposes of the links and any other resources used.
        /// </summary>
        /// <remarks>
        /// After the <see cref="ModelMirror"/> is disposed, links will stop
        /// responding to events from the <see cref="EventQueue"/> even if the
        /// events were posted before disposal (but not fired until after
        /// disposal).
        /// </remarks>
        public void Dispose() {
            this.Dispose(true);
            System.GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Immediately causes all links to stop reacting to events,
        /// and disposes of the links and any other resources used.
        /// </summary>
        /// <remarks>
        /// After the <see cref="ModelMirror"/> is disposed, links will stop
        /// responding to events from the <see cref="EventQueue"/> even if the
        /// events were posted before disposal (but not fired until after
        /// disposal).
        /// </remarks>
        /// <param name="disposing">
        /// <code>true</code> if this method is called by <see cref="Dispose()"/>,
        /// or <code>false</code> if called by the garbage collector.
        /// If <paramref name="disposing"/> is <code>false</code>, then
        /// links will stop responding to events but are not themselves disposed.
        /// </param>
        protected virtual void Dispose(bool disposing) {
            if (this.m_Disposed)
                return;

            this.m_Disposed = true;
            this.m_Enabled = false;

            if (disposing) {
                using (Synchronizer.Lock(this)) {
                    foreach (IDisposable link in this.l) {
                        link.Dispose();
                    }

                    this.l.Clear();
                }
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether links will <i>temporarily</i>
        /// stop responding to events.  Note that this may cause the link targets
        /// to become unsynchronized, a state from which it may not be possible
        /// to completely recover.  Links are not "refreshed" when the <see cref="ModelMirror"/>
        /// is re-enabled.
        /// </summary>
        /// <exception cref="ObjectDisposedException">
        /// If the <see cref="ModelMirror"/> has been disposed.
        /// </exception>
        public bool Enabled {
            get { return this.m_Enabled; }
            set {
                if (this.m_Disposed)
                    throw new ObjectDisposedException("ModelMirror",
                        "Cannot enable or disable a ModelMirror that has been disposed.");

                // This doesn't need to be synchronized because even if the ModelMirror
                // is disposed between the check/exception above and this point,
                // links will immediately stop reacting to events anyway.
                this.m_Enabled = value;
            }
        }

        /// <summary>
        /// Registers a "link" that will copy changes from one <see cref="Property"/>
        /// of a given value type <typeparamref name="V"/> to another <see cref="Property"/>
        /// of the same value type.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The link persists until the <see cref="ModelMirror"/> is
        /// <see cref="Dispose()">disposed</see>, at which time it immediately ceases
        /// to function, even if pending events on the <see cref="EventQueue"/> have
        /// yet to be processed.
        /// </para>
        /// <para>
        /// Changes on the "<paramref name="from">from</paramref>" property are
        /// serialized on the <see cref="ModelMirror">ModelMirror</see>'s 
        /// <see cref="EventQueue"/>.
        /// </para>
        /// </remarks>
        /// <typeparam name="O1">
        /// The owner-type of the <paramref name="from"/> property.
        /// </typeparam>
        /// <typeparam name="O2">
        /// The owner-type of the <paramref name="to"/> property.
        /// </typeparam>
        /// <typeparam name="V">
        /// The value-type which is common between the two properties.
        /// </typeparam>
        /// <param name="from">
        /// The property from which value-changes originate.
        /// </param>
        /// <param name="to">
        /// The property to which value-changes are copied.
        /// </param>
        /// <exception cref="ObjectDisposedException">
        /// If the <see cref="ModelMirror"/> has been disposed.
        /// </exception>
        public void Link<V>(Property<V> from, Property<V> to) {

            if (this.m_Disposed)
                throw new ObjectDisposedException("ModelMirror",
                    "New links cannot be established once a ModelMirror has been disposed.");

            using (Synchronizer.Lock(this)) {
                this.l.Add(from.ListenAndInitialize(this.q, delegate(Property<V>.EventArgs args) {
                    if (this.m_Disposed || !this.m_Enabled)
                        return;

                    Debug.Assert(args.Sender == from,
                        "ModelMirror received an event from an unlinked property.");

                    using (Synchronizer.Lock(to.Owner.SyncRoot)) {
                        to.Value = args.New;
                    }
                }));
            }
        }

        /// <summary>
        /// Registers a "link" that will synchronize members from one <see cref="Collection"/>
        /// of a given member type <typeparamref name="V"/> to another <see cref="Collection"/>
        /// of the same member type.
        /// </summary>
        /// <remarks>
        /// <para>
        /// All changes made to the <paramref name="from"/> collection are copied to
        /// the <paramref name="to"/> collection, including
        /// <see cref="Collection.Add">additions</see> and
        /// <see cref="Collection.Remove">removals</see>.
        /// </para>
        /// <para>
        /// When the link is first created, all members of the <paramref name="from"/>
        /// collection are immediately copied to the <paramref name="to"/> collection
        /// (serialized on the <see cref="ModelMirror"/>'s <see cref="EventQueue"/>,
        /// of course).  If this behavior is not desirable, then the <see cref="ModelMirror"/>
        /// should be <see cref="Enabled">disabled</see> before the link is created,
        /// and re-enabled afterwards.  Be aware, however, that this could adversely
        /// affect other links that have been established on the <see cref="ModelMirror"/>.
        /// </para>
        /// <para>
        /// If the <paramref name="to"/> collection is not empty or is modified
        /// in any way other than via this link, or if the <see cref="ModelMirror"/>
        /// is <see cref="Enabled">disabled</see> at any time, then
        /// no guarantees can be made about the relative ordering of the copied members.
        /// In other words, the indices of the <paramref name="from"/> collection 
        /// will not necessarily match the indices of the same members in the
        /// <paramref name="to"/> collection.
        /// </para>
        /// <para>
        /// The link persists until the <see cref="ModelMirror"/> is
        /// <see cref="Dispose()">disposed</see>, at which time it immediately ceases
        /// to function, even if pending events on the <see cref="EventQueue"/> have
        /// yet to be processed.
        /// </para>
        /// <para>
        /// Changes on the "<paramref name="from">from</paramref>" collection are
        /// serialized on the <see cref="ModelMirror">ModelMirror</see>'s 
        /// <see cref="EventQueue"/>.
        /// </para>
        /// </remarks>
        /// <typeparam name="O1">
        /// The owner-type of the <paramref name="from"/> collection.
        /// </typeparam>
        /// <typeparam name="O2">
        /// The owner-type of the <paramref name="to"/> collection.
        /// </typeparam>
        /// <typeparam name="V">
        /// The member-type which is common between the two collection.
        /// </typeparam>
        /// <param name="from">
        /// The collection from which member changes originate.
        /// </param>
        /// <param name="to">
        /// The to which members are copied.
        /// </param>
        /// <exception cref="ObjectDisposedException">
        /// If the <see cref="ModelMirror"/> has been disposed.
        /// </exception>
        public void Link<V>(Collection<V> from, Collection<V> to) {
            if (this.m_Disposed)
                throw new ObjectDisposedException("ModelMirror",
                    "New links cannot be established once a ModelMirror has been disposed.");

            using (Synchronizer.Lock(this)) {
                this.l.Add(new CollectionLink<V>(this, from, to));
            }
        }

        /// <summary>
        /// Utility class that implements the link created by
        /// <see cref="ModelMirror.Link(Collection)"/>.
        /// </summary>
        /// <typeparam name="O1">
        /// The owner-type of the "from" collection
        /// </typeparam>
        /// <typeparam name="O2">
        /// The owner-type of the "to" collection.
        /// </typeparam>
        /// <typeparam name="V">
        /// The member type that is common between the two collections.
        /// </typeparam>
        private class CollectionLink<V> : CollectionMirror<V, IDisposable> {
            private readonly ModelMirror m;
            private readonly Collection<V> to;

            internal CollectionLink(ModelMirror mirror, Collection<V> from, Collection<V> to)
                : base(mirror.q, from) {

                this.m = mirror;
                this.to = to;

                // Only copy the existing members if the ModelMirror is enabled.
                if(this.m.m_Enabled)
                    base.Initialize();
            }

            protected override bool SetUp(int index, V member, out IDisposable tag) {
                // There is nothing to do when the CollectionLink is disposed,
                // so we have no use for a tag.
                tag = null;

                // If we should not copy the new member becaues the ModelMirror is
                // disposed or disabled, return "false" so TearDown will not be
                // invoked if the member is removed at some later time when the
                // ModelMirror has been re-enabled.
                if (this.m.m_Disposed || !this.m.m_Enabled)
                    return false;

                // Attempt to insert the member at the correct index in the target
                // collection.  If the ModelMirror has never been disabled, and if
                // the target collection was empty when the link was established
                // and has never been modified, then this should work correctly.
                // Otherwise, there is no guarantee about the relative ordering
                // of the copied members.
                using (Synchronizer.Lock(this.to.Owner.SyncRoot)) {
                    index = Math.Max(0, Math.Min(index, this.to.Count));
                    this.to.InsertAt(index, member);
                }

                // Invoke TearDown if the member is later removed.
                return true;
            }

            protected override void TearDown(int index, V member, IDisposable tag) {
                Debug.Assert(tag == null,
                    "A non-null tag was somehow assigned in ModelMirror.CollectionLink.");

                // Do nothing if the ModelMirror has been disposed or disabled.
                if (this.m.m_Disposed || !this.m.m_Enabled)
                    return;

                // Attempt to find a matching member to remove from the target collection.
                using (Synchronizer.Lock(this.to.Owner.SyncRoot)) {
                    index = Math.Max(0, Math.Min(index, this.to.Count));

                    // Scan outwards in both directions, using the original index as a
                    // starting point, looking for a member of the target collection
                    // that equals the member we're trying to remove.
                    for (int i = index, j = index + 1; ; i--, j++) {
                        if (i >= 0 && member == this.to[index = i])
                            break;
                        if (j < this.to.Count) {
                            if (member == this.to[index = j])
                                break;
                        } else if (i < 0) {
                            // Give up, since there is no element in the target collection
                            // which equals the member we're trying to remove.
                            return;
                        }
                    }

                    // Once an equal member is found, remove it.
                    this.to.RemoveAt(index);
                }
            }
        }
    }
}
