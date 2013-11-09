using System;
using System.Collections.Generic;
using System.Drawing;

using UW.ClassroomPresenter.Model.Background;

namespace UW.ClassroomPresenter.Viewer.Background
{
    /// <summary>
    /// Abstract class for primitive render
    /// </summary>
    public abstract class PrimitiveRenderer:IDisposable
    {
        /// <summary>
        /// Get corresponding render according the primitive model
        /// </summary>
        public static PrimitiveRenderer GetRenderer(IBackgroundPrimitive primitive, float zoom)
        {
            if (primitive is LinePrimitive)
            {
                return new LinePrimitiveRenderer((LinePrimitive)primitive, zoom);
            }
            else if (primitive is IteratorPrimitive)
            {
                return new IteratorPrimitiveRenderer((IteratorPrimitive)primitive,zoom);
            }
            else
            {
                return null;
            }
           
        }

        /// <summary>
        /// Draw the current Primitive
        /// </summary>
        public abstract void Draw(BackgroundTemplate parent, Graphics g, Rectangle r);

        #region Dispose

        private bool m_Disposed;

        public void Dispose()
        {
            this.Dispose(true);
            System.GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (this.m_Disposed) return;
            if (disposing)
            {
            }
            this.m_Disposed = true;
        }

        #endregion
    }
}
