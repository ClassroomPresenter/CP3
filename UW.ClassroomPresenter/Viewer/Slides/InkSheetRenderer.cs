// $Id: InkSheetRenderer.cs 1260 2006-12-12 04:02:54Z cmprince $

using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

using Microsoft.Ink;

using UW.ClassroomPresenter.Model;
using UW.ClassroomPresenter.Model.Presentation;
using UW.ClassroomPresenter.Model.Viewer;
using System.Diagnostics;

using UW.ClassroomPresenter.Decks;

namespace UW.ClassroomPresenter.Viewer.Slides {
    public class InkSheetRenderer : SheetRenderer {
        private readonly InkSheetModel m_Sheet;
        private readonly Renderer m_Renderer;
        private bool m_Disposed;

        // The length by which the DrawingAttributes of selected strokes is expanded when drawing a border around them.
        private const int SELECTED_BORDER_WIDTH__HIMETRIC = 300;

        public InkSheetRenderer(SlideDisplayModel display, InkSheetModel sheet) : base(display, sheet) {

            
            this.m_Sheet = sheet;

            this.m_Renderer = new Renderer();

            this.m_Sheet.Changed["Selection"].Add(new PropertyEventHandler(this.HandleSelectionChanged));
            this.m_Sheet.InkAdded += new StrokesEventHandler(this.HandleInkAdded);
            this.m_Sheet.InkDeleted += new StrokesEventHandler(this.HandleInkDeleted);

            this.SlideDisplay.Changed["InkTransform"].Add(new PropertyEventHandler(this.HandleInkTransformChanged));
            this.HandleInkTransformChanged(this.SlideDisplay, null);
        }

        protected override void Dispose(bool disposing) {
            if(this.m_Disposed) return;
            try {
                if(disposing) {
                    this.m_Sheet.Changed["Selection"].Remove(new PropertyEventHandler(this.HandleSelectionChanged));
                    this.m_Sheet.InkAdded -= new StrokesEventHandler(this.HandleInkAdded);
                    this.m_Sheet.InkDeleted -= new StrokesEventHandler(this.HandleInkDeleted);

                    this.SlideDisplay.Changed["InkTransform"].Remove(new PropertyEventHandler(this.HandleInkTransformChanged));
                }
            } finally {
                base.Dispose(disposing);
            }
            this.m_Disposed = false;
        }

        public void Paint( Graphics g, Rectangle bounds ) {
            // Lock the sheet and strokes
            using( Synchronizer.Lock( this.m_Sheet.SyncRoot ) ) {
                using( Synchronizer.Lock( this.m_Sheet.Ink.Strokes.SyncRoot ) ) {
                    if( this.m_Sheet.Ink.Strokes.Count <= 0 ) return;

                    // Make a copy of the set of all strokes to be painted.
                    // The selected strokes will be removed from this and painted separately.
                    Strokes strokes = this.m_Sheet.Ink.Strokes;
                    Strokes selection = this.m_Sheet.Selection;

                    if( selection != null )
                        strokes.Remove( selection );

                    // Create an image using temporary graphics and graphics buffer object
                    Bitmap imageForPrinting = new Bitmap( bounds.Width, bounds.Height );
                    using( Graphics graphicsImage = Graphics.FromImage( imageForPrinting ) )
                    using( DibGraphicsBuffer dib = new DibGraphicsBuffer() ) {
                        // Create temporary screen Graphics
                        System.Windows.Forms.Form tempForm = new System.Windows.Forms.Form();
                        Graphics screenGraphics = tempForm.CreateGraphics();
                        // Create temporary Graphics from the Device Independent Bitmap
                        using( Graphics graphicsTemp = dib.RequestBuffer( screenGraphics, bounds.Width, bounds.Height ) ) {
                            graphicsTemp.Clear( System.Drawing.Color.Transparent );

                            // Draw the unselected ink!
                            // (Ink is not a published property of the InkSheetModel, so no need to obtain a lock.)
                            this.m_Renderer.Draw( graphicsTemp, strokes );

                            if( selection != null ) {
                                // Draw each stroke individually, adjusting its DrawingAttributes.
                                // TODO: This is very inefficient compared to drawing them all as a group (make a selection of several hundred strokes to see -- a big slow-down).
                                foreach( Stroke stroke in selection ) {

                                    // It's possible to erase ink while it's selected,
                                    // either with the inverted end of the stylus or via the Undo service.
                                    // But even though the deleted ink won't be part of the main Ink.Strokes collection,
                                    // it won't have been removed from the selection Strokes collection.
                                    // TODO: Being able to delete strokes while they are selected might break more than this.
                                    //   Evaluate what needs to change in the eraser and undo service to make this less hacky.
                                    if( stroke.Deleted ) continue;

                                    // First draw a highlighted background for all the strokes.
                                    DrawingAttributes atts = stroke.DrawingAttributes.Clone();
                                    float width = atts.Width, height = atts.Height;
                                    atts.RasterOperation = RasterOperation.CopyPen;
                                    atts.Color = SystemColors.Highlight;
                                    atts.Width += SELECTED_BORDER_WIDTH__HIMETRIC;
                                    atts.Height += SELECTED_BORDER_WIDTH__HIMETRIC;
                                    this.m_Renderer.Draw( graphicsTemp, stroke, atts );
                                }

                                foreach( Stroke stroke in selection ) {
                                    if( stroke.Deleted ) continue;

                                    // Second, draw the stroke itself, but with a contrastive color.
                                    DrawingAttributes atts = stroke.DrawingAttributes.Clone();
                                    atts.RasterOperation = RasterOperation.CopyPen;
                                    atts.Color = SystemColors.HighlightText;
                                    this.m_Renderer.Draw( graphicsTemp, stroke, atts );
                                }
                            }

                            // Use the buffer to paint onto the final image
                            dib.PaintBuffer( graphicsImage, 0, 0 );

                            // Draw this image onto the printer graphics,
                            // adjusting for printer margins
                            g.DrawImage( imageForPrinting, bounds.Top, bounds.Left );
                        }
                    }
                }
            }
        }

