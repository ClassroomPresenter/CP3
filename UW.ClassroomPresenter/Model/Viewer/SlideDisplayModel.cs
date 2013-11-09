// $Id: SlideDisplayModel.cs 1260 2006-12-12 04:02:54Z cmprince $

using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Threading;
using System.Windows.Forms;

using Microsoft.Ink;

using UW.ClassroomPresenter.Model;
using UW.ClassroomPresenter.Model.Presentation;

namespace UW.ClassroomPresenter.Model.Viewer {

    /// <summary>
    /// Encapsulates the information needed by <see cref="SlideRenderer"/>s and <see cref="SheetRenderer"/>s
    /// to render a slide onto a <see cref="SlideViewer"/>.
    /// </summary>
    public class SlideDisplayModel : PropertyPublisher {
        /// <summary>
        /// Used to convert HIMETRIC to pixels, and for <see cref="CreateGraphics"/>.
        /// </summary>
        private readonly Control m_Control;
        /// <summary>
        /// Must be set if m_Control is null
        /// </summary>
        private readonly Graphics m_Graphics;

        private readonly ControlEventQueue m_EventQueue;

        /// <summary>
        /// indicates whether this display is a second monitor display
        /// </summary>
        private bool second_monitor_ = false;

        /// <summary>
        /// Used exclusively by <see cref="FitSlideToBounds"/> for converting pixel coordinates to HIMETRIC coordinates.
        /// </summary>
        private readonly Renderer m_Renderer;

        // Published properties:
        private Rectangle m_Bounds;
        private Matrix m_PixelTransform;
        private Matrix m_InkTransform;
        private bool m_RenderLocalRealTimeInk;
        private SlideModel m_Slide;
        private SheetDisposition m_SheetDisposition;

        /// <summary>
        /// Instantiates a new <see cref="SlideDisplayModel"/>.
        /// </summary>
        /// <param name="control">
        /// The control that will be used for <see cref="CreateGraphics"/>; presumably, the <see cref="SlideViewer"/>
        /// on which the slide will be rendered.
        /// </param>
        public SlideDisplayModel(Control control, ControlEventQueue dispatcher) {
            this.m_Control = control;
            this.m_Graphics = null;
            this.m_EventQueue = dispatcher;
            this.m_Renderer = new Renderer();

            // Initialize transform matrices to the identity matrix.
            this.m_PixelTransform = new Matrix();
            this.m_InkTransform = new Matrix();

            // Initialize the bounds to an empty rectangle to avoid NullReferenceExceptions.
            this.m_Bounds = Rectangle.Empty;

            // Default is to render real time ink.
            this.m_RenderLocalRealTimeInk = true;

            //Initialize Disposition to Instructor and display BG as default
            this.m_SheetDisposition = SheetDisposition.All | SheetDisposition.Background;
        }

        /// <summary>
        /// Instantiates a new <see cref="SlideDisplayModel"/>.
        /// </summary>
        /// <param name="control">
        /// The control that will be used for <see cref="CreateGraphics"/>; presumably, the <see cref="SlideViewer"/>
        /// on which the slide will be rendered.
        /// </param>
        public SlideDisplayModel( Graphics g, ControlEventQueue dispatcher ) {
            this.m_Control = null;
            this.m_Graphics = g;
            this.m_EventQueue = dispatcher;
            this.m_Renderer = new Renderer();

            // Initialize transform matrices to the identity matrix.
            this.m_PixelTransform = new Matrix();
            this.m_InkTransform = new Matrix();

            // Initialize the bounds to an empty rectangle to avoid NullReferenceExceptions.
            this.m_Bounds = Rectangle.Empty;

            // Default is to render real time ink.
            this.m_RenderLocalRealTimeInk = true;

            //Initialize Disposition to Instructor and display BG as default
            this.m_SheetDisposition = SheetDisposition.All | SheetDisposition.Background;
        }

        /// <summary>
        /// The rectangle enclosing the area of the screen on which the slide is to be rendered (in pixels).
        /// </summary>
        [Published] public Rectangle Bounds {
            get { return this.GetPublishedProperty("Bounds", ref this.m_Bounds); }
            set { this.SetPublishedProperty("Bounds", ref this.m_Bounds, value); }
        }

