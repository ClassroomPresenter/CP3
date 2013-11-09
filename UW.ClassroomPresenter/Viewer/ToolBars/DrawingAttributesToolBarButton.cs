// $Id: DrawingAttributesToolBarButton.cs 1921 2009-07-17 21:36:40Z lining $

using System;
using System.Collections;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Ink;

using UW.ClassroomPresenter.Model;
using UW.ClassroomPresenter.Model.Stylus;
using UW.ClassroomPresenter.Viewer.Text;
using UW.ClassroomPresenter.Model.Viewer;

namespace UW.ClassroomPresenter.Viewer.ToolBars {

    #region DrawingAttributesToolBarButton

    /// <summary>
    /// A ToolBarButton that allows the user to select a stylus.
    /// </summary>
    internal class DrawingAttributesToolBarButton : ToolStripButton {
        /// <summary>
        /// The event queue
        /// </summary>
        private readonly ControlEventQueue m_EventQueue;
        /// <summary>
        /// Drawing attribute change dispatcher
        /// </summary>
        private readonly EventQueue.PropertyEventDispatcher m_DrawingAttributesChangedDispatcher;
        /// <summary>
        /// Stylus change dispatcher
        /// </summary>
        private readonly EventQueue.PropertyEventDispatcher m_StylusChangedDispatcher;

        private readonly EventQueue.PropertyEventDispatcher m_ColorSetChangedDispatcher;

        private readonly EventQueue.PropertyEventDispatcher m_PenWidthChangedDispatcher;

        private readonly EventQueue.PropertyEventDispatcher m_HLWidthChangedDispatcher;

        /// <summary>
        /// Keeps track of different styluses
        /// </summary>
        private readonly Hashtable m_Table;
        /// <summary>
        /// The model
        /// </summary>
        private PresenterModel m_Model;

        /// <summary>
        /// The current stylus
        /// </summary>
        private StylusModel m_CurrentStylus;
        /// <summary>
        /// The current stylus entry
        /// </summary>
        private StylusEntry m_CurrentStylusEntry;

        /// <summary>
        /// Template button image
        /// </summary>
        private Bitmap templateImage;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="dispatcher">The event queue</param>
        /// <param name="entries">Array of StylusEntries</param>
        /// <param name="model">The presenter model</param>
        /// <param name="parent">The parent ToolStrip</param>
        public DrawingAttributesToolBarButton(ControlEventQueue dispatcher, StylusEntry[] entries, PresenterModel model, ToolStrip parent) {
            this.m_EventQueue = dispatcher;
            this.m_Table = new Hashtable();
            this.m_Model = model;
            this.Parent = parent;

            this.templateImage = new Bitmap( Image.FromStream( this.GetType().Assembly.GetManifestResourceStream( "UW.ClassroomPresenter.Viewer.ToolBars.Icons.buttontemplate.png" ) ) );

            foreach(StylusEntry entry in entries)
                this.m_Table.Add(entry.Stylus, entry);

            this.m_DrawingAttributesChangedDispatcher = new EventQueue.PropertyEventDispatcher(this.m_EventQueue, new PropertyEventHandler(this.HandleDrawingAttributesChanged));
            this.m_StylusChangedDispatcher = new EventQueue.PropertyEventDispatcher(this.m_EventQueue, new PropertyEventHandler(this.HandleStylusChanged));
            this.m_ColorSetChangedDispatcher = new EventQueue.PropertyEventDispatcher(this.m_EventQueue, new PropertyEventHandler(this.HandleColorSetChanged));
            this.m_PenWidthChangedDispatcher = new EventQueue.PropertyEventDispatcher(this.m_EventQueue, new PropertyEventHandler(this.HandlePenWidthChanged));
            this.m_HLWidthChangedDispatcher = new EventQueue.PropertyEventDispatcher(this.m_EventQueue, new PropertyEventHandler(this.HandleHLWidthChanged));

            foreach(StylusModel stylus in this.m_Table.Keys)
                stylus.Changed["DrawingAttributes"].Add(this.m_DrawingAttributesChangedDispatcher.Dispatcher);
            this.m_Model.Changed["Stylus"].Add(this.m_StylusChangedDispatcher.Dispatcher);
            this.m_Model.ViewerState.Changed["UseLightColorSet"].Add(this.m_ColorSetChangedDispatcher.Dispatcher);
            this.m_Model.ViewerState.Changed["DefaultPenWidth"].Add(this.m_PenWidthChangedDispatcher.Dispatcher);
            this.m_Model.ViewerState.Changed["DefaultHLWidth"].Add(this.m_HLWidthChangedDispatcher.Dispatcher);

            // Initialize the Pushed and Enabled states.
            // Note: HandleStylusChanged invokes HandleDrawingAttributesChanged.
            this.m_StylusChangedDispatcher.Dispatcher(this.m_Model, null);

            this.m_ColorSetChangedDispatcher.Dispatcher(this, null);
            this.m_PenWidthChangedDispatcher.Dispatcher(this, null);
        }

