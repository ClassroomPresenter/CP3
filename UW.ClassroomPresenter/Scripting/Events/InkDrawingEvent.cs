using System;
using System.Windows.Forms;
using System.Collections;
using System.IO;
using UW.ClassroomPresenter.Scripting;

namespace UW.ClassroomPresenter.Scripting.Events {
    /// <summary>
    /// Class representing the drawing of ink to a control
    /// </summary>
    public class InkDrawingEvent : IScriptEvent {
        public int InterStrokeDelay = 200;
        protected string[] m_Path;
        protected Microsoft.Ink.Ink PlaybackInk;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="param">The event parameters</param>
        public InkDrawingEvent( ArrayList param ) {
            PlaybackInk = new Microsoft.Ink.Ink();
            if( param.Count >= 1 ) {
                LoadRecordedData( (string)param[0] );
            }
            if( param.Count >= 2 ) {
                this.m_Path = Script.ParsePath( (string)param[1] );
            }
        }

        /// <summary>
        /// Execute the ink drawing event
        /// </summary>
        /// <param name="service">The service to execute over</param>
        /// <returns>True if successful, false otherwise</returns>
        public bool Execute( ScriptExecutionService service ) {
            Control c = service.GetControl( this.m_Path );
            if( c == null )
                return false;
            if( c.InvokeRequired ) {
                GenericVoidCallback func = new GenericVoidCallback( delegate { c.Focus(); } );
                c.Invoke( func );
            } else
                c.Focus();

            this.PlaybackRecordedData( c, true, PacketPlaybackRate.Default, service.GetInkTransform() );
            return true;
        }

        /// <summary>
        /// Destructor
        /// </summary>
        ~InkDrawingEvent() {
            if( null != PlaybackInk ) {
                PlaybackInk.Dispose();
            }
        }

        /// <summary>
        ///  Specifies whether the ink data is played back:
        ///     at real-time speed (Default), 
        ///     with a delay between packets (Slow),
        ///     immediately with no delay between packets (Fast)
        /// </summary>
        protected enum PacketPlaybackRate {
            Default = 0,
            Slow,
            Fast
        }

        /// <summary>
        ///  Loads Ink Data from a specified ISF or JNT file
        /// </summary>
        /// <param name="inkDataFileName">
        /// The file to load the Ink Data from.</param>
        protected void LoadRecordedData(string inkDataFileName) {
            // First, reset the Ink object
            PlaybackInk.DeleteStrokes();

            if( !File.Exists( inkDataFileName ) ) {
                MessageBox.Show( "Cannot find file: " + inkDataFileName );
                return;
            }

            // Read in the specified file
            FileStream isfStream = new FileStream( inkDataFileName, FileMode.Open, FileAccess.Read );

            // Verify the filestream creation was successful
            if (null != isfStream && isfStream.Length > 0) {
                byte[] isfBytes = new byte[isfStream.Length];

                // read in the ISF doc
                isfStream.Read(isfBytes, 0, (int)isfStream.Length);

                // Load the ink into a new Temporary object
                // Once an Ink object has been "dirtied" it cannot load ink
                Microsoft.Ink.Ink TemporaryInk = new Microsoft.Ink.Ink();

                // Load the ISF into the ink object
                TemporaryInk.Load(isfBytes);

                // Add the loaded strokes to our Ink object
                PlaybackInk.AddStrokesAtRectangle(TemporaryInk.Strokes, TemporaryInk.Strokes.GetBoundingBox());

                // Close the file stream
                isfStream.Close();
            }
        }

        delegate void GenericVoidCallback();

