// $Id: MainToolBar.cs 1612 2008-05-29 22:04:21Z cmprince $

using System;
using System.Diagnostics;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;

using UW.ClassroomPresenter.Model;
using UW.ClassroomPresenter.Model.Network;

namespace UW.ClassroomPresenter.Viewer.ToolBars {
    /// <summary>
    /// The main toolstrip containing all the buttons for the toolbar.
    /// 5/25/2006 -- Converted from ToolBar class to ToolStrip class 
    /// to enable buttons with images of multiple dimensions.
    /// </summary>
    public class MainToolBar : ToolStrip {
        /// <summary>
        /// The event queue to post messages to
        /// </summary>
        private readonly ControlEventQueue m_EventQueue;
        /// <summary>
        /// The model that this component modifies and interacts with
        /// </summary>
        private readonly PresenterModel m_Model;
        /// <summary>
        /// True when this component has been disposed
        /// </summary>
        private bool m_Disposed;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="model">The model that this component modifies</param>
        public MainToolBar(PresenterModel model, ControlEventQueue dispatcher) {
            // Initialize private variables
            this.m_EventQueue = dispatcher;
            this.m_Model = model;

            // Setup the object UI description
            this.SuspendLayout();

            this.Name = "MainToolBar";
            this.GripStyle = ToolStripGripStyle.Hidden;

            // Create the primary image list for this object
            this.ImageList = new ImageList();
            this.ImageList.ImageSize = new Size(40, 40);
            this.ImageList.ColorDepth = ColorDepth.Depth32Bit;

            // Set the default button size
            this.ImageScalingSize = new Size( 40, 40 );
            this.AutoSize = true;

            // Assign a custom renderer to this object so that rendering appears 
            // in the old style
            this.Renderer = new CustomRenderer();
  
            this.ResumeLayout();
        }

        /// <summary>
        /// Disposes of this UI component
        /// </summary>
        /// <param name="disposing">True if we are in the process of disposing all objects</param>
        protected override void Dispose( bool disposing ) {
            if( this.m_Disposed ) return;
            try {
                if( disposing ) {
                    this.ImageList.Dispose();
                }
            } finally {
                base.Dispose( disposing );
            }

            this.m_Disposed = true;
        }

        /// <summary>
        /// Custom renderer class for displaying the toolbar in the Windows Classic style
        /// </summary>
        protected class CustomRenderer : ToolStripSystemRenderer {
            /// <summary>
            /// Constructor
            /// </summary>
            public CustomRenderer() : base() {}

            /// <summary>
            /// Renders the separator as a blank space (i.e. no vertical/horizontal line)
            /// </summary>
            /// <param name="e">The render parameters</param>
            protected override void OnRenderSeparator( ToolStripSeparatorRenderEventArgs e ) {
                e.Graphics.Clear( e.ToolStrip.BackColor );
            }

            /// <summary>
            /// Renders the background of toolbar buttons
            /// </summary>
            /// <param name="e">The render parameters</param>
            protected override void OnRenderButtonBackground( ToolStripItemRenderEventArgs e ) {
                // Perform the default actions
                // NOTE: Is this needed? If not, do we have to clear the button background?
                base.OnRenderButtonBackground( e );

                // Plus draw the button border based on the state of the button
                if( e.Item is ToolStripButton ) {
                    ButtonState state = 0;
                    if( !e.Item.Enabled )
                        state |= ButtonState.Inactive;
                    if( ((ToolStripButton)e.Item).Checked )
                        state |= ButtonState.Checked;
                    if( e.Item.Pressed )
                        state |= ButtonState.Pushed;
                    if( state == 0 )
                        state = ButtonState.Normal;
                    ControlPaint.DrawButton( e.Graphics, new Rectangle( 0, 0, e.Item.Width, e.Item.Height ), state );
                }
            }

            /// <summary>
            /// Renders the split button background
            /// </summary>
            /// <param name="e">The render parameters</param>
            protected override void OnRenderSplitButtonBackground( ToolStripItemRenderEventArgs e ) {
                // Perform the default actions
                // NOTE: Is this needed? If not, do we have to clear the button background?
                base.OnRenderSplitButtonBackground( e );

                // Plus draw the button border based on the state of the button
                if( e.Item is ToolStripSplitButton ) {
                    ButtonState state = 0;
                    if( !e.Item.Enabled )
                        state |= ButtonState.Inactive;
                    if( e.Item is CustomDrawingAttributesDropDownButton && ((CustomDrawingAttributesDropDownButton)e.Item).Checked )
                        state |= ButtonState.Checked;
                    if( ((ToolStripSplitButton)e.Item).ButtonPressed )
                        state |= ButtonState.Pushed;
                    if( state == 0 )
                        state = ButtonState.Normal;
                    // Draw the button itself
                    ControlPaint.DrawButton( e.Graphics, 
                        new Rectangle( 0, 0, 
                        ((ToolStripSplitButton)e.Item).ButtonBounds.Width, 
                        ((ToolStripSplitButton)e.Item).ButtonBounds.Width), 
                        state );
                    // Calculate the dimensions of the drop-down button
                    Rectangle rtemp = ((ToolStripSplitButton)e.Item).DropDownButtonBounds;
                    rtemp.Width++;
                    rtemp.Height++;
                    rtemp.X--;
                    rtemp.Y--;
                    // Remove the "checked" state from the state
                    state &= ~ButtonState.Checked; 
                    // Draw the drop-down button
                    ControlPaint.DrawComboButton( e.Graphics, rtemp, state );
                }
            }

            /// <summary>
            /// Render the button images based on the size of the button
            /// </summary>
            /// <param name="e"></param>
            protected override void OnRenderItemImage( ToolStripItemImageRenderEventArgs e ) {
                if( e.Item is ToolStripButton && e.Image != null && !(e.Item is ToolStripDropDownButton)  ) {
                    // Calculate the correct location to draw
                    Rectangle r = e.Item.ContentRectangle;
                    if( e.Item.Pressed || ((ToolStripButton)e.Item).Checked ) {
                        r.Y += 1;
                        r.X += 1;
                    }
                    // Calculate the correct image to draw
                    Image i = e.Image;
                    if( !e.Item.Enabled )
                        i = ToolStripRenderer.CreateDisabledImage( e.Image );

                    // Maintain the image aspect ratio and center in button
                    int imageWidth = i.Width;
                    int imageHeight = i.Height;
                    float imageAspect = (float)imageWidth / (float)imageHeight;
                    int origButtonWidth = r.Width;
                    int origButtonHeight = r.Height;
                    float origButtonAspect = (float)origButtonWidth / (float)origButtonHeight;

                    // Adjust the Button aspect ratio
                    if( imageAspect < origButtonAspect ) {
                        r.Width = (int)Math.Round( imageAspect * origButtonHeight );
                    } else if( imageAspect > origButtonAspect ) {
                        r.Height = (int)Math.Round( origButtonWidth / imageAspect );
                    }

                    // Center Image in Button
                    r.X += (origButtonWidth-r.Width) / 2;
                    r.Y += (origButtonHeight-r.Height) / 2;

                    // Draw the image
                    e.Graphics.DrawImage( i, r );
                } else
                    base.OnRenderItemImage( e );
            }
        }
    }
}