        /// <summary>
        /// Dispose of the resources
        /// </summary>
        /// <param name="disposing">Truely dispose</param>
        protected override void Dispose(bool disposing) {
            try {
                if(disposing) {
                    foreach(StylusModel stylus in this.m_Table.Keys)
                        stylus.Changed["DrawingAttributes"].Remove(this.m_DrawingAttributesChangedDispatcher.Dispatcher);
                    this.m_Model.Changed["Stylus"].Remove(this.m_StylusChangedDispatcher.Dispatcher);
                    this.m_Model.ViewerState.Changed["UseLightColorSet"].Remove(this.m_ColorSetChangedDispatcher.Dispatcher);
                    this.templateImage.Dispose();
                }
            } finally {
                base.Dispose(disposing);
            }
        }

        /// <summary>
        /// The current stylus entry
        /// </summary>
        protected StylusEntry CurrentStylusEntry {
            get { return this.m_CurrentStylusEntry; }
            set {
                using(Synchronizer.Lock(this)) {
                    bool update = (this.m_CurrentStylus != null
                        && this.m_Table.ContainsKey(this.m_CurrentStylus)
                        && (this.m_CurrentStylusEntry == null
                            || ((StylusEntry) this.m_Table[this.m_CurrentStylus]).DrawingAttributes == this.m_CurrentStylusEntry.DrawingAttributes)
                        && this.m_CurrentStylusEntry != value);

                    this.m_CurrentStylusEntry = value;

                    this.ToolTipText = value != null ? value.ToolTipText : null;

                    this.UpdateBitmapAtImageIndex();
                    if(this.Checked && update)
                        this.OnClick(new EventArgs());
                }
            }
        }

        /// <summary>
        /// Update the button bitmap because the color changed
        /// </summary>
        protected internal virtual void UpdateBitmapAtImageIndex() {
            // Get the bitmap on which we will draw an icon.
            Image image;

            if (this.Parent == null || ((ToolStrip)this.Parent).ImageList == null)
                return;

            if (this.ImageIndex >= 0)
                image = this.Parent.ImageList.Images[this.ImageIndex];
            else
                image = this.Image;
            if( image.Width != templateImage.Width ||
                image.Height != templateImage.Height ||
                image.PixelFormat != templateImage.PixelFormat ) {

                Graphics g = Graphics.FromImage( image );
                g.Clear( Color.Transparent );

                // Draw the icon as a 3D-ish sphere.
                // FIXME: Use other attributes of DrawingAttributes besides color and transparency.

                // Srink the circle by -1 to compensate for pixel model, and by -3x-2 more
                // to compensate for ToolBarButton border.
                GraphicsPath ellipse = new GraphicsPath();
                ellipse.AddEllipse( new Rectangle( 0, 0, image.Size.Width - 4, image.Size.Height - 3 ) );

                // We'll now need this.DrawingAttributes, so lock to make sure it isn't changed unexpectedly.
                // Keep the lock until after we're done setting the bitmap to ensure it doesn't get out of sync
                // with the DrawingAttributes via a race condition.

                // Define the gradient to be light-colored in the middle and slightly off-center.
                if( this.CurrentStylusEntry != null ) {
                    PathGradientBrush gradient = new PathGradientBrush( ellipse );
                    gradient.CenterColor = Color.WhiteSmoke;
                    float sqrt2 = (float)Math.Sqrt( 2d );
                    gradient.CenterPoint = new PointF( gradient.CenterPoint.X / sqrt2, gradient.CenterPoint.Y / sqrt2 );
                    Color color = Color.FromArgb( byte.MaxValue - this.CurrentStylusEntry.DrawingAttributes.Transparency,
                        this.CurrentStylusEntry.DrawingAttributes.Color );
                    gradient.SurroundColors = new Color[] { color };
                    gradient.SetSigmaBellShape( 1f, 0.4f );

                    // Fill with a white background first to make the transparency look better.
                    // TODO: Maybe fill with a pattern to make the transparency more obvious.
                    g.FillPath( new SolidBrush( Color.White ), ellipse );

                    // Now fill the gradient.
                    g.FillPath( gradient, ellipse );
                }

                // Surround it with a thin black border.  In the unlikely case
                // that the current DrawingAttributes is null, this is all that will be visible.
                g.DrawPath( new Pen( Color.Black, 0.1f ), ellipse );

                // Force the button to reload its image (even though it's probably the same object as before).
                this.Parent.ImageList.Images[this.ImageIndex] = image;
            } else {
                // Sanity Check
                if( this.CurrentStylusEntry != null ) {
                    Color basecolor = this.CurrentStylusEntry.DrawingAttributes.Color;
                    // Draw each pixel in the image
                    for( int y = 0; y < this.templateImage.Height; y++ ) {
                        for( int x = 0; x < this.templateImage.Width; x++ ) {
                            Color col = templateImage.GetPixel( x, y );
                            // Check if this pixel is transparent or not
                            if( col.A != 0 ) {
                                int r = ((int)col.R + (int)basecolor.R) - 119;
                                r = Math.Min( 255, Math.Max( 0, r ) );
                                int g = ((int)col.G + (int)basecolor.G) - 119;
                                g = Math.Min( 255, Math.Max( 0, g ) );
                                int b = ((int)col.B + (int)basecolor.B) - 119;
                                b = Math.Min( 255, Math.Max( 0, b ) );

                                ((Bitmap)image).SetPixel( x, y,
                                                          Color.FromArgb( col.A, r, g, b ) );
                            } else {
                                ((Bitmap)image).SetPixel( x, y,
                                                          Color.FromArgb( 0, 0, 0, 0 ) );
                            }
                        }
                    }
                    // Replace the existing image with this one
                    Misc.ImageListHelper.Replace( (Bitmap)image, this.ImageIndex, this.Parent.ImageList );
                    this.Image = image;
                    this.Invalidate();
                }
            }
        }

