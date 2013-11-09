using System;
using System.Collections.Generic;
using System.Text;

namespace UW.ClassroomPresenter.Model {

    /// <summary>
    /// Utility class that implements <see cref="IDisposable"/>
    /// on behalf of types <typeparamref name="T"/> that do not 
    /// themselves implement <see cref="IDisposable"/>.
    /// </summary>
    /// <remarks>
    /// If <typeparamref name="T"/> <emph>does</emph> implement <see cref="IDisposable"/>,
    /// then its <see cref="IDisposable.Dispose">Dispose()</see> method will be invoked
    /// when the <see cref="Disposable"/> instance is disposed.  Otherwise,
    /// <see cref="Disposable.Dispose()"/> is a no-op.
    /// </remarks>
    /// <typeparam name="T">
    /// The type of the <see cref="Value"/> field.
    /// </typeparam>
    public struct Disposable<T> : IDisposable {
        /// <summary>
        /// The value wrapped by the <see cref="Disposable"/> instance.
        /// </summary>
        /// <remarks>
        /// If <typeparamref name="T"/> <emph>does</emph> implement <see cref="IDisposable"/>,
        /// then its <see cref="IDisposable.Dispose">Dispose()</see> method will be invoked
        /// when the <see cref="Disposable"/> instance is disposed.  Otherwise,
        /// <see cref="Disposable.Dispose()"/> is a no-op.
        /// </remarks>
        public readonly T Value;

        /// <summary>
        /// Creates a new <see cref="Disposable"/> instance which wraps the given value.
        /// </summary>
        /// <param name="value">
        /// The value to wrap, which will be assigned to the <see cref="Value"/> field.
        /// </param>
        public Disposable(T value) {
            this.Value = value;
        }

        /// <summary>
        /// Gets the <see cref="Value"/> field of a <see cref="Disposable"/> wrapper.
        /// </summary>
        /// <param name="wrapper">The wrapper whose value to get.</param>
        /// <returns>The value of <code>wrapper.Value</code>.</returns>
        public static implicit operator T(Disposable<T> wrapper) {
            return wrapper.Value;
        }

        /// <summary>
        /// Gets the <see cref="Value"/> field of a <see cref="Disposable"/> wrapper.
        /// </summary>
        /// <param name="wrapper">The wrapper whose value to get.</param>
        /// <returns>The value of <code>wrapper.Value</code>.</returns>
        public static T operator ~(Disposable<T> wrapper) {
            return wrapper.Value;
        }

        /// <summary>
        /// Invokes <see cref="IDisposable.Dispose()"/> on the wrapped <see cref="Value"/>
        /// if <typeparamref name="T"/> implements <see cref="IDisposable"/> and the value
        /// is non-null; otherwise, does nothing.
        /// </summary>
        public void Dispose() {
            IDisposable d = this.Value as IDisposable;
            if (d != null)
                d.Dispose();
        }
    }

    /// <summary>
    /// Utility class that implements <see cref="IDisposable"/> by invoking
    /// a <see cref="Disposable.Delegate"/> when it is disposed.
    /// </summary>
    /// <example>
    /// This class is most easily used by instantiating with an 
    /// anonymous delegate.  For example,
    /// <code><![CDATA[
    /// SomeResourceClass x = ...; // Allocate resources.
    /// IDisposable disposer = new Disposable(delegate() {
    ///     // Dispose of resources here.
    /// });
    /// 
    /// ... // Code which makes use of resources.
    /// 
    /// // Dispose the resources at some later time.
    /// disposer.Dispose();
    /// ]]></code>
    /// </example>
    public struct Disposable : IDisposable {
        private readonly Delegate m;

        /// <summary>
        /// Creates a new <see cref="Disposable"/> instance which will
        /// invoke the given delegate when it is disposed.
        /// </summary>
        /// <param name="disposer">The delegate to invoke.</param>
        public Disposable(Delegate disposer) {
            this.m = disposer;
        }

        /// <summary>
        /// Invokes the <see cref="Disposer.Delegate">delegate</see>
        /// which was passed to the <see cref="Disposable()"/> constructor.
        /// </summary>
        public void Dispose() {
            this.m();
        }

        /// <summary>
        /// A no-parameter delegate which can be passed to the
        /// <see cref="Disposable()"/> constructor.
        /// </summary>
        public delegate void Delegate();
    }
}