        public override void Paint(PaintEventArgs args) {
            using (Synchronizer.Lock(this.m_Sheet.SyncRoot)) {
                using (Synchronizer.Lock(this.m_Sheet.Ink.Strokes.SyncRoot)) {
                    if (this.m_Sheet.Ink.Strokes.Count <= 0) return;

                    Graphics g = args.Graphics;

                    // Make a copy of the set of all strokes to be painted.
                    // The selected strokes will be removed from this and painted separately.
                    Strokes strokes = this.m_Sheet.Ink.Strokes;
                    Strokes selection = this.m_Sheet.Selection;

                    if (selection != null)
                        strokes.Remove(selection);

                    // Note: The Renderer.Draw(Graphics,...) method is buggy (as of TabletPC API version 1.7),
                    //   and it causes all sorts of corruption when used in conjunction with double-buffering.
                    //   The problem is actually due to the renderer not restoring the state of the DC when it
                    //   is finished (specifically due to not resetting the the clipping region, AFAICT).
                    //   The workaround is to save and restore the state of the DC ourselves, and also to set
                    //   the clip rectangle manually via some Win32 GDI interop calls.
                    IntPtr rgn = g.Clip.GetHrgn(g);
                    IntPtr hdc = g.GetHdc();
                    try {
                        int saved = SaveDC(hdc);
                        try {
                            // Copy the clip region from the Graphics object to the DC.
                            SelectClipRgn(hdc, rgn);

                            // Draw the unselected ink!
                            // (Ink is not a published property of the InkSheetModel, so no need to obtain a lock.)
                            this.m_Renderer.Draw(hdc, strokes);

                            if (selection != null) {
                                // Draw each stroke individually, adjusting its DrawingAttributes.
                                // TODO: This is very inefficient compared to drawing them all as a group (make a selection of several hundred strokes to see -- a big slow-down).
                                foreach (Stroke stroke in selection) {

                                    // It's possible to erase ink while it's selected,
                                    // either with the inverted end of the stylus or via the Undo service.
                                    // But even though the deleted ink won't be part of the main Ink.Strokes collection,
                                    // it won't have been removed from the selection Strokes collection.
                                    // TODO: Being able to delete strokes while they are selected might break more than this.
                                    //   Evaluate what needs to change in the eraser and undo service to make this less hacky.
                                    if (stroke.Deleted) continue;

                                    // First draw a highlighted background for all the strokes.
                                    DrawingAttributes atts = stroke.DrawingAttributes.Clone();
                                    float width = atts.Width, height = atts.Height;
                                    atts.RasterOperation = RasterOperation.CopyPen;
                                    atts.Color = SystemColors.Highlight;
                                    atts.Width += SELECTED_BORDER_WIDTH__HIMETRIC;
                                    atts.Height += SELECTED_BORDER_WIDTH__HIMETRIC;
                                    this.m_Renderer.Draw(hdc, stroke, atts);
                                }

                                foreach (Stroke stroke in selection) {
                                    if (stroke.Deleted) continue;

                                    // Second, draw the stroke itself, but with a contrastive color.
                                    DrawingAttributes atts = stroke.DrawingAttributes.Clone();
                                    atts.RasterOperation = RasterOperation.CopyPen;
                                    atts.Color = SystemColors.HighlightText;
                                    this.m_Renderer.Draw(hdc, stroke, atts);
                                }
                            }
                        }
                        finally {
                            RestoreDC(hdc, saved);
                        }
                    }
                    finally {
                        g.ReleaseHdc(hdc);
                        if( rgn != IntPtr.Zero )
                            g.Clip.ReleaseHrgn(rgn);

                    }
                }
            }
        }

