// $Id: EventQueue.cs 1278 2007-01-27 00:32:32Z pediddle $

using System;
using System.Collections;
using System.Diagnostics;
using System.Threading;

namespace UW.ClassroomPresenter.Model {
    /// <summary>
    /// An abstract class which allows events to be posted to a queue, to be executed later.
    /// </summary>
    /// <remarks>
    /// The primary purpose of this class is to allow event handlers to
    /// run with a "clean" call-stack.
    /// This is to prevent deadlocks due to unexpected locks being held
    /// by the caller of an event handler.
    /// </remarks>
    public abstract class EventQueue : IDisposable {
        private bool m_Disposed;

        /// <summary>
        /// Gets a <c>bool</c> indicating whether a call to <see cref="Dispose"/> has completed.
        /// </summary>
        public bool Disposed {
            get { return this.m_Disposed; }
        }

        ~EventQueue() {
            this.Dispose(false);
        }

        /// <summary>
        /// Releases any resources used by the <see cref="EventQueue"/>, and halts the execution
        /// of any queued posted events which are waiting to be executed.
        /// </summary>
        public void Dispose() {
            this.Dispose(true);
            System.GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Releases any resources used by the <see cref="EventQueue"/>, and halts the execution
        /// of any queued posted events which are waiting to be executed.
        /// </summary>
        /// <param name="disposing">
        /// <c>true</c> if called from <see cref="Dispose"/>, and <c>false</c> if
        /// called by the garbage collector's finalizer.
        /// </param>
        protected virtual void Dispose(bool disposing) {
            this.m_Disposed = true;
        }

        /// <summary>
        /// Posts an event which takes no arguments to the queue.
        /// </summary>
        /// <remarks>
        /// This overloaded method exists so that anonymous no-argument methods will default
        /// to the <see cref="ThreadStart"/> delegate type.  For example,
        /// <code>
        /// queue.Post(delegate() { ... });
        /// </code>
        /// would produce a type error if not for the fact that the delegate can be
        /// inferred by the compiler to be of type <see cref="ThreadStart"/>.
        /// </remarks>
        /// <param name="method">The no-argument method to be executed.</param>
        public abstract void Post(EventDelegate method);

        /// <summary>
        /// A delegate which takes no arguments, for use with <see cref="EventQueue.Post"/>.
        /// </summary>
        public delegate void EventDelegate();

        /// <summary>
        /// A utility class allowing <see cref="PropertyEventHandlers"/> to be easily executed
        /// on an <see cref="EventQueue"/>.
        /// <seealso cref="Dispatcher"/>
        /// </summary>
        public sealed class PropertyEventDispatcher {
            private readonly EventQueue m_Queue;
            private readonly PropertyEventHandler m_Handler;
            private readonly PropertyEventHandler m_Dispatcher;

#if DEBUG_EVENTS
            private string m_OriginalStackTrace;
#endif

            /// <summary>
            /// Creates a new <see cref="PropertyEventDispatcher"/>.
            /// </summary>
            /// <param name="queue">
            /// The <see cref="EventQueue"/> to which the <see cref="Dispatcher"/>
            /// will redirect events.
            /// </param>
            /// <param name="handler">
            /// The <see cref="PropertyEventHandler"/> to be executed on the <see cref="EventQueue"/>
            /// each time the <see cref="Dispatcher"/> is invoked.
            /// </param>
            public PropertyEventDispatcher(EventQueue queue, PropertyEventHandler handler) {
                this.m_Queue = queue;
                this.m_Handler = handler;
                this.m_Dispatcher = new PropertyEventHandler(this.HandlePropertyEvent);
            }

            /// <summary>
            /// Gets a <see cref="PropertyEventHandler"/> which will "redirect" all
            /// property events received to the handler which was passed to the
            /// <see cref="PropertyEventDispatcher"/> constructor,
            /// executed on the <see cref="EventQueue"/>.
            /// </summary>
            public PropertyEventHandler Dispatcher {
                get { return this.m_Dispatcher; }
            }

            /// <summary>
            /// Posts a property event to the <see cref="EventQueue"/>.
            /// </summary>
            /// <param name="sender">The sender of the event.</param>
            /// <param name="args">The arguments to the event.</param>
            private void HandlePropertyEvent(object sender, PropertyEventArgs args) {
#if DEBUG_EVENTS
                // Save the most recent stack trace of the caller.
                // Most of the time, this will be enough to figure out how
                // the property changed for debugging exceptions in
                // the event handler.
                this.m_OriginalStackTrace = new StackTrace(true).ToString();
#endif
                this.m_Queue.Post(delegate() { this.m_Handler(sender, args); });
            }
        }
    }
}
