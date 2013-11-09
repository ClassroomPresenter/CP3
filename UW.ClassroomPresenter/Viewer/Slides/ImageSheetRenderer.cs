// $Id: ImageSheetRenderer.cs 1705 2008-08-12 21:40:25Z lamphare $

using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Threading;
using System.Windows.Forms;

using UW.ClassroomPresenter.Model;
using UW.ClassroomPresenter.Model.Presentation;
using UW.ClassroomPresenter.Model.Viewer;
using System.Diagnostics;

namespace UW.ClassroomPresenter.Viewer.Slides {
    public class ImageSheetRenderer : SheetRenderer {
        private readonly ImageSheetModel m_Sheet;
        private bool m_Disposed;
        private EventQueue.PropertyEventDispatcher repaint_dispatcher_;

        private static readonly Pen c_NoImagePen = CreateNoImagePen();

        public ImageSheetRenderer(SlideDisplayModel display, ImageSheetModel sheet) : base(display, sheet) {
            
            this.m_Sheet = sheet;
            repaint_dispatcher_ = new EventQueue.PropertyEventDispatcher(this.SlideDisplay.EventQueue, this.Repaint);

            if (m_Sheet.Image  == null) {
                // we are looking at a background image
                if (this.m_Sheet.Deck != null) {
                    this.m_Sheet.Deck.SlideContentAdded += new PropertyEventHandler(repaint_dispatcher_.Dispatcher);
                }
                else { 
                    //FIXME: This happened a couple of times.  Ignoring it is not the best fix.
                    Trace.WriteLine("Warning: ImageSheetModel.Deck was found to be null when creating ImageSheetRenderer.");
                }
            } else {
                //we're looking at an ImageIt image
                this.m_Sheet.Changed["Bounds"].Add(new PropertyEventHandler(repaint_dispatcher_.Dispatcher));
                this.m_Sheet.Changed["Image"].Add(new PropertyEventHandler(repaint_dispatcher_.Dispatcher)); 
                this.SlideDisplay.Changed["Slide"].Add(new PropertyEventHandler(repaint_dispatcher_.Dispatcher));
            }
        }

        protected override void Dispose(bool disposing) {
            if(this.m_Disposed) return;
            try {
                if(disposing) {
                    if (m_Sheet.Deck != null) {
                        //we are in background image mode
                        this.m_Sheet.Deck.SlideContentAdded -= new PropertyEventHandler(repaint_dispatcher_.Dispatcher);
                    } else {
                        //we are in ImageIt mode
                        this.m_Sheet.Changed["Bounds"].Remove(new PropertyEventHandler(repaint_dispatcher_.Dispatcher));
                        this.m_Sheet.Changed["Image"].Remove(new PropertyEventHandler(repaint_dispatcher_.Dispatcher));
                        this.SlideDisplay.Changed["Slide"].Remove(new PropertyEventHandler(repaint_dispatcher_.Dispatcher));
                        this.SlideDisplay.Invalidate();                   
                    }
                }
            } finally {
                base.Dispose(disposing);
            }
            this.m_Disposed = true;
        }

        public override void Paint(PaintEventArgs args) {
            if (this.m_Sheet == null || (!this.m_Sheet.Visible && this.m_Sheet.Image != null)) {
                ///this is an ImageIt image and we should not be painting it
                return;
            }
            using (Synchronizer.Lock(this.SlideDisplay.SyncRoot)) {
                Graphics g = args.Graphics;
                ///Lock the graphics object so it doesn't get used elsewhere.
                using (Synchronizer.Lock(g)) {
                    g.Transform = this.SlideDisplay.PixelTransform;

                    if (this.m_Sheet.Image != null) {
                        //this is an image on the slide, not a background image, so we'll just paint it the
                        //easy way
                        using (Synchronizer.Lock(this.m_Sheet.SyncRoot)) {
                            using (Synchronizer.Lock(this.m_Sheet.Image)) {
                                g.DrawImage(this.m_Sheet.Image, this.m_Sheet.Bounds.X - 3, this.m_Sheet.Bounds.Y - 3, this.m_Sheet.Bounds.Width, this.m_Sheet.Bounds.Height);
                            }
                        }
                    }
                    else {
                        // do the icky complicated looking stuff.
                        if (this.m_Sheet.Deck != null) {
                            using (Synchronizer.Lock(this.m_Sheet.Deck.SyncRoot)) {
                                using (Synchronizer.Lock(this.m_Sheet.SyncRoot)) {
                                    Image image = this.m_Sheet.Deck.GetSlideContent(this.m_Sheet.MD5);

                                    // The ImageContent might not have been loaded yet if the slide is broadcast over the network.
                                    if (image == null) {
                                        Rectangle bounds = this.m_Sheet.Bounds;
                                        g.DrawLine(c_NoImagePen, bounds.Location, new Point(bounds.Right, bounds.Bottom));
                                        g.DrawLine(c_NoImagePen, new Point(bounds.X, bounds.Bottom), new Point(bounds.Right, bounds.Y));
                                        g.DrawRectangle(c_NoImagePen, bounds);
                                        return;
                                    }

                                    // Bug 605: If the image's actual rendered size is less than 2f pixels tall or wide,
                                    // then Graphics.DrawImage will (often) fail with an ExternalException, code 0x80004005.
                                    // This seems to be a bug in GDI+, and it also seems to only happen with non-integer transform scaling factors.
                                    // This threshhold was discovered empirically, and doesn't seem to be documented anywhere.
                                    // It's possible this bug could reappear under different conditions.
                                    // The solution is to simply ignore images that would render to an area less than this small size.
                                    // TODO: This will ignore very small sheets (ie., 1-pixel horizontal or vertical lines).
                                    //   If no better fix can be found for this, then DeckBuilder should be modified to give
                                    //   small images a transparent buffer around the edge to make them bigger.
                                    PointF[] sizeV = new PointF[] { (PointF)(Point)this.m_Sheet.Bounds.Size };
                                    g.Transform.TransformVectors(sizeV);
                                    if (sizeV[0].X >= 2f && sizeV[0].Y >= 2f) {

                                        // The Image class is not capable of multithreaded access.
                                        // Simultaneous access will throw a
                                        //    "InvalidOperationException: The object is currently in use elsewhere."
                                        // So it needs to be locked, and also locked anywhere else the Image is accessed.
                                        using (Synchronizer.Lock(image)) {
                                            // Draw the image, scaled to be within its bounds.
                                            g.DrawImage(image, this.m_Sheet.Bounds);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        private void Repaint(object sender, PropertyEventArgs args) {
            // TODO: Invalidate only the changed region.
            this.SlideDisplay.Invalidate();
        }

        private static Pen CreateNoImagePen() {
            Pen pen = new Pen(Color.Red, 2.0f);
            pen.Alignment = PenAlignment.Inset;
            return pen;
        }
    }
}
