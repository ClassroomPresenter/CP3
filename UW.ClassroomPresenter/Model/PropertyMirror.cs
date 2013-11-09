using System;
using System.Threading;

namespace UW.ClassroomPresenter.Model {
    public abstract class PropertyMirror<V, T> : IDisposable
        where O : ISynchronizable
        where T : IDisposable {

        private readonly EventQueue q;
        private readonly Property<V> p;

        private IDisposable m_Changer;
        private T t;

        private bool m_Initialized;
        private volatile bool m_Disposed;

        public PropertyMirror(EventQueue dispatcher, Property<V> property, T tag) {
            this.q = dispatcher;
            this.p = property;
            this.t = tag;
        }

        public PropertyMirror(EventQueue dispatcher, Property<V> property)
            : this(dispatcher, property, default(T)) { }

        ~PropertyMirror() {
            this.Dispose(false);
        }

        public void Dispose() {
            this.Dispose(true);
            System.GC.SuppressFinalize(this);
        }

        protected void Initialize() {
            using (Synchronizer.Lock(this.p.Owner.SyncRoot)) {

                if (this.m_Initialized)
                    throw new InvalidOperationException("The property mirror has already been initialized.");

                if (this.m_Disposed)
                    throw new ObjectDisposedException("PropertyMirror",
                        "A property mirror cannot be initialized after it has been disposed.");

                this.m_Initialized = true;

                this.m_Changer = this.p.ListenAndInitialize(
                    this.q,
                    delegate(Property<V>.EventArgs args) {
                        if (this.m_Disposed)
                            return;
                        Debug.Assert(args.Sender == this.p,
                            "PropertyMirror received an event from a property it does not own.");
                        this.Changed(args.Old, args.New, ref this.t);
                    });
            }
        }

        protected virtual void Dispose(bool disposing) {
            if (this.m_Disposed)
                return;

            if (disposing) {
                IDisposable changer = Interlocked.Exchange(ref this.m_Changer, null);
                if (changer != null) changer.Dispose();

                T tag = Interlocked.Exchange(ref this.m_Tag, null);
                if (tag != null) this.m_Tag.Dispose();
            }
        }

        protected abstract void Changed(V old, V value, ref T tag);

        public class Delegator : PropertyMirror<V, T> {
            private readonly ChangedDelegate c;

            public Delegator(EventQueue dispatcher, Property<V> property, T tag, ChangedDelegate changed)
                : base(dispatcher, property, tag) {
                this.c = changed;
            }

            public Delegator(EventQueue dispatcher, Property<V> property, ChangedDelegate changed)
                : this(dispatcher, property, default(T), changed) { }

            protected override void Changed(V old, V value, ref T tag) {
                this.c(old, value, ref tag);
            }

            public new Delegator Initialize() {
                base.Initialize();
                return this;
            }

            public delegate void ChangedDelegate(V old, V value, ref T tag);
        }
    }
}