        /// <summary>
        /// Handle the button being clicked
        /// </summary>
        /// <param name="args">The event args</param>
        protected override void OnClick( EventArgs args ) {
            if( this.m_CurrentStylus != null && this.CurrentStylusEntry != null ) {
                using( Synchronizer.Lock( this.m_CurrentStylus.SyncRoot ) ) {
                    this.m_CurrentStylus.DrawingAttributes = this.CurrentStylusEntry.DrawingAttributes.Clone();
                }
            }
            base.OnClick( args );
        }

        /// <summary>
        /// Handle the drawing attributes changing
        /// </summary>
        /// <param name="sender">The event sender</param>
        /// <param name="args">The event arguments</param>
        private void HandleDrawingAttributesChanged(object sender, PropertyEventArgs args) {
            if(sender == this.m_CurrentStylus && this.m_CurrentStylus != null) {
                // Update the bitmap via the CurrentStylusEntry setter.
                this.CurrentStylusEntry = this.m_Table[sender] as StylusEntry;

                using(Synchronizer.Lock(this.m_CurrentStylus.SyncRoot)) {
                    this.Checked = this.CurrentStylusEntry == null ? false
                        : DrawingAttributesEquals(this.m_CurrentStylus.DrawingAttributes, this.CurrentStylusEntry.DrawingAttributes);
                }
            } else {
                this.Checked = false;
            }
        }

        /// <summary>
        /// Handle the stylus changing
        /// </summary>
        /// <param name="sender">The event sender</param>
        /// <param name="args">The event arguments</param>
        private void HandleStylusChanged(object sender, PropertyEventArgs args) {
            bool enable;
            Color color;

            using(Synchronizer.Lock(this.m_Model.SyncRoot)) {
                if(this.m_Model.Stylus != null && this.m_Table.ContainsKey(this.m_Model.Stylus)) {
                    using( Synchronizer.Lock( this.m_Model.Stylus.SyncRoot ) ) {
                        color = this.m_Model.Stylus.DrawingAttributes.Color;
                    }

                    using (Synchronizer.Lock(this.m_Model.ViewerState.SyncRoot)) {
                        if (this.m_Model.ViewerState.UseLightColorSet) {
                            if (color == Color.Black) {
                                color = Color.White;
                            } else if (color == Color.Red) {
                                color = Color.Pink;
                            } else if (color == Color.Green) {
                                color = Color.LightGreen;
                            } else if (color == Color.Blue) {
                                color = Color.LightBlue;
                            }
                        } else {
                            if (color == Color.White) {
                                color = Color.Black;
                            } else if (color == Color.LightBlue) {
                                color = Color.Blue;
                            } else if (color == Color.Pink) {
                                color = Color.Red;
                            } else if (color == Color.LightGreen) {
                                color = Color.Green;
                            }
                        }
                    }

                    using( Synchronizer.Lock( this.m_Model.Stylus.SyncRoot ) ) {
                        if (color != this.m_Model.Stylus.DrawingAttributes.Color)
                            this.m_Model.Stylus.DrawingAttributes.Color = color;
                    }
                    // In addition to enabling the button, update the current stylus
                    // and DrawingAttributes (possibly causing redrawing the bitmap).
                    this.m_CurrentStylus = this.m_Model.Stylus;
                    this.HandleDrawingAttributesChanged(this.m_Model.Stylus, null);
                    enable = true;
                } else {
                    enable = false;
                }
            }

            this.Enabled = enable;
        }
   