        private void HandleSelectionChanged(object sender, PropertyEventArgs args_) {
            PropertyChangeEventArgs args = args_ as PropertyChangeEventArgs;
            Rectangle invalid = Rectangle.Empty;

            if(args != null) {
                if(args.NewValue is Strokes) {
                    invalid = ((Strokes) args.NewValue).GetBoundingBox();
                    if(args.OldValue is Strokes) {
                        invalid = Rectangle.Union(invalid, ((Strokes) args.OldValue).GetBoundingBox());
                    }
                } else if(args.OldValue is Strokes) {
                    invalid = ((Strokes) args.OldValue).GetBoundingBox();
                }
            }

            // Inflate the invalid region by the width of the border drawn around selected strokes,
            // sine Strokes.GetBoundingBox only accounts for the width of the strokes' natural DrawingAttributes.
            invalid.Inflate(SELECTED_BORDER_WIDTH__HIMETRIC, SELECTED_BORDER_WIDTH__HIMETRIC);

            this.InvalidateInkSpaceRectangle(invalid);
        }

        protected virtual void HandleInkAdded(object sender, StrokesEventArgs e) {
            Rectangle invalid = Rectangle.Empty;

            // HACK: It would be much simpler and more efficient to just use
            // Ink.CreateStrokes(e.StrokeIds).GetBoundingBox(), but CreateStrokes has
            // a tendency to throw "ComException HRESULT 0x80040222" (see Bug 726).
            // This hack manually iterates through the strokes looking for the added ones;
            // there doesn't seem to be any way to make this more efficient with a table
            // lookup, at least not with the APIs exposed by Microsoft.Ink.Ink.

            using (Synchronizer.Lock(this.m_Sheet.Ink.Strokes.SyncRoot)) {
                foreach (Stroke stroke in this.m_Sheet.Ink.Strokes) {
                    if (Array.IndexOf<int>(e.StrokeIds, stroke.Id) >= 0) {
                        invalid = (invalid == Rectangle.Empty)
                            ? stroke.GetBoundingBox()
                            : Rectangle.Union(invalid, stroke.GetBoundingBox());
                    }
                }
            }

            this.InvalidateInkSpaceRectangle(invalid);
        }

        private void InvalidateInkSpaceRectangle(Rectangle invalid) {
            using(Graphics g = this.SlideDisplay.CreateGraphics()) {
                Point location = invalid.Location;
                Point size = ((Point) invalid.Size);

                this.m_Renderer.InkSpaceToPixel(g, ref location);
                this.m_Renderer.InkSpaceToPixel(g, ref size);

                invalid = new Rectangle(location, ((Size) size));
                this.SlideDisplay.Invalidate(invalid);
            }
        }

        protected virtual void HandleInkDeleted(object sender, StrokesEventArgs e) {
            using(Synchronizer.Lock(this.m_Sheet.SyncRoot)) {
                this.SlideDisplay.Invalidate(this.m_Sheet.Bounds);
            }
        }

        private void HandleInkTransformChanged(object sender, PropertyEventArgs args) {
            using(Synchronizer.Lock(this.SlideDisplay.SyncRoot)) {
                this.m_Renderer.SetViewTransform(this.SlideDisplay.InkTransform);
            }
        }

        [DllImport("gdi32.dll")]
        static extern int SelectClipRgn(IntPtr hdc, IntPtr hrgn);
        [DllImport("gdi32.dll")]
        static extern bool RestoreDC(IntPtr hdc, int nSavedDC);
        [DllImport("gdi32.dll")]
        static extern int SaveDC(IntPtr hdc);

        [StructLayout(LayoutKind.Sequential)]
        [GuidAttribute("C39A4B1A-9186-40d8-BB26-DEE38D92A084")]
        public struct XFORM {
            [MarshalAs(UnmanagedType.R4)]
            public float eM11;
            [MarshalAs(UnmanagedType.R4)]
            public float eM12;
            [MarshalAs(UnmanagedType.R4)]
            public float eM21;
            [MarshalAs(UnmanagedType.R4)]
            public float eM22;
            [MarshalAs(UnmanagedType.R4)]
            public float eDx;
            [MarshalAs(UnmanagedType.R4)]
            public float eDy;
        }

        [DllImport("gdi32.dll")]
        static extern int SetWorldTransform( IntPtr hdc, IntPtr xform );

        [DllImport("gdi32.dll")]
        static extern int SetGraphicsMode( IntPtr hdc, int mode );

        [DllImport("gdi32.dll")]
        static extern int SetWindowOrgEx( IntPtr hdc, int x, int y, IntPtr pt );

    }
}
