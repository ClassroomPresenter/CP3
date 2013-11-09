// $Id: ThreadEventQueue.cs 975 2006-06-28 01:02:59Z pediddle $

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace UW.ClassroomPresenter.Model {

    /// <summary>
    /// An <see cref="EventQueue"/> which executes events on a dedicated thread.
    /// </summary>
    /// <remarks>
    /// The thread is a background thread which can be stopped manually by calling <see cref="Dispose"/>.
    /// </remarks>
    public class ThreadEventQueue : EventQueue {
        // Note: All locking, waiting, etc. will be done on this.m_Queue instead of this!
        private readonly PriorityQueue<Invoker> m_Queue;
        private bool m_Disposed;

        /// <summary>
        /// Creates a new <see cref="ThreadEventQueue"/>, and starts the dedicated event thread
        /// which will process posted events.
        /// </summary>
        public ThreadEventQueue() {
            this.m_Queue = new PriorityQueue<Invoker>();

            Thread thread = new Thread(new ThreadStart(this.ProcessMessages));
            thread.Name = "ThreadEventQueue.ProcessMessages";
            thread.IsBackground = true;
            thread.Start();
        }

        /// <summary>
        /// Stops executing queued events, stops the event thread, and releases any
        /// resources used by the <see cref="ThreadEventQueue"/>.
        /// </summary>
        /// <param name="disposing"></param>
        protected override void Dispose(bool disposing) {
            try {
                if(disposing) {
                    using(Synchronizer.Cookie cookie = Synchronizer.Lock(this.m_Queue)) {
                        if(this.m_Disposed) return;
                        this.m_Disposed = true;

                        // Setting m_Disposed to true will cause the ProcessMessages thread to stop, once we pulse the monitor.
                        Monitor.Pulse(this.m_Queue);
                    }
                }
            } finally {
                base.Dispose(disposing);
            }
        }

        /// <summary>
        /// Posts an event to the dedicated event thread with 
        /// <see cref="MessagePriority.Default">default message priority</see>.
        /// </summary>
        /// <param name="method">The method to invoke on the event thread.</param>
        public override void Post(EventQueue.EventDelegate method) {
            this.Post(method, MessagePriority.Default);
        }

        /// <summary>
        /// Posts an event to the dedicated event thread with the specified priority.
        /// </summary>
        /// <param name="method">The method to invoke on the event thread.</param>
        /// <param name="priority">Controls the order in which messages are executed.</param>
        public virtual void Post(EventDelegate method, MessagePriority priority) {
            using(Synchronizer.Lock(this.m_Queue)) {
                if(this.m_Disposed) {
                    Debug.WriteLine("WARNING: Ignoring event posted to disposed event queue.",
                        this.GetType().ToString());
                    return;
                }

                // Insert the message into the queue.
                this.m_Queue.Enqueue(new Invoker(method, priority));

                // The ProcessMessages thread is the only thread that can
                // be Waiting on the queue, so we only need to pulse one thread.
                Monitor.Pulse(this.m_Queue);
            }
        }

        /// <summary>
        /// The thread loop which executes queued events.
        /// </summary>
        /// <remarks>
        /// The loop iterates each time <c>Monitor.Pulse(this.m_Queue)</c> is called.
        /// It is expected that this will <em>only</em> happen when either <see cref="Invoker"/>s
        /// are added to the queue <em>or</em> <see cref="Dispose"/> is called.
        /// </remarks>
        private void ProcessMessages() {
            Synchronizer.Cookie cookie = Synchronizer.Lock(this.m_Queue);
            try {
                while(!this.m_Disposed) {
                    try {
                        // Wait until we have a message in the queue.
                        if(this.m_Queue.Count <= 0) {
                            cookie.Wait();
                        }

                        // Halt the thread as soon as the ThreadEventQueue is disposed.
                        if(this.m_Disposed) {
                            return;
                        }

                        // Monitor.Pulse is only called either when the ThreadEventQueue is disposed
                        // or when a message is added to the queue.
                        Debug.Assert(this.m_Queue.Count > 0);

                        Invoker invoker = this.m_Queue.Dequeue();

                        // Temporarily release the lock so other threads can add things to the
                        // queue while the message is processing.  This also avoids potential deadlocks.
                        cookie.Dispose();
                        try {
                            invoker.Method();
                        } finally {
                            cookie = Synchronizer.Lock(this.m_Queue);
                        }
                    }

                    catch(Exception e) {
                        // Report the error, but do not break out of the thread.
                        // TODO: Log the error to the system event log.
                        Trace.Fail("The ThreadEventQueue encountered an error: " + e.ToString(), e.StackTrace);
                    }
                }
            } finally {
                cookie.Dispose();
            }
        }

        /// <summary>
        /// Encapsulates a delegate and its arguments.
        /// </summary>
        private class Invoker : IComparable<Invoker> {
            public readonly EventDelegate Method;
            private readonly ulong m_Priority;

            /// <summary>
            /// The <see cref="PriorityQueue"/>'s sorting is not necessarily stable, so
            /// this counter is used to ensure that <see cref="Invoker">Invokers</see> are
            /// always sorted (1) by their priority, but also (2) by the order in which 
            /// they were created.
            /// </summary>
            private static long c_Sequence;

            public Invoker(EventDelegate method, MessagePriority priority) {
                this.Method = method;

                // Divide the ulong space into 6 segments (from MessagePriority.Lowest == 0 to RealTime == 5).
                // Then add a global sequence number, so Invokers are sorted first by priority, then by sequence.
                this.m_Priority = ((ulong.MaxValue / ((ulong) MessagePriority.RealTime))
                    * (((ulong) MessagePriority.RealTime) - (ulong) priority))
                    + ((ulong) Interlocked.Increment(ref c_Sequence));
            }

            #region IComparable<Invoker> Members

            public int CompareTo(Invoker obj) {
                return this.m_Priority.CompareTo(obj.m_Priority);
            }

            #endregion IComparable<Invoker> Members
        }

        /// <summary>
        /// A standard array-based heap implementation.
        /// </summary>
        protected class PriorityQueue<T> where T : IComparable<T> {
            private T[] m_Array;
            private int m_Count;

            public PriorityQueue() {
                this.m_Array = new T[2];
            }

            /// <summary>
            /// Inserts an object into the queue.
            /// </summary>
            /// <param name="value"></param>
            public void Enqueue(T value) {
                this.m_Count++;

                // Grow the array if necessary.
                if(this.m_Count >= this.m_Array.Length)
                    this.m_Array.CopyTo(this.m_Array = new T[this.m_Array.Length * 2], 0);

                // Insert the value into the queue, beginning the search
                // for the proper slot at the first empty slot in the array.
                this.PercolateUp(this.m_Count, value);
            }

            /// <summary>
            /// Gets and removes the minimum-valued object from the queue.
            /// </summary>
            /// <exception cref="InvalidOperationException">
            /// If the queue is empty.
            /// </exception>
            /// <returns>the minimum-valued object from the queue</returns>
            public T Dequeue() {
                if(this.m_Count <= 0)
                    throw new InvalidOperationException("The PriorityQueue is empty.");

                // Fetch the highest priority item in the queue.
                // This will be the return value of the method.
                T value = this.m_Array[1];

                // Replace it with the last item in the array,
                // which we will then "percolate down" to restore
                // the heap order property.  (If the queue is empty,
                // this will set it to the null sentinel value.)
                T replacement = this.m_Array[this.m_Count];
                this.m_Array[1] = default(T);
                this.m_Array[this.m_Count] = default(T);
                this.m_Count--;

                if(this.m_Count > 0)
                    this.PercolateDown(1, replacement);

                return value;
            }

            private void PercolateUp(int i, T value) {
                if(i == 1) {
                    // Special case if we've reached the top of the heap.
                    // (The value is the highest priority in the entire queue.)
                    this.m_Array[1] = value;
                } else if(this.m_Array[i / 2].CompareTo(value) < 0) {
                    // Simply insert the value if we've found a suitable slot.
                    this.m_Array[i] = value;
                } else {
                    // Recursive case: preserve the heap order property by
                    // searching higher in the tree and moving the old
                    // "parent" down in the tree.
                    this.m_Array[i] = this.m_Array[i / 2];
                    this.PercolateUp(i / 2, value);
                }
            }

            private void PercolateDown(int i, T value) {
                if(2 * i > this.m_Count) {
                    // We've reached the bottom of the heap.
                    this.m_Array[i] = value;
                } else if(2 * i == this.m_Count) {
                    // We must special-case the last slot in the heap
                    // since there is only one sibling.
                    if(this.m_Array[2 * i].CompareTo(value) < 0) {
                        this.m_Array[i] = this.m_Array[2 * i];
                        this.m_Array[2 * i] = value;
                    } else {
                        this.m_Array[i] = value;
                    }
                } else {
                    // We must first find which of the two subtrees is lesser.
                    int j = (2 * i)
                        + (this.m_Array[2 * i].CompareTo(this.m_Array[2 * i + 1]) < 0 ? 0 : 1);
                    // Then see if we need to descend into the lesser subtree.
                    if(this.m_Array[j].CompareTo(value) < 0) {
                        // If we must percolate down further, this is the recursive case.
                        this.m_Array[i] = this.m_Array[j];
                        this.PercolateDown(j, value);
                    } else {
                        this.m_Array[i] = value;
                    }
                }
            }

            /// <summary>
            /// Gets the number of objects currently in the queue.
            /// </summary>
            public int Count {
                get { return this.m_Count; }
            }
        }
    }

    /// <summary>
    /// Enumeration which indicates the order in which 
    /// messages posted to a <see cref="ThreadEventQueue"/> should be executed.
    /// Messages posted with lower priority are not executed until the <see cref="ThreadEventQueue"/>
    /// is completely empty of all other messages of higher priority.
    /// </summary>
    public enum MessagePriority {
        /// <summary>
        /// The lowest priority.  Messages with this priority are not executed until the
        /// <see cref="ThreadEventQueue"/> is empty of all other messages.
        /// </summary>
        Lowest = -2,

        /// <summary>
        /// Lower than normal priority.  Messages with this priority are not 
        /// executed until the <see cref="ThreadEventQueue"/> is empty of all
        /// messages except those with <see cref="Lowest"/> priority.
        /// </summary>
        Low = -1,

        /// <summary>
        /// Default priority.
        /// </summary>
        Default = 0,

        /// <summary>
        /// Higher than normal priority.  Messages with this priority preempt 
        /// those with <see cref="Default"/> or lower priority.
        /// </summary>
        High = 1,

        /// <summary>
        /// The highest non-<see cref="RealTime"/> priority.  Messages with this priority
        /// preempt all others except <see cref="RealTime"/> messages, but are still
        /// expected to be executed safely and reliably.
        /// </summary>
        Higher = 2,

        /// <summary>
        /// The highest of all priorities, but sacrificing reliability.  Messages with
        /// this priority are executed before all others, but the <see cref="ThreadEventQueue"/>
        /// implementation <emph>may</emph> choose to make the message less reliable in
        /// order to execute the message more immediately, or the message may not be executed at all.
        /// <see cref="RealTime"/> messages should be small and fast, and/or highly resiliant to failure.
        /// </summary>
        /// <remarks>
        /// For example, the <see cref="RTPNetworkSender"/> implementation does not
        /// employ its RTP reliability features when sending <see cref="RealTime"/> messages.
        /// </remarks>
        RealTime = 3,
    }
}