        /// <summary>
        /// Change the color set the button based on the property option
        /// </summary>
        /// <param name="e"></param>
        public void HandleColorSetChanged(object sender, PropertyEventArgs args) {
            Color color;
            string tooltip = "";

            foreach (StylusEntry stylus in this.m_Table.Values) {
                if (stylus.PenType == PenType.Pen || stylus.PenType == PenType.Text) {
                    color = stylus.DrawingAttributes.Color;
                
                    using (Synchronizer.Lock(this.m_Model.ViewerState.SyncRoot)) {
                        if (this.m_Model.ViewerState.UseLightColorSet) {
                            if (color == Color.Black) {
                                color = Color.White;
                                tooltip = (stylus.PenType == PenType.Pen) ? Strings.SelectWhitePen : Strings.WhiteText;
                            } else if (color == Color.Red) {
                                color = Color.Pink;
                                tooltip = (stylus.PenType == PenType.Pen) ? Strings.SelectPinkPen : Strings.PinkText;
                            } else if (color == Color.Green) {
                                color = Color.LightGreen;
                                tooltip = (stylus.PenType == PenType.Pen) ? Strings.SelectLightgreenPen : Strings.LightgreenText;
                            } else if (color == Color.Blue) {
                                color = Color.LightBlue;
                                tooltip = (stylus.PenType == PenType.Pen) ? Strings.SelectLightbluePen : Strings.LightblueText;
                            }
                        } else {
                            if (color == Color.White) {
                                color = Color.Black;
                                tooltip = (stylus.PenType == PenType.Pen) ? Strings.SelectBlackPen : Strings.BlackText;
                            } else if (color == Color.LightBlue) {
                                color = Color.Blue;
                                tooltip = (stylus.PenType == PenType.Pen) ? Strings.SelectBluePen: Strings.BlueText;
                            } else if (color == Color.Pink) {
                                color = Color.Red;
                                tooltip = (stylus.PenType == PenType.Pen) ? Strings.SelectRedPen : Strings.RedText;
                            } else if (color == Color.LightGreen) {
                                color = Color.Green;
                                tooltip = (stylus.PenType == PenType.Pen) ? Strings.SelectGreenPen : Strings.GreenText;
                            }
                        }
                    }
                    
                    if (color != stylus.DrawingAttributes.Color) {
                        stylus.DrawingAttributes.Color = color;
                        stylus.ToolTipText = string.Copy(tooltip);
                    }
                }
            }

            if (this.m_CurrentStylus != null) {
                using( Synchronizer.Lock( this.m_CurrentStylus.SyncRoot ) ) {
                    color = this.m_CurrentStylus.DrawingAttributes.Color;
                }

                using (Synchronizer.Lock(this.m_Model.ViewerState.SyncRoot)) {
                    if (this.m_Model.ViewerState.UseLightColorSet) {
                        if (color == Color.Black) {
                            color = Color.White;
                        } else if (color == Color.Red) {
                            color = Color.Pink;
                        } else if (color == Color.Green) {
                            color = Color.LightGreen;
                        } else if (color == Color.Blue) {
                            color = Color.LightBlue;
                        }
                    } else {
                        if (color == Color.White) {
                            color = Color.Black;
                        } else if (color == Color.LightBlue) {
                            color = Color.Blue;
                        } else if (color == Color.Pink) {
                            color = Color.Red;
                        } else if (color == Color.LightGreen) {
                            color = Color.Green;
                        }
                    }
                }

                using( Synchronizer.Lock( this.m_CurrentStylus.SyncRoot ) ) {
                    if (color != this.m_CurrentStylus.DrawingAttributes.Color)
                        this.m_CurrentStylus.DrawingAttributes.Color = color;
                }
            }
            this.HandleDrawingAttributesChanged(this.m_CurrentStylus, null);
        }

        /// <summary>
        /// Change the width of pen
        /// </summary>
        /// <param name="e"></param>
        public void HandlePenWidthChanged(object sender, PropertyEventArgs args) {
            int width;

            using (Synchronizer.Lock(this.m_Model.ViewerState.SyncRoot)) {
                width = this.m_Model.ViewerState.DefaultPenWidth;
            }

            foreach (StylusEntry stylus in this.m_Table.Values) {
                if (stylus.PenType == PenType.Pen && stylus.DrawingAttributes.Width != width) {
                    stylus.DrawingAttributes.Width = width;
                    stylus.DrawingAttributes.Height = width;
                }

            }

            this.HandleDrawingAttributesChanged(this.m_CurrentStylus, null);
        }

        /// <summary>
        /// Change the width of Highlighter
        /// </summary>
        public void HandleHLWidthChanged(object sender, PropertyEventArgs args){
            int width;

            using(Synchronizer.Lock(this.m_Model.ViewerState.SyncRoot))
            {
                width=this.m_Model.ViewerState.DefaultHLWidth;
            }

            foreach(StylusEntry stylus in this.m_Table.Values)
            {
                if (stylus.PenType == PenType.Hightlighter && stylus.DrawingAttributes.Width != width) {
                    stylus.DrawingAttributes.Width = width;
                    stylus.DrawingAttributes.Height = width;
                }
            }

            this.HandleDrawingAttributesChanged(this.m_CurrentStylus, null);
        }

        /// <summary>
        /// Compares two sets of drawing attributes to determine if they're equal
        /// </summary>
        /// <param name="left">The first set</param>
        /// <param name="right">The second set</param>
        /// <returns></returns>
        public static bool DrawingAttributesEquals(DrawingAttributes left, DrawingAttributes right) {
            
            if(left.AntiAliased != right.AntiAliased) return false;
            if(!left.Color.Equals(right.Color)) return false;
            if(left.ExtendedProperties.Count != right.ExtendedProperties.Count) return false;
            foreach(ExtendedProperty prop in left.ExtendedProperties) {
                if(!right.ExtendedProperties.DoesPropertyExist(prop.Id)) return false;
                object data = right.ExtendedProperties[prop.Id].Data;
                if(data == null && prop.Data != null || !data.Equals(prop.Data))
                    return false;
            }
            if(left.FitToCurve != right.FitToCurve) return false;
            if(left.Height != right.Height) return false;
            if(left.IgnorePressure != right.IgnorePressure) return false;
            if(left.PenTip != right.PenTip) return false;
            if(left.RasterOperation != right.RasterOperation) return false;
            if(left.Transparency != right.Transparency) return false;
            if(left.Width != right.Width) return false;
            return true;
        }
    }

