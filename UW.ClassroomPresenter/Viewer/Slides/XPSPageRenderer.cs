using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Documents;
using System.Drawing;
using System.Windows.Xps.Packaging;
using System.Windows.Forms;
using System.Windows.Xps;
using System.Windows.Media;
using System.Windows.Media.Imaging;

using UW.ClassroomPresenter.Model;
using UW.ClassroomPresenter.Model.Presentation;
using UW.ClassroomPresenter.Model.Viewer;

namespace UW.ClassroomPresenter.Viewer.Slides
{
    public class XPSPageRenderer :SheetRenderer {
        private SlideDisplayModel m_Display;
        private XPSPageSheetModel m_Sheet;
        private bool m_Disposed;
        private EventQueue.PropertyEventDispatcher repaint_dispatcher_;
        
        public XPSPageRenderer(SlideDisplayModel display, XPSPageSheetModel sheet):base(display,sheet) {
            this.m_Display = display;
            this.m_Sheet = sheet;

            this.repaint_dispatcher_ = new EventQueue.PropertyEventDispatcher(this.SlideDisplay.EventQueue, this.Repaint);
            if (this.m_Sheet != null) {
                this.m_Sheet.Changed["Bounds"].Add(new PropertyEventHandler(repaint_dispatcher_.Dispatcher));
                this.SlideDisplay.Changed["Slide"].Add(new PropertyEventHandler(repaint_dispatcher_.Dispatcher));
            }
        }

        protected override void Dispose(bool disposing) {
            if (this.m_Disposed) return;
            try {
                if (disposing) {
                    if (this.m_Sheet != null) {
                        this.m_Sheet.Changed["Bounds"].Remove(new PropertyEventHandler(repaint_dispatcher_.Dispatcher));
                        this.SlideDisplay.Changed["Slide"].Remove(new PropertyEventHandler(repaint_dispatcher_.Dispatcher));
                        this.SlideDisplay.Invalidate();
                    }
                }
            }
            finally {
                base.Dispose(disposing);
            }
            this.m_Disposed = true;
        }

        public override void Paint(PaintEventArgs args) {
            if (this.m_Sheet == null) return;
            
            RenderTargetBitmap botmap = new RenderTargetBitmap(this.m_Sheet.Width, this.m_Sheet.Height, 96d, 96d, PixelFormats.Default);
            if (this.m_Sheet.DocumentPage == null && this.m_Sheet.DocumentPageWrapper != null) 
                this.m_Sheet.DocumentPage = this.m_Sheet.DocumentPageWrapper.XPSDocumentPage;
            if (this.m_Sheet.DocumentPage == null) return;
            
            botmap.Render(this.m_Sheet.DocumentPage.Visual);

            byte[] rgbValues = new byte[this.m_Sheet.Height * this.m_Sheet.Width * 4];

            botmap.CopyPixels(rgbValues, this.m_Sheet.Width * 4, 0);

            using (Bitmap bmp = new Bitmap(this.m_Sheet.Width, this.m_Sheet.Height, System.Drawing.Imaging.PixelFormat.Format32bppRgb)) {
                System.Drawing.Imaging.BitmapData bd = bmp.LockBits(new Rectangle(0, 0, this.m_Sheet.Width, this.m_Sheet.Height),
                    System.Drawing.Imaging.ImageLockMode.WriteOnly,
                    System.Drawing.Imaging.PixelFormat.Format32bppRgb);
                System.Runtime.InteropServices.Marshal.Copy(rgbValues, 0, bd.Scan0, rgbValues.Length);
                bmp.UnlockBits(bd);

                using (Synchronizer.Lock(this.SlideDisplay.SyncRoot)) {
                    Graphics g = args.Graphics;
                    ///Lock the graphics object so it doesn't get used elsewhere.
                    using (Synchronizer.Lock(g)) {
                        g.Transform = this.SlideDisplay.PixelTransform;

                        using (Synchronizer.Lock(this.m_Sheet.SyncRoot)) {
                            using (Synchronizer.Lock(bmp)) {
                                g.DrawImage(bmp, this.m_Sheet.Bounds.X - 3, this.m_Sheet.Bounds.Y - 3, this.m_Sheet.Bounds.Width, this.m_Sheet.Bounds.Height);
                            }
                        }
                    }
                }
            }
            System.GC.Collect();
        }

        private void Repaint(object sender, PropertyEventArgs args) {
            // TODO: Invalidate only the changed region.
            this.SlideDisplay.Invalidate();
        }
        
    }
}