        /// <summary>
        /// The geometric transform by which all pixel-based slide rendering operations should be adjusted.
        /// </summary>
        /// <remarks>
        /// The transform will scale and translate the slide to fit within the <see cref="Bounds"/>.
        /// The pre-transform origin <c>(0,0)</c> is mapped to
        /// <c><see cref="Bounds"/>.<see cref="Rectangle.Location">Location</see></c>.
        /// </remarks>
        /// <seealso cref="Graphics.Transform"/>
        [Published] public Matrix PixelTransform {
            get { return this.GetPublishedProperty("PixelTransform", ref this.m_PixelTransform); }
            set {
                if(value == null) throw new NullReferenceException("The pixel transform cannot be null.");
                this.SetPublishedProperty("PixelTransform", ref this.m_PixelTransform, value);
            }
        }
        public bool IsSecondMonitorDisplay {
            get { return second_monitor_; }
            set { second_monitor_ = value; }
        }
        /// <summary>
        /// The geometric transform by which all ink rendering operations should be adjusted.
        /// </summary>
        /// <remarks>
        /// The transform's scaling factor is the same as that of <see cref="PixelTransform"/>,
        /// but the translation factor is in <em>HIMETRIC</em> units (adjusted by the DPI of the <see cref="Control"/>
        /// with which the <see cref="SlideDisplayModel"/> was constructed) instead of pixels.
        /// </remarks>
        /// <remarks>
        /// The transform will scale and translate the slide to fit within the <see cref="Bounds"/>.
        /// The pre-transform origin <c>(0,0)</c> is mapped to
        /// <c><see cref="Bounds"/>.<see cref="Rectangle.Location">Location</see></c>.
        /// </remarks>
        /// <seealso cref="Renderer.SetViewTransform"/>
        /// <seealso cref="TransformableDynamicRenderer.Transform"/>
        [Published] public Matrix InkTransform {
            get { return this.GetPublishedProperty("InkTransform", ref this.m_InkTransform); }
            set {
                if(value == null) throw new NullReferenceException("The ink transform cannot be null.");
                this.SetPublishedProperty("InkTransform", ref this.m_InkTransform, value);
            }
        }

        /// <summary>
        /// Indicates whether <see cref="RealTimeInkSheetRenderer"/>s should render all real-time ink
        /// from their associated <see cref="RealTimeInkSheetModel"/>s.
        /// </summary>
        /// <remarks>
        /// When the <see cref="RealTimeInkSheetModel"/>'s disposition contains <see cref="Disposition.Remote"/>,
        /// then <see cref="RealTimeInkSheetRenderer"/>s should render real-time ink regardless of
        /// the value of this property.
        /// </remarks>
        /// <remarks>
        /// The rational here is that a <see cref="SlideViewer"/> on which the user can draw ink
        /// will use a high-priority <see cref="TransformableDynamicRenderer"/> attached directly
        /// to the <see cref="RealTimeStylus"/>, making it unnecessary to render the ink twice.
        /// </remarks>
        [Published] public bool RenderLocalRealTimeInk {
            get { return this.GetPublishedProperty("RenderLocalRealTimeInk", ref this.m_RenderLocalRealTimeInk); }
            set { this.SetPublishedProperty("RenderLocalRealTimeInk", ref this.m_RenderLocalRealTimeInk, value); }
        }

        /// <summary>
        /// The current slide which is to be displayed.
        /// </summary>
        /// <remarks>
        /// Setting this property will not cause the <see cref="Invalidated"/> event to be fired,
        /// nor will it cause the <see cref="InkTransform"/>, <see cref="PixelTransform"/>, or
        /// <see cref="Bounds"/> to be recalculated.
        /// Such recalculations should be managed by the <see cref="SlideViewer"/> which created the
        /// <see cref="SlideDisplayModel"/>.
        /// </remarks>
        [Published] public SlideModel Slide {
            get { return this.GetPublishedProperty("Slide", ref this.m_Slide); }
            set { this.SetPublishedProperty("Slide", ref this.m_Slide, value); }
        }

        /// <summary>
        /// Filters the sheets which are displayed by the viewer according to their <see cref="SheetModel.Disposition"/>.
        /// </summary>
        /// <remarks>
        /// Setting this property will not cause the <see cref="Invalidated"/> event to be fired,
        /// nor will it cause the <see cref="InkTransform"/>, <see cref="PixelTransform"/>, or
        /// <see cref="Bounds"/> to be recalculated.
        /// Such recalculations should be managed by the <see cref="SlideViewer"/> which created the
        /// <see cref="SlideDisplayModel"/>.
        /// </remarks>
        [Published] public SheetDisposition SheetDisposition {
            get { return this.GetPublishedProperty("SheetDisposition", ref this.m_SheetDisposition); }
            set { this.SetPublishedProperty("SheetDisposition", ref this.m_SheetDisposition, value); }
        }