    #endregion


    public enum PenType
    {
        Pen = 1,
        Hightlighter = 2,
        Text = 3,
        Unknown = 4,
    }

    /// <summary>
    /// Class representing a stylus entry
    /// </summary>
    public class StylusEntry
    {
        public readonly DrawingAttributes DrawingAttributes;
        public readonly PenType PenType;

        public readonly StylusModel Stylus;
        public string ToolTipText;

        public StylusEntry(DrawingAttributes atts, StylusModel stylus, string toolTipText)
        {
            if (atts == null)
                throw new ArgumentNullException("atts");
            if (stylus == null)
                throw new ArgumentNullException("stylus");

            this.DrawingAttributes = atts;
            this.Stylus = stylus;
            this.ToolTipText = toolTipText;
            this.PenType = PenType.Unknown;
        }

        public StylusEntry(DrawingAttributes atts, StylusModel stylus, string toolTipText, PenType pt)
        {
            if (atts == null)
                throw new ArgumentNullException("atts");
            if (stylus == null)
                throw new ArgumentNullException("stylus");

            this.DrawingAttributes = atts;
            this.Stylus = stylus;
            this.ToolTipText = toolTipText;
            this.PenType = pt;
        }
    }

    #region CustomDrawingAttributesDropDownButton

    /// <summary>
    /// Allows the user to define a custom stylus.
    /// FIXME: add UI for more attributes than just the color.
    /// </summary>
    internal class CustomDrawingAttributesDropDownButton : ToolStripSplitButton {
        /// <summary>
        /// The event queue
        /// </summary>
        private readonly ControlEventQueue m_EventQueue;
        /// <summary>
        /// The drawing attributes changed dispatcher
        /// </summary>
        private readonly EventQueue.PropertyEventDispatcher m_DrawingAttributesChangedDispatcher;
        /// <summary>
        /// The stylus changed dispatcher
        /// </summary>
        private readonly EventQueue.PropertyEventDispatcher m_StylusChangedDispatcher;
        /// <summary>
        /// The PenState changed dispatcher
        /// </summary>
        private readonly EventQueue.PropertyEventDispatcher m_PenStateChangedDispatcher;

        /// <summary>
        /// Hashtable of past stylus entries
        /// </summary>
        private readonly Hashtable m_Table;
        /// <summary>
        /// The presenter model
        /// </summary>
        private PresenterModel m_Model;

        /// <summary>
        /// The current pen stylus
        /// </summary>
        private StylusModel m_CurrentStylus;
        /// <summary>
        /// The current stylus entry
        /// </summary>
        private StylusEntry m_CurrentStylusEntry;

        /// <summary>
        /// Template button image
        /// </summary>
        private Bitmap templateImage;

