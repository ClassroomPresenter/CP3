// $Id: ControlEventQueue.cs 975 2006-06-28 01:02:59Z pediddle $

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Windows.Forms;

namespace UW.ClassroomPresenter.Model {
    /// <summary>
    /// An <see cref="EventQueue"/> which posts events to a control's thread using
    /// <see cref="Control.BeginInvoke"/>.
    /// <seealso cref="Post(Delegate,object[])"/>
    /// </summary>
    public class ControlEventQueue : EventQueue {

        /// <summary>
        /// The control to which events are posted.
        /// </summary>
        /// <remarks>
        /// All internal synchronization should be done on the control!
        /// </remarks>
        private readonly Control m_Control;

        /// <summary>
        /// A <see cref="Queue"/> containing <see cref="EventDelegate"/> instances.
        /// </summary>
        /// <remarks>
        /// The queue is not used unless events are posted while the control's handle is unavailable.
        /// </remarks>
        private readonly Queue<EventDelegate> m_Queue;

        /// <summary>
        /// Creates a new <see cref="ControlEventQueue"/>.
        /// </summary>
        /// <param name="control">
        /// The control whose thread events will be posted to.
        /// </param>
        public ControlEventQueue(Control control) {
            this.m_Control = control;
            this.m_Queue = new Queue<EventDelegate>();
            this.m_Control.HandleCreated += new EventHandler(this.OnControlHandleCreated);
        }

        /// <summary>
        /// Releases any resources used by the <see cref="ControlEventQueue"/>,
        /// and clears the queue of any events which have not yet been executed.
        /// </summary>
        /// <remarks>
        /// Once events have been posted to the control's creator thread with
        /// <see cref="Control.BeginInvoke"/>, nothing can stop them from executing.
        /// However, if events are still queued locally by the
        /// <see cref="ControlEventQueue"/> (ie., if the control's handle was
        /// unavailable when they were posted), then <see cref="Dispose"/> will
        /// release those events and they will <em>never</em> be executed.
        /// </remarks>
        protected override void Dispose(bool disposing) {
            if(disposing) {
                using(Synchronizer.Lock(this.m_Control)) {
                    try {
                        if(!this.Disposed) {
                            this.m_Control.HandleCreated -= new EventHandler(this.OnControlHandleCreated);

                            // We'll never be able to execute any events again, so clear the queue
                            // to release to the garbage collector any data therein.
                            this.m_Queue.Clear();
                        }
                    } finally {
                        base.Dispose(disposing);
                    }
                }
            } else {
                base.Dispose(disposing);
            }
        }

        /// <summary>
        /// Posts an event to the control's event queue using <see cref="Control.BeginInvoke"/>.
        /// </summary>
        /// <remarks>
        /// If the control's handle is not available, the delegate will not be executed
        /// until the handle is (re-)created.
        /// </remarks>
        /// <param name="method">The method to invoke on the event thread.</param>
        public override void Post(EventDelegate method) {
            // Lock on the control to:
            // (1) Prevent events from being posted while the queue is being flushed.
            // (2) Prevent the control's handle from being created or destroyed while this method executes.
            using(Synchronizer.Lock(this.m_Control)) {
                if(this.Disposed) {
                    Debug.WriteLine("WARNING: Ignoring posted event since EventQueue is disposed.",
                        this.GetType().ToString() + "<" + this.m_Control.GetType().Name + ">");
                    return;
                }

                // If InvokeRequired is true, then the control has a handle and
                // we can use BeginInvoke immediately.  Otherwise, it is safe to call
                // IsHandleCreated (it would not be safe if InvokeRequired were true),
                // and if there is a handle, we can also call BeginInvoke from any thread.
                // However, if there are events waiting in our own queue (ie., we're in
                // the middle of a call to FlushEventQueue), then the event must be queued
                // to preserve execution order.
                if(this.m_Queue.Count <= 0 && (this.m_Control.InvokeRequired || this.m_Control.IsHandleCreated)) {
                    this.m_Control.BeginInvoke(method);
                }

                else {
                    // But if there is no handle, we cannot call BeginInvoke, so we'll have to
                    // wait until the control's handle is (re-)created.  Therefore, queue
                    // the delegate and its arguments until OnControlHandleCreated can use it.
                    this.m_Queue.Enqueue(method);
                }
            }
        }

        /// <summary>
        /// Calls <see cref="FlushEventQueue"/> when the control's handle is created.
        /// </summary>
        private void OnControlHandleCreated(object sender, EventArgs e) {
            this.FlushEventQueue();
        }

        /// <summary>
        /// Flushes any queued events.
        /// </summary>
        /// <remarks>
        /// Posted events cannot be posted to the control's own event queue if the
        /// control's handle is not available.  When this happens, the events are queued locally.
        /// Once the control's handle is created, it becomes possible to flush the queue.
        /// <para>
        /// Note that events might still remain in the queue after <see cref="FlushEventQueue"/>
        /// returns if the control's handle is destroyed at any time during the flush.
        /// </para>
        /// <para>
        /// This method should only be called from the control's creator thread.
        /// </para>
        /// </remarks>
        protected virtual void FlushEventQueue() {
            // We need to prevent events from being posted while we iterate over the queue,
            // and the Post method locks on the control, so we'll do the same here.
            using(Synchronizer.Lock(this.m_Control)) {
                while(this.m_Queue.Count > 0) {
                    Debug.Assert(!this.m_Control.InvokeRequired, "FlushEventQueue called from the wrong thread.");
                    Debug.Assert(this.m_Control.IsHandleCreated, "Control.HandleCreated is a liar: there is no handle.");

                    // It's possible that the event handler destroyed the handle.
                    // If that is the case, we must stop processing the queue until
                    // OnHandleCreated is called again.
                    if(!this.m_Control.IsHandleCreated)
                        break;

                    // If we have a handle, we can queue the delegate for execution.
                    this.m_Control.BeginInvoke(this.m_Queue.Dequeue());
                }
            }
        }
    }
}