        /// <summary>
        /// Returns a <see cref="Graphics"/> object from the <see cref="Control"/> with which
        /// the <see cref="SlideDisplayModel"/> was constructed.
        /// </summary>
        /// <remarks>
        /// The returned <see cref="Graphics"/> object is unmodified from its original state.
        /// The caller must apply the appropriate clip and transform, using <see cref="Bounds"/>
        /// and <see cref="PixelTransform"/> as appropriate.
        /// </remarks>
        public Graphics CreateGraphics() {
            return this.m_Control.CreateGraphics();
        }

        public ControlEventQueue EventQueue {
            get { return this.m_EventQueue; }
        }

        /// <summary>
        /// Occurs when the rendered slide or sheets require repainting.
        /// </summary>
        public event InvalidateEventHandler Invalidated;

        /// <summary>
        /// Causes the <see cref="Invalidated"/> event to be fired.
        /// </summary>
        /// <param name="invalidRect">
        /// The area of the slide (relative to the slide's coordinate system)
        /// which needs to be repainted.
        /// </param>
        public void Invalidate() {
            // FIXME: How should an infinite rectangle be represented?  Need to pass the owners' bounds.
            this.Invalidate(Rectangle.Empty);
        }

        /// <summary>
        /// Causes the <see cref="Invalidated"/> event to be fired.
        /// </summary>
        /// <param name="invalidRect">
        /// The area of the slide (relative to the slide's coordinate system)
        /// which needs to be repainted.
        /// </param>
        public void Invalidate(Rectangle invalidRect) {
            this.OnInvalidated(new InvalidateEventArgs(invalidRect));
        }

        /// <summary>
        /// Fires the <see cref="Invalidated"/> event.
        /// </summary>
        /// <param name="args"></param>
        protected void OnInvalidated(InvalidateEventArgs args) {
            InvalidateEventHandler invalidated = this.Invalidated;
            if(invalidated != null)
                invalidated(this, args);
        }

        /// <summary>
        /// A helper function to compute the bounds and transforms necessary to display a slide in a <see cref="SlideViewer"/>.
        /// </summary>
        /// <remarks>
        /// This method does not modify the state of the <see cref="SlideDisplayModel"/>.  Callers must use the return values
        /// to set the state as they find appropriate.
        /// </remarks>
        ///
        /// <param name="sides">
        /// Specifies which sides of the slide will be scaled to match the sides of the <see cref="SlideViewer"/>'s bounds.
        /// <para>
        /// If <see cref="DockStyle.Left"/> or <see cref="DockStyle.Right"/>, then the slide's scaled height will be matched
        /// to that of <paramref name="bounds"/> (the <see cref="SlideViewer"/>'s bounds), and the width will be scaled
        /// such that the aspect ratio is maintained.
        /// </para>
        /// <para>
        /// If <see cref="DockStyle.Top"/> or <see cref="DockStyle.Bottom"/>, then the slide's scaled <em>width</em> will be matched
        /// to that of <paramref name="bounds"/> (the <see cref="SlideViewer"/>'s bounds), and the <em>height</em> will be scaled
        /// such that the aspect ratio is maintained.
        /// </para>
        /// <para>
        /// If <see cref="DockStyle.Fill"/>, then the slide will be scaled such that both the height and width fit within the
        /// <paramref name="bounds"/>.
        /// </para>
        /// <para>
        /// If <see cref="DockStyle.None"/>, then the slide will not be scaled at all.
        /// </para>
        /// </param>
        ///
        /// <param name="bounds">
        /// The "target" bounds to which the slide's computed bounds will be scaled to fill,
        /// according to the specified <see cref="DockStyle"/>.
        /// <para>
        /// If the <paramref name="bounds"/>'s offset (the upper-left corner of the <paramref name="bounds"/> rectangle)
        /// is not <c>(0,0)</c>, then the <paramref name="pixel"/> and <paramref name="ink"/> transforms
        /// will be translated by the slide's offset <em>before</em> scaling.  This allows the <see cref="SlideViewer"/>
        /// to specify an offset in pixels by which the entire scaled slide will be adjusted.
        /// </para>
        /// </param>
        ///
        /// <param name="zoom">
        /// The zoom factor by which to scale the slide, after fitting its unscaled bounds to the viewer.
        /// <seealso cref="SlideModel.Zoom"/>
        /// </param>
        ///
        /// <param name="slide">
        /// On input, the unscaled bounds of the slide.  On output, the computed bounds of the slide, scaled to fit
        /// in <see cref="bounds"/> according to the specified <see cref="DockStyle"/>, and translated
        /// to <c><paramref name="bounds"/>.<see cref="Rectangle.Location">Location</see></c>.
        /// <para>
        /// If the slide's offset (the upper-left corner of the <paramref name="slide"/> rectangle) is not
        /// <c>(0,0)</c> on input, then the <paramref name="pixel"/> and <paramref name="ink"/> transforms
        /// will be translated by the slide's offset <em>after</em> scaling, so that the slide's offset's coordinates are
        /// on the same scale as the rest of the slide.
        /// </para>
        /// <para>
        /// On output, this rectangle becomes suitable for use as the clipping rectangle when rendering the slide,
        /// as it will be scaled and translated to cover the exact area that will contain the slide (assuming all
        /// rendering is done using the appropriate transformations).
        /// </para>
        /// </param>
        ///
        /// <param name="pixel">
        /// On output, the transformation matrix that should be used when drawing the slide.
        /// <para>
        /// The scaling of the <paramref name="pixel"/> matrix is the same as that of the <paramref name="ink"/>
        /// matrix, but the translation factor will be in pixels.
        /// </para>
        /// </param>
        ///
        /// <param name="ink">
        /// On output, the transformation matrix that should be used when rendering the slide's ink.
        /// <para>
        /// The scaling of the <paramref name="ink"/> matrix is the same as that of the <paramref name="pixel"/>
        /// matrix, but the translation factor will be in <em>HIMETRIC</em> units.  This scale is influenced
        /// by the display DPI of the <see cref="Control"/> with which the <see cref="SlideDisplayModel"/>
        /// was constructed.
        /// <seealso cref="Renderer.PixelToInkSpace"/>
        /// <see cref="Control.CreateGraphics"/>
        /// </para>
        /// </param>
        public void FitSlideToBounds(DockStyle sides, Rectangle bounds, float zoom, ref Rectangle slide, out Matrix pixel, out Matrix ink) {
            if( this.m_Control != null )
                this.FitSlideToBounds( this.CreateGraphics(), sides, bounds, zoom, ref slide, out pixel, out ink );
            else
                this.FitSlideToBounds( this.m_Graphics, sides, bounds, zoom, ref slide, out pixel, out ink );
        }