        /// <summary>
        /// Determines if the button should be checked or not
        /// </summary>
        private bool m_Checked = false;
        public bool Checked {
            get { return this.m_Checked; }
            set { this.m_Checked = value; }
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="dispatcher">The event queue</param>
        /// <param name="entries">The stylus entries</param>
        /// <param name="model">The presenter model</param>
        /// <param name="parent">The parent ToolStrip</param>
        public CustomDrawingAttributesDropDownButton( ControlEventQueue dispatcher, StylusEntry[] entries, PresenterModel model, ToolStrip parent ) {
            this.m_EventQueue = dispatcher;
            this.m_Table = new Hashtable();
            this.m_Model = model;
            this.Parent = parent;

            this.templateImage = new Bitmap( Image.FromStream( this.GetType().Assembly.GetManifestResourceStream( "UW.ClassroomPresenter.Viewer.ToolBars.Icons.buttontemplate.png" ) ) );

            foreach( StylusEntry entry in entries )
                this.m_Table.Add( entry.Stylus, entry );

            this.m_DrawingAttributesChangedDispatcher = new EventQueue.PropertyEventDispatcher( this.m_EventQueue, new PropertyEventHandler( this.HandleDrawingAttributesChanged ) );
            this.m_StylusChangedDispatcher = new EventQueue.PropertyEventDispatcher( this.m_EventQueue, new PropertyEventHandler( this.HandleStylusChanged ) );
            this.m_PenStateChangedDispatcher = new EventQueue.PropertyEventDispatcher( this.m_EventQueue, new PropertyEventHandler( this.HandlePenStateChanged ) );

            foreach( StylusModel stylus in this.m_Table.Keys )
                stylus.Changed["DrawingAttributes"].Add( this.m_DrawingAttributesChangedDispatcher.Dispatcher );
            this.m_Model.Changed["Stylus"].Add( this.m_StylusChangedDispatcher.Dispatcher );
            this.m_Model.PenState.Changed["PenRasterOperation"].Add(this.m_PenStateChangedDispatcher.Dispatcher);
            this.m_Model.PenState.Changed["PenTip"].Add(this.m_PenStateChangedDispatcher.Dispatcher);
            this.m_Model.PenState.Changed["PenColor"].Add(this.m_PenStateChangedDispatcher.Dispatcher);
            this.m_Model.PenState.Changed["PenWidth"].Add(this.m_PenStateChangedDispatcher.Dispatcher);
            this.m_Model.PenState.Changed["PenHeight"].Add(this.m_PenStateChangedDispatcher.Dispatcher);
            this.m_Model.PenState.Changed["HLRasterOperation"].Add(this.m_PenStateChangedDispatcher.Dispatcher);
            this.m_Model.PenState.Changed["HLTip"].Add(this.m_PenStateChangedDispatcher.Dispatcher);
            this.m_Model.PenState.Changed["HLColor"].Add(this.m_PenStateChangedDispatcher.Dispatcher);
            this.m_Model.PenState.Changed["HLWidth"].Add(this.m_PenStateChangedDispatcher.Dispatcher);
            this.m_Model.PenState.Changed["HLHeight"].Add(this.m_PenStateChangedDispatcher.Dispatcher);


            // Initialize the Pushed and Enabled states.
            // Note: HandleStylusChanged invokes HandleDrawingAttributesChanged.
            this.m_StylusChangedDispatcher.Dispatcher( this.m_Model, null );
            this.m_PenStateChangedDispatcher.Dispatcher( this.m_Model, null );
        }

        /// <summary>
        /// Dispose of all the resources
        /// </summary>
        /// <param name="disposing">True if really disposing</param>
        protected override void Dispose( bool disposing ) {
            try {
                if( disposing ) {
                    foreach( StylusModel stylus in this.m_Table.Keys )
                        stylus.Changed["DrawingAttributes"].Remove( this.m_DrawingAttributesChangedDispatcher.Dispatcher );
                    this.m_Model.Changed["Stylus"].Remove( this.m_StylusChangedDispatcher.Dispatcher );
                    this.m_Model.PenState.Changed["PenRasterOperation"].Remove(this.m_PenStateChangedDispatcher.Dispatcher);
                    this.m_Model.PenState.Changed["PenTip"].Remove(this.m_PenStateChangedDispatcher.Dispatcher);
                    this.m_Model.PenState.Changed["PenColor"].Remove(this.m_PenStateChangedDispatcher.Dispatcher);
                    this.m_Model.PenState.Changed["PenWidth"].Remove(this.m_PenStateChangedDispatcher.Dispatcher);
                    this.m_Model.PenState.Changed["PenHeight"].Remove(this.m_PenStateChangedDispatcher.Dispatcher);
                    this.m_Model.PenState.Changed["HLRasterOperation"].Remove(this.m_PenStateChangedDispatcher.Dispatcher);
                    this.m_Model.PenState.Changed["HLTip"].Remove(this.m_PenStateChangedDispatcher.Dispatcher);
                    this.m_Model.PenState.Changed["HLColor"].Remove(this.m_PenStateChangedDispatcher.Dispatcher);
                    this.m_Model.PenState.Changed["HLWidth"].Remove(this.m_PenStateChangedDispatcher.Dispatcher);
                    this.m_Model.PenState.Changed["HLHeight"].Remove(this.m_PenStateChangedDispatcher.Dispatcher);
                    this.templateImage.Dispose();
                }
            } finally {
                base.Dispose( disposing );
            }
        }

        /// <summary>
        /// The current stylus entry
        /// </summary>
        protected StylusEntry CurrentStylusEntry {
            get { return this.m_CurrentStylusEntry; }
            set {
                using( Synchronizer.Lock( this ) ) {
                    bool update = (this.m_CurrentStylus != null
                        && this.m_Table.ContainsKey( this.m_CurrentStylus )
                        && (this.m_CurrentStylusEntry == null
                            || ((StylusEntry)this.m_Table[this.m_CurrentStylus]).DrawingAttributes == this.m_CurrentStylusEntry.DrawingAttributes)
                        && this.m_CurrentStylusEntry != value);

                    this.m_CurrentStylusEntry = value;

                    this.ToolTipText = value != null ? value.ToolTipText : null;

                    this.UpdateBitmapAtImageIndex();
                    if( this.Checked && update )
                        this.OnClick( new EventArgs() );
                }
            }
        }

        /// <summary>
        /// Update the image bitmap because the color changed
        /// </summary>
        protected internal virtual void UpdateBitmapAtImageIndex() {
            // Get the bitmap on which we will draw an icon.
            Image image;

            if (this.Parent == null || this.Parent.ImageList == null)
                return;

            if (this.ImageIndex >= 0)
                image = this.Parent.ImageList.Images[this.ImageIndex];
            else
                image = this.Image;
            if( image.Width != templateImage.Width ||
                image.Height != templateImage.Height ||
                image.PixelFormat != templateImage.PixelFormat ) {

                Graphics g = Graphics.FromImage( image );
                g.Clear( Color.Transparent );

                // Draw the icon as a 3D-ish sphere.
                // FIXME: Use other attributes of DrawingAttributes besides color and transparency.

                // Srink the circle by -1 to compensate for pixel model, and by -3x-2 more
                // to compensate for ToolBarButton border.
                GraphicsPath ellipse = new GraphicsPath();
                ellipse.AddEllipse( new Rectangle( 0, 0, image.Size.Width - 4, image.Size.Height - 3 ) );

                // We'll now need this.DrawingAttributes, so lock to make sure it isn't changed unexpectedly.
                // Keep the lock until after we're done setting the bitmap to ensure it doesn't get out of sync
                // with the DrawingAttributes via a race condition.

                // Define the gradient to be light-colored in the middle and slightly off-center.
                if( this.CurrentStylusEntry != null ) {
                    PathGradientBrush gradient = new PathGradientBrush( ellipse );
                    gradient.CenterColor = Color.WhiteSmoke;
                    float sqrt2 = (float)Math.Sqrt( 2d );
                    gradient.CenterPoint = new PointF( gradient.CenterPoint.X / sqrt2, gradient.CenterPoint.Y / sqrt2 );
                    Color color = Color.FromArgb( byte.MaxValue - this.CurrentStylusEntry.DrawingAttributes.Transparency,
                        this.CurrentStylusEntry.DrawingAttributes.Color );
                    gradient.SurroundColors = new Color[] { color };
                    gradient.SetSigmaBellShape( 1f, 0.4f );

                    // Fill with a white background first to make the transparency look better.
                    // TODO: Maybe fill with a pattern to make the transparency more obvious.
                    g.FillPath( new SolidBrush( Color.White ), ellipse );

                    // Now fill the gradient.
                    g.FillPath( gradient, ellipse );
                }

                // Surround it with a thin black border.  In the unlikely case
                // that the current DrawingAttributes is null, this is all that will be visible.
                g.DrawPath( new Pen( Color.Black, 0.1f ), ellipse );

                // Force the button to reload its image (even though it's probably the same object as before).
                this.Parent.ImageList.Images[this.ImageIndex] = image;
            } else {
                // Sanity Check
                if( this.CurrentStylusEntry != null ) {
                    Color basecolor = this.CurrentStylusEntry.DrawingAttributes.Color;
                    // Draw each pixel in the image
                    for( int y = 0; y < this.templateImage.Height; y++ ) {
                        for( int x = 0; x < this.templateImage.Width; x++ ) {
                            Color col = templateImage.GetPixel( x, y );
                            // Check if this pixel is transparent or not
                            if( col.A != 0 ) {
                                int r = ((int)col.R + (int)basecolor.R) - 119;
                                r = Math.Min( 255, Math.Max( 0, r ) );
                                int g = ((int)col.G + (int)basecolor.G) - 119;
                                g = Math.Min( 255, Math.Max( 0, g ) );
                                int b = ((int)col.B + (int)basecolor.B) - 119;
                                b = Math.Min( 255, Math.Max( 0, b ) );

                                ((Bitmap)image).SetPixel( x, y,
                                                          Color.FromArgb( col.A, r, g, b ) );
                            } else {
                                ((Bitmap)image).SetPixel( x, y,
                                                          Color.FromArgb( 0, 0, 0, 0 ) );
                            }
                        }
                    }
                    // Replace the existing image with this one
                    Misc.ImageListHelper.Replace( (Bitmap)image, this.ImageIndex, this.Parent.ImageList );
                    this.Image = image;
                    this.Invalidate();
                }
            }
        }

        /// <summary>
        /// Handle the button being clicked
        /// </summary>
        /// <param name="args">The event args</param>
        protected override void OnClick( EventArgs args ) {
            if( this.m_CurrentStylus != null && this.CurrentStylusEntry != null ) {
                using( Synchronizer.Lock( this.m_CurrentStylus.SyncRoot ) ) {
                    this.m_CurrentStylus.DrawingAttributes = this.CurrentStylusEntry.DrawingAttributes.Clone();
                }
            }
            base.OnClick( args );
        }

        /// <summary>
        /// Handle the drawing attributes changing
        /// </summary>
        /// <param name="sender">The event sender</param>
        /// <param name="args">The event args</param>
        private void HandleDrawingAttributesChanged( object sender, PropertyEventArgs args ) {
            if( sender == this.m_CurrentStylus && this.m_CurrentStylus != null ) {
                // Update the bitmap via the CurrentStylusEntry setter.
                this.CurrentStylusEntry = this.m_Table[sender] as StylusEntry;

                using( Synchronizer.Lock( this.m_CurrentStylus.SyncRoot ) ) {
                    this.Checked = this.CurrentStylusEntry == null ? false
                        : DrawingAttributesToolBarButton.DrawingAttributesEquals( this.m_CurrentStylus.DrawingAttributes, this.CurrentStylusEntry.DrawingAttributes );
                }
            } else {
                this.Checked = false;
            }
        }

        /// <summary>
        /// Handle the stylus changing
        /// </summary>
        /// <param name="sender">The event sender</param>
        /// <param name="args">The event args</param>
        private void HandleStylusChanged( object sender, PropertyEventArgs args ) {
            bool enable;

            using( Synchronizer.Lock( this.m_Model.SyncRoot ) ) {
                if( this.m_Model.Stylus != null && this.m_Table.ContainsKey( this.m_Model.Stylus ) ) {
                    // In addition to enabling the button, update the current stylus
                    // and DrawingAttributes (possibly causing redrawing the bitmap).
                    if (m_Model.Stylus is PenStylusModel) {
                        this.m_CurrentStylus = (PenStylusModel)this.m_Model.Stylus;
                    } else {
                        this.m_CurrentStylus = (TextStylusModel)this.m_Model.Stylus;
                    }
                    this.HandleDrawingAttributesChanged( this.m_Model.Stylus, null );
                    enable = true;
                } else {
                    enable = false;
                }
            }

            this.Enabled = enable;
        }

        /// <summary>
        /// Handle the stylus changing
        /// </summary>
        /// <param name="sender">The event sender</param>
        /// <param name="args">The event args</param>
        private void HandlePenStateChanged(object sender, PropertyEventArgs args) {
            using (Synchronizer.Lock(this.m_Model.PenState.SyncRoot)) {
                foreach (StylusEntry stylus in this.m_Table.Values) {
                    if (stylus.PenType == PenType.Pen) {
                        stylus.DrawingAttributes.RasterOperation = this.m_Model.PenState.PenRasterOperation;
                        stylus.DrawingAttributes.PenTip = this.m_Model.PenState.PenTip;
                        stylus.DrawingAttributes.Color = Color.FromArgb(this.m_Model.PenState.PenColor);
                        stylus.DrawingAttributes.Width = this.m_Model.PenState.PenWidth;
                        stylus.DrawingAttributes.Height = this.m_Model.PenState.PenHeight;
                        this.HandleDrawingAttributesChanged(stylus, null);
                    }

                    if (stylus.PenType == PenType.Hightlighter) {
                        stylus.DrawingAttributes.RasterOperation = this.m_Model.PenState.HLRasterOperation;
                        stylus.DrawingAttributes.PenTip = this.m_Model.PenState.HLTip;
                        stylus.DrawingAttributes.Color = Color.FromArgb(this.m_Model.PenState.HLColor);
                        stylus.DrawingAttributes.Width = this.m_Model.PenState.HLWidth;
                        stylus.DrawingAttributes.Height = this.m_Model.PenState.HLHeight;
                        this.HandleDrawingAttributesChanged(stylus, null);
                    }
                }
            }
        }

        /// <summary>
        /// Event handler for the drop down opening
        /// </summary>
        /// <param name="sender">The sender</param>
        /// <param name="e">The args</param>
        internal virtual void HandleParentButtonDropDown(object sender, EventArgs e) {
            if (this.CurrentStylusEntry == null || this.CurrentStylusEntry.DrawingAttributes == null)
                return;
            Form options = new PropertiesForm.PenPropertiesPage(this.m_Model.PenState, this.CurrentStylusEntry.DrawingAttributes, this.CurrentStylusEntry.PenType == PenType.Pen);
            DialogResult result = options.ShowDialog();

            // Force an update, since there's no way to change the DrawingAttributes instance.
            this.CurrentStylusEntry = this.CurrentStylusEntry;
            if (this.m_CurrentStylus != null && this.CurrentStylusEntry != null) {
                using (Synchronizer.Lock(this.m_CurrentStylus.SyncRoot)) {
                    this.m_CurrentStylus.DrawingAttributes = this.CurrentStylusEntry.DrawingAttributes.Clone();
                }
            }
        }
    }

