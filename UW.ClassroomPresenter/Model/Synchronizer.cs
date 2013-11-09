// $Id: Synchronizer.cs 947 2006-04-12 02:42:47Z pediddle $

#if !DEBUG
#undef DEBUG_LOCK_TRACE
#undef DEBUG_LOCK_RELEASE
#endif

using System;
using System.Collections;
using System.Diagnostics;
using System.Text;
using System.Threading;

public interface ISynchronizable {
    object SyncRoot { get; }
}

public abstract class SyncRoot : ISynchronizable {
    private readonly object s = new object();

    object ISynchronizable.SyncRoot {
        [DebuggerStepThrough]
        get { return this.s; }
    }

    [DebuggerStepThrough]
    public Synchronizer.Cookie Lock() {
        return Synchronizer.Lock(this.s);
    }
}

public class Synchronizer {

    [DebuggerStepThrough]
    private Synchronizer() { }

    [DebuggerStepThrough]
    public static Cookie Lock(object o) {
        // Create the cookie which will release the lock.
#if DEBUG_LOCK_TRACE
        LockTrace t;
        lock(c_Traces.SyncRoot) {
            t = GetTrace(o);
            if(t == null)
                c_Traces[new HashableWeakReference(o)] = t = new LockTrace(o);
        }
        Cookie cookie = new Cookie(o, t);
#else
        Cookie cookie = new Cookie(o);
#endif
#if DEBUG_LOCK_CONTENTION
        if(!Monitor.TryEnter(o)) {
            Thread owner = t.Owner;
            Debug.WriteLine(string.Format("Lock contention detected on object of type {0}:",
                o.GetType().ToString()), t.GetType().ToString());
            Debug.Indent();
            try {
                Debug.WriteLine(string.Format("Contending thread: {0}; Thread owning lock: {1}",
                    Thread.CurrentThread.Name, owner == null ? null : owner.Name));
            } finally {
                Debug.Unindent();
            }
            Monitor.Enter(o);
        }
#else
        // Acquire the lock.
        Monitor.Enter(o);
#endif
        try {
#if DEBUG_LOCK_TRACE
            t.TraceEnter();
#endif
            return cookie;
        } catch {
            // If there's an exception for any reason after the lock is acquired, make sure it's released.
            cookie.Dispose();
            throw;
        }
    }

    [Conditional("DEBUG_LOCK_TRACE")]
    [DebuggerStepThrough]
    public static void AssertLockIsHeld(object o) {
#if DEBUG_LOCK_TRACE
        LockTrace debug = GetTrace(o);
        if(debug == null || debug.Owner != Thread.CurrentThread)
            throw new SynchronizationLockException(string.Format("No lock is held on object {0}.", o.GetType().ToString()));
#endif
    }

    [Conditional("DEBUG_LOCK_TRACE")]
    [DebuggerStepThrough]
    public static void AssertLockIsHeld(object o, string message) {
#if DEBUG_LOCK_TRACE
        LockTrace debug = GetTrace(o);
        if(debug == null || debug.Owner != Thread.CurrentThread)
            throw new SynchronizationLockException(message);
#endif
    }

