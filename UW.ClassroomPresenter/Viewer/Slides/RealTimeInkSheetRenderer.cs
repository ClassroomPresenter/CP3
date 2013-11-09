// $Id: RealTimeInkSheetRenderer.cs 776 2005-09-21 20:57:14Z pediddle $

using System;
using System.Diagnostics;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;

using UW.ClassroomPresenter.Model;
using UW.ClassroomPresenter.Model.Presentation;
using UW.ClassroomPresenter.Model.Viewer;
using UW.ClassroomPresenter.Viewer.Inking;

namespace UW.ClassroomPresenter.Viewer.Slides {
    /// <summary>
    /// Summary description for RealTimeInkSheetRenderer.
    /// </summary>
    public class RealTimeInkSheetRenderer : InkSheetRenderer {
        private readonly RealTimeInkSheetModel m_Sheet;
        private readonly TransformableDynamicRenderer.Core.RealTimeInkAdapter m_Renderer;
        private bool m_Disposed;

        /// <summary>
        /// Creates a new RealTimeInkSheetRenderer, which will draw ink from the specified
        /// <see cref="RealTimeInkSheetModel"/> on <see cref="Graphics"/> obtained
        /// from the specified <see cref="InkSheetRendererParent"/>.
        /// </summary>
        /// <remarks>
        /// By default, rendering of real-time ink is enabled.  It may be disabled by setting the
        /// <see cref="RealTimeInkEnabled"/> property.
        /// <para>
        /// Drawing of "static" ink is always enabled, but it is only drawn when the parent
        /// <see cref="SlideRenderer"/> is invalidated.  It is <em>not</em> drawn when new ink
        /// is added to the <see cref="InkSheetModel"/>, for it is assumed that a "third-party"
        /// <see cref="TransformableDynamicRenderer"/> or other renderer is doing the job.
        /// </para>
        /// </remarks>
        public RealTimeInkSheetRenderer(SlideDisplayModel display, RealTimeInkSheetModel sheet) : base(display, sheet) {
            this.m_Sheet = sheet;

            // FIXME: When the SlideViewer that created the SlideDisplayModel is invalidated, invoke TransformableDynamicRenderer.Refresh().
            this.m_Renderer = new TransformableDynamicRenderer.Core.RealTimeInkAdapter(display, sheet);

            if((this.m_Sheet.Disposition & SheetDisposition.Remote) == 0) {
                this.SlideDisplay.Changed["RenderLocalRealTimeInk"].Add(new PropertyEventHandler(this.HandleRenderRealTimeInkChanged));
                this.HandleRenderRealTimeInkChanged(this.SlideDisplay, null);
            } else {
                this.RenderRealTimeInk = true;
            }
        }

        protected override void Dispose(bool disposing) {
            if(this.m_Disposed) return;
            try {
                if(disposing) {
                    if((this.m_Sheet.Disposition & SheetDisposition.Remote) == 0)
                        this.SlideDisplay.Changed["RenderLocalRealTimeInk"].Remove(new PropertyEventHandler(this.HandleRenderRealTimeInkChanged));
                    this.m_Renderer.Dispose();
                }
            } finally {
                base.Dispose(disposing);
            }
            this.m_Disposed = true;
        }

        public override void Paint(PaintEventArgs args) {
            // TODO: Refresh only the necessary clip region.
            if(this.m_Disposed)
                throw new ObjectDisposedException("RealTimeInkSheetRenderer");

            this.m_Renderer.Refresh();
            base.Paint(args);
        }

        protected bool RenderRealTimeInk {
            get { return this.m_Renderer.Enabled; }
            set { this.m_Renderer.Enabled = value; }
        }

        private void HandleRenderRealTimeInkChanged(object sender, PropertyEventArgs args) {
            Debug.Assert((this.m_Sheet.Disposition & SheetDisposition.Remote) == 0);
            using(Synchronizer.Lock(this.SlideDisplay.SyncRoot)) {
                this.RenderRealTimeInk = this.SlideDisplay.RenderLocalRealTimeInk;
            }
        }
    }
}