    #endregion

    internal class CustomFontButton : ToolStripDropDownButton {
        StylusToolBarButton textbutton_;
        PresenterModel model_;
        public CustomFontButton(StylusToolBarButton textbutton, PresenterModel model) : base(){
            this.ToolTipText = "Choose a default font";
            textbutton_ = textbutton;
            model_ = model;
            this.Enabled = false;
            model_.Changed["Stylus"].Add(new PropertyEventHandler(this.ChangeStylus));
            
        }

        /// <summary>
        /// keeps track of the stylus so that the font button is only enabled when we are 
        /// in text mode.
        /// </summary>
        /// <param name="o"></param>
        /// <param name="args"></param>
        private void ChangeStylus(object o, PropertyEventArgs args) {
            using (Synchronizer.Lock(model_.Stylus)) {
                if (model_.Stylus is TextStylusModel) {
                    this.Enabled = true;
                } else {
                    this.Enabled = false;
                }
            }
        }

        /// <summary>
        /// pops up a font dialogue and changes the font.
        /// </summary>
        /// <param name="e"></param>
        protected override void OnClick(EventArgs e) {
            base.OnClick(e);
            FontDialog d = new FontDialog();
            d.ShowApply = true;
            d.ShowColor = false;
            d.ShowEffects = false;
            d.Font = TextStylusModel.GetInstance().DefaultFont;
            d.ShowDialog();

            ///pre: we are in text stylus mode
            ///this is ensured by textbutton_.PerformClick()
            using (Synchronizer.Lock(TextStylusModel.GetInstance().SyncRoot)) {
                try {
                    TextStylusModel.GetInstance().Font = d.Font;
                } catch(Exception){}
            }            
        }
    }
}