    public struct Cookie : IDisposable {
        private object o;

#if DEBUG_LOCK_TRACE
        [DebuggerStepThrough]
        internal Cookie(object o, LockTrace t) {
            this.t = t;
#else
        [DebuggerStepThrough]
        internal Cookie(object o) {
#endif
            this.o = o;
#if DEBUG_LOCK_RELEASE
            this.p = new DrainPlug(o.GetType().ToString());
#endif
        }

        [DebuggerStepThrough]
        public void Dispose() {
            object o = this.o;
            if(o == null)
                throw new InvalidOperationException("A Synchronizer.Cookie can only be disposed once.");
#if DEBUG_LOCK_RELEASE
            // Prevent the drain plug from detecting an error by preventing it from being finalized.
            System.GC.SuppressFinalize(this.p);
            this.p = null;
#endif
#if DEBUG_LOCK_TRACE
            this.t.TraceExit();
#endif
            this.o = null;
            Monitor.Exit(o);
        }

        [DebuggerStepThrough]
        public bool Wait() {
            return this.Wait(Timeout.Infinite);
        }

        [DebuggerStepThrough]
        public bool Wait(int millisecondsTimeout) {
            return this.Wait(new TimeSpan(0, 0, 0, 0, millisecondsTimeout));
        }

        [DebuggerStepThrough]
        public bool Wait(TimeSpan timeout) {
#if DEBUG_LOCK_TRACE
            this.t.TraceExit();
            try {
                return Monitor.Wait(this.o, timeout);
            } finally {
                this.t.TraceEnter();
            }
#else
            return Monitor.Wait(this.o, timeout);
#endif
        }

        [DebuggerStepThrough]
        public void Pulse() {
            Monitor.Pulse(this.o);
        }

        [DebuggerStepThrough]
        public void PulseAll() {
            Monitor.PulseAll(this.o);
        }

#if DEBUG_LOCK_TRACE
        /// <summary>
        /// A reference to the LockTrace object.
        /// </summary>
        private LockTrace t;
#endif

#if DEBUG_LOCK_RELEASE
        /// <summary>
        /// A reference to a DrainPlug object.
        /// </summary>
        private DrainPlug p;

        /// <summary>
        /// As discussed in <http://www.interact-sw.co.uk/iangblog/2004/04/26/yetmoretimedlocking>,
        /// the "drain plug" serves to ensure that if the cookie is "leaked", an error will be displayed
        /// as the garbage collector finalizes it.
        /// </summary>
        private class DrainPlug {
            private readonly string name;

           [DebuggerStepThrough]
            public DrainPlug(string name) {
                this.name = name;
            }

            [DebuggerStepThrough]
            ~DrainPlug() {
                Debug.Fail(string.Format("A Synchronizer.Cookie was not disposed on object {0}!", this.name));
            }
        }
#endif
    }

#if DEBUG_LOCK_TRACE
    private static readonly Hashtable c_Traces = new Hashtable();

    [DebuggerStepThrough]
    private static LockTrace GetTrace(object o) {
        lock(c_Traces.SyncRoot) {
            return ((LockTrace) c_Traces[o]);
        }
    }

    internal class LockTrace {
        private static readonly LocalDataStoreSlot c_Stacks = Thread.AllocateDataSlot();

        private readonly Hashtable m_Precursors = new Hashtable();
        private readonly string type;
        public int Count;
        public Thread Owner;

        [DebuggerStepThrough]
        public LockTrace(object o) {
            this.type = o.GetType().ToString();
        }