        /// <summary>
        /// A helper function to compute the bounds and transforms necessary to display a slide in a <see cref="SlideViewer"/>.
        /// </summary>
        /// <remarks>
        /// This method does not modify the state of the <see cref="SlideDisplayModel"/>.  Callers must use the return values
        /// to set the state as they find appropriate.
        /// </remarks>
        ///
        /// <param name="sides">
        /// Specifies which sides of the slide will be scaled to match the sides of the <see cref="SlideViewer"/>'s bounds.
        /// <para>
        /// If <see cref="DockStyle.Left"/> or <see cref="DockStyle.Right"/>, then the slide's scaled height will be matched
        /// to that of <paramref name="bounds"/> (the <see cref="SlideViewer"/>'s bounds), and the width will be scaled
        /// such that the aspect ratio is maintained.
        /// </para>
        /// <para>
        /// If <see cref="DockStyle.Top"/> or <see cref="DockStyle.Bottom"/>, then the slide's scaled <em>width</em> will be matched
        /// to that of <paramref name="bounds"/> (the <see cref="SlideViewer"/>'s bounds), and the <em>height</em> will be scaled
        /// such that the aspect ratio is maintained.
        /// </para>
        /// <para>
        /// If <see cref="DockStyle.Fill"/>, then the slide will be scaled such that both the height and width fit within the
        /// <paramref name="bounds"/>.
        /// </para>
        /// <para>
        /// If <see cref="DockStyle.None"/>, then the slide will not be scaled at all.
        /// </para>
        /// </param>
        ///
        /// <param name="bounds">
        /// The "target" bounds to which the slide's computed bounds will be scaled to fill,
        /// according to the specified <see cref="DockStyle"/>.
        /// <para>
        /// If the <paramref name="bounds"/>'s offset (the upper-left corner of the <paramref name="bounds"/> rectangle)
        /// is not <c>(0,0)</c>, then the <paramref name="pixel"/> and <paramref name="ink"/> transforms
        /// will be translated by the slide's offset <em>before</em> scaling.  This allows the <see cref="SlideViewer"/>
        /// to specify an offset in pixels by which the entire scaled slide will be adjusted.
        /// </para>
        /// </param>
        ///
        /// <param name="zoom">
        /// The zoom factor by which to scale the slide, after fitting its unscaled bounds to the viewer.
        /// <seealso cref="SlideModel.Zoom"/>
        /// </param>
        ///
        /// <param name="slide">
        /// On input, the unscaled bounds of the slide.  On output, the computed bounds of the slide, scaled to fit
        /// in <see cref="bounds"/> according to the specified <see cref="DockStyle"/>, and translated
        /// to <c><paramref name="bounds"/>.<see cref="Rectangle.Location">Location</see></c>.
        /// <para>
        /// If the slide's offset (the upper-left corner of the <paramref name="slide"/> rectangle) is not
        /// <c>(0,0)</c> on input, then the <paramref name="pixel"/> and <paramref name="ink"/> transforms
        /// will be translated by the slide's offset <em>after</em> scaling, so that the slide's offset's coordinates are
        /// on the same scale as the rest of the slide.
        /// </para>
        /// <para>
        /// On output, this rectangle becomes suitable for use as the clipping rectangle when rendering the slide,
        /// as it will be scaled and translated to cover the exact area that will contain the slide (assuming all
        /// rendering is done using the appropriate transformations).
        /// </para>
        /// </param>
        ///
        /// <param name="pixel">
        /// On output, the transformation matrix that should be used when drawing the slide.
        /// <para>
        /// The scaling of the <paramref name="pixel"/> matrix is the same as that of the <paramref name="ink"/>
        /// matrix, but the translation factor will be in pixels.
        /// </para>
        /// </param>
        ///
        /// <param name="ink">
        /// On output, the transformation matrix that should be used when rendering the slide's ink.
        /// <para>
        /// The scaling of the <paramref name="ink"/> matrix is the same as that of the <paramref name="pixel"/>
        /// matrix, but the translation factor will be in <em>HIMETRIC</em> units.  This scale is influenced
        /// by the display DPI of the <see cref="Control"/> with which the <see cref="SlideDisplayModel"/>
        /// was constructed.
        /// <seealso cref="Renderer.PixelToInkSpace"/>
        /// <see cref="Control.CreateGraphics"/>
        /// </para>
        /// </param>
        public void FitSlideToBounds( Graphics g, DockStyle sides, Rectangle bounds, float zoom, ref Rectangle slide, out Matrix pixel, out Matrix ink ) {
            // Compute the scaling and translation matrix needed so that the slide fits within the SlideViewer.

            float sx = ((float)bounds.Width) / ((float)slide.Width);
            float sy = ((float)bounds.Height) / ((float)slide.Height);

            float scale;
            if( sides == DockStyle.Left || sides == DockStyle.Right ) {
                // Scale so the slide's height is equal to that of the viewer.
                scale = sy;
            } else if( sides == DockStyle.Top || sides == DockStyle.Bottom ) {
                // Scale so the slide's width is equal to that of the viewer.
                scale = sx;
            } else if( sides == DockStyle.Fill ) {
                // Scale so that both dimensions of the slide fit within the viewer,
                // keeping the aspect ratio intact.
                scale = Math.Min( sx, sy );
            } else {
                // Don't scale at all if DockStyle.None.
                scale = 1f;
            }

            Point[] translations = {
                // The first translation will take place before scaling (translating the "world").
                new Point(bounds.Left, bounds.Top),
                // The second translation takes place after scaling (translating the slide).
                new Point(-slide.X, -slide.Y),
            };

            // Compute the actual bounds of the slide, which may be smaller than the SlideViewer.
            slide = Rectangle.Round( new RectangleF( new PointF( bounds.Left, bounds.Top ),
                new SizeF( scale * slide.Width, scale * slide.Height ) ) );

            // Compute the pixel transformation.
            // Scale the transformation by the zoom factor.
            // Note: While the transformation is scaled, the new slide bounds are not.
            pixel = new Matrix();
            pixel.Translate( translations[0].X, translations[0].Y );
            pixel.Scale( scale * zoom, scale * zoom ); // Keep the aspect ration intact.
            pixel.Translate( translations[1].X, translations[1].Y );

            // Convert the two translations to ink coordinates.
            this.m_Renderer.PixelToInkSpace( g, ref translations );

            // Using the new translation factors, compute the ink transformation.
            ink = new Matrix();
            ink.Translate( translations[0].X, translations[0].Y );
            ink.Scale( scale * zoom, scale * zoom ); // Keep the aspect ration intact.
            ink.Translate( translations[1].X, translations[1].Y );
        }
    }
}