        /// <summary>
        ///  Plays back ink data to a specified control
        /// </summary>
        /// <param name="destinationControl">
        /// The control to play the Ink Data to.</param>           
        /// <param name="destinationRenderer">
        /// The Ink Renderer used to convert ink data to display coordinates</param>        
        /// <param name="keepDestinationAspectRatio">
        /// Specified whether to keep original aspect ratio of ink when scaling</param>
        /// <param name="playbackRate">
        /// The rate at which to play back the Ink Data</param>
        protected void PlaybackRecordedData( Control destinationControl,  
                                             bool keepDestinationAspectRatio, 
                                             PacketPlaybackRate playbackRate,
                                             System.Drawing.Drawing2D.Matrix iTrans ) {
            if( null != PlaybackInk ) {
                System.Drawing.Graphics g = destinationControl.CreateGraphics();
                Microsoft.Ink.Renderer destinationRenderer = new Microsoft.Ink.Renderer();
                destinationRenderer.SetViewTransform( iTrans );

                // Set whether or not to keep ink aspect ratio in the display window                
                bool keepAspectRatio = keepDestinationAspectRatio;
                
                // Declare scaling factors
                float scaleFactorX = 1.0f;
                float scaleFactorY = 1.0f;

                // Get the size of the display window                
                System.Drawing.Size displayWindowSize = destinationControl.ClientSize;    
   
                // Get ink bounding box in ink space; convert the box's size to pixel;
                System.Drawing.Rectangle inkBoundingBox = PlaybackInk.GetBoundingBox();     

                // Set the size and offset of the destination input
                System.Drawing.Point inkBoundingBoxSize = new System.Drawing.Point(inkBoundingBox.Width, inkBoundingBox.Height);

                // Convert the inkspace coordinate to pixels
                destinationRenderer.InkSpaceToPixel(g, ref inkBoundingBoxSize);    
              
                // Get the offset of ink
                System.Drawing.Point inkOffset = inkBoundingBox.Location;                              
                    
                // Determine what the scaling factor of the destination control is so
                // we know how to correctly resize the ink data
                getDisplayWindowScalingFactor(new System.Drawing.Size(inkBoundingBoxSize), displayWindowSize, keepAspectRatio, ref scaleFactorX, ref scaleFactorY);
                
                // Iterate through all ink strokes and extract the packet data
                foreach ( Microsoft.Ink.Stroke currentStroke in PlaybackInk.Strokes ) {
                    // Convert the stroke's packet data to INPUT structs
                    INPUT[] inputs = SendInputInterop.StrokeToInputs( currentStroke, destinationRenderer, g, scaleFactorX, scaleFactorY, destinationControl, inkOffset );
                    
                    if ( null != inputs ) {
                        // Iterate through all the extracted INPUT data in order to send to destination control
                        for ( int i = 0; i < inputs.Length; i++ ) {
                            // Use the Win32 SendInput API to send the ink data point to the control
                            // Note that all playback will use the upper left of the destination control
                            // as the origin
                            SendInputInterop.SendInput( 1, new INPUT[] { inputs[i] }, System.Runtime.InteropServices.Marshal.SizeOf( inputs[i] ) );

                            // Determine the delay between packets (within a stroke)
                            switch( playbackRate ) {
                                case PacketPlaybackRate.Default:
                                default:
                                    System.Threading.Thread.Sleep(5);
                                    break;
                                case PacketPlaybackRate.Slow:
                                    System.Threading.Thread.Sleep(100);
                                    break;
                                case PacketPlaybackRate.Fast:
                                    break;
                            }
                        }
                    }

                    // Reset the focus to the destination control in case it has been changed
                    if( destinationControl.InvokeRequired ) {
                        GenericVoidCallback func = new GenericVoidCallback( delegate { destinationControl.Focus();  } );
                        destinationControl.Invoke( func );
                    } else
                        destinationControl.Focus();

                    // Create a delay between each stroke
                    if ( 0 != InterStrokeDelay) {
                        System.Threading.Thread.Sleep((int)(InterStrokeDelay));
                    }
                }
                // dispose the graphics object
                g.Dispose();
            }
        }
        
        /// <summary>
        /// Get the scaling factors for the display window
        /// </summary>
        /// <param name="inkBoundingBoxSize">Size of the ink bounding box</param>
        /// <param name="displayWindowSize">Size of the display window</param>
        /// <param name="keepAspectRatio">Specifies whether original ink aspect ratio is retained when scaling</param>
        /// <param name="scaleFactorX">Horizontal scaling factor</param>
        /// <param name="scaleFactorY">Vertical scaling factor</param>
        protected void getDisplayWindowScalingFactor( System.Drawing.Size inkBoundingBoxSize, 
                                                      System.Drawing.Size displayWindowSize, 
                                                      bool keepAspectRatio, 
                                                      ref float scaleFactorX, 
                                                      ref float scaleFactorY ) {    
            // scale the ink only when the display window is smaller than the ink bounding box 
            if ( (inkBoundingBoxSize.Height > displayWindowSize.Height) ||
                 (inkBoundingBoxSize.Width > displayWindowSize.Width) ) {
                scaleFactorX = displayWindowSize.Width / ((float) inkBoundingBoxSize.Width);
                scaleFactorY = displayWindowSize.Height / ((float) inkBoundingBoxSize.Height);         
            
                // Use the smaller scaling factor if keepAspectRatio is turned on.
                if( true == keepAspectRatio ) {
                    if ( scaleFactorX < scaleFactorY)
                        scaleFactorY = scaleFactorX;
                    else
                        scaleFactorX = scaleFactorY;
                }      
            } else {
                scaleFactorX = 1.0f;
                scaleFactorY = 1.0f;
            }            
        }
    }
}