        [DebuggerStepThrough]
        public void TraceEnter() {
            Stack stack = ((Stack) Thread.GetData(c_Stacks));

            try {
                if(stack == null) {
                    Thread.SetData(c_Stacks, stack = new Stack());
                    stack.Push(this);
                    return;
                } else if(stack.Contains(this)) {
                    // Do not process the sequence if the Synchronizer is already locked.
                    // This could cause us to miss some *potential* bugs, but it still won't
                    // fail to report any code which could *actually* cause a deadlock.
                    // This is to avoid false-positives when multiple locks are acquired recursively.
                    Debug.Assert(this.Count > 0);
                    return;
                } else if(stack.Count == 0) {
                    stack.Push(this);
                    return;
                }

                StackTrace trace = null;

                // Validate the sequence in which the locks were acquired using a "graph" of sorts.
                // The set of locks which have ever been locked by a thread at the time that
                // this synchronizer was locked are stored in the m_Precursors table.
                // If a "precursor" is already locked when a new lock is taken out on a synchronizer,
                // then the potential for deadlock exists because the order is inconsistent.
                ArrayList failures = null;
                foreach(LockTrace s in stack) {
                    Debug.Assert(s != this);

                    if(s.m_Precursors.ContainsKey(this)) {
                        if(failures == null) failures = new ArrayList();
                        if(!failures.Contains(s)) failures.Add(s);
                    }

                    // Save the stack trace for debugging later.
                    if(!this.m_Precursors.ContainsKey(s)) {
                        if(trace == null)
                            trace = new StackTrace(2, true);
                        this.m_Precursors.Add(s, trace);
                    }
                }

                if(failures != null) {
                    StringBuilder detail = new StringBuilder();
                    detail.Append("An object of type ");
                    detail.Append(this.type);
                    detail.Append(" was locked after the following other objects: ");
                    foreach(LockTrace s in failures) {
                        detail.Append(s.type);
                        detail.Append(", ");
                    }
                    detail.Append("whereas they were previously locked in a different order.");

                    try {
                        Debug.WriteLine(detail, this.GetType().ToString());
                        Debug.Indent();
                        Debug.WriteLine("Stack trace of the current lock:");
                        Debug.Indent();
                        if(trace == null)
                            trace = new StackTrace(2, true);
                        for(int i = 0; i < trace.FrameCount; i++)
                            Debug.WriteLine(FormatStackFrame(trace.GetFrame(i)));
                        Debug.Unindent();
                        Debug.WriteLine("Stack traces of the previous lock in the different order:");
                        Debug.Indent();
                        foreach(LockTrace s in failures) {
                            Debug.WriteLine(string.Format("Object of type {0}:", s.type));
                            StackTrace other = ((StackTrace) s.m_Precursors[this]);
                            Debug.Indent();
                            for(int i = 0; i < other.FrameCount; i++)
                                Debug.WriteLine(FormatStackFrame(other.GetFrame(i)));
                            Debug.Unindent();
                        }
                        Debug.Unindent();
                        Debug.Unindent();

                        // Throw and catch an exception.  This is so we can automatically break execution
                        // using Visual Studio's "first-chance" exception handling.
                        // But a normal user (even when DEBUG is defined) should not be interrupted.
                        throw new SynchronizationLockException(detail.ToString());
                    } catch {}
                }

                stack.Push(this);
            }

            finally {
                if(!((this.Owner == null && this.Count == 0) || (this.Count > 0 && this.Owner == Thread.CurrentThread)))
                    throw new SynchronizationLockException(string.Format("An object of type {0} is locked by multiple threads.  A Synchronizer.Cookie was probably not disposed.", this.type));
                this.Owner = Thread.CurrentThread;
                this.Count++;
            }
        }

        [DebuggerStepThrough]
        private static string FormatStackFrame(StackFrame f) {
            System.Reflection.MethodBase mb = f.GetMethod();
            if(mb == null) return "<unknown>";
            string method = string.Format("{0}.{1}", mb.DeclaringType.Name, mb.Name);

            string file = f.GetFileName();
            if(file == null) return method;
            else return string.Format("{0} in \"{1}\":{2}:{3}", method, file, f.GetFileLineNumber(), f.GetFileColumnNumber());
        }

        [DebuggerStepThrough]
        public void TraceExit() {
            Debug.Assert(this.Count > 0 && this.Owner != null, "A Synchronizer.Cookie was somehow disposed out of order.");
            if(--this.Count <= 0)
                this.Owner = null;

            Stack stack = ((Stack) Thread.GetData(c_Stacks));

            Debug.Assert(stack != null, "TraceExit cannot be called before TraceEnter.");
            Debug.Assert(this.Count >= 0, "A Synchronizer.Cookie was disposed more than once.");

            if(this.Count == 0) {
                object popped = stack.Pop();

                Debug.WriteLineIf(popped != this,
                    string.Format("A lock on an object of type {0} was released out of order.", this.type));
            }
        }
    }

    private class HashableWeakReference : WeakReference {
        private readonly int m_HashCode;

        [DebuggerStepThrough]
        public HashableWeakReference(object o)
            : base(o) {
            this.m_HashCode = o.GetHashCode();
        }

        [DebuggerStepThrough]
        public override bool Equals(object obj) {
            return (this.IsAlive && object.ReferenceEquals(this.Target, obj))
                || object.ReferenceEquals(this, obj);
        }

        [DebuggerStepThrough]
        public override int GetHashCode() {
            Debug.Assert(!this.IsAlive || this.m_HashCode == this.Target.GetHashCode(), "The referent's HashCode has changed.");
            return this.m_HashCode;
        }
    }

#endif
}
