//-------------------------------------------------------------------------
//
//  Copyright (C) Microsoft Corporation All rights reserved.
//
//  File: SendInputInterop.cs
//
//  Description: Converts individual Stroke packets to a Win32 array of INPUT objects,
//  which is then passed to the Win32 SendInput function to simulate user input.
//
//--------------------------------------------------------------------------
using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Ink;

namespace UW.ClassroomPresenter.Scripting {
    #region Win32 Input Related Struct Definitions
    [StructLayout(LayoutKind.Sequential)]
    internal struct KBDINPUT {
        internal UInt16 vKey;
        internal UInt16 scanCode;
        internal UInt32 flags;
        internal UInt32 time;
        internal UIntPtr extraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct MOUSEINPUT {
        internal Int32 dx;
        internal Int32 dy;
        internal UInt32 mouseData;
        internal UInt32 dwFlags;
        internal UInt32 time;
        internal UIntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct HARDWAREINPUT {
        internal UInt32 uMsg;
        internal UInt16 wParamL;
        internal UInt16 wParamH;
    }

    [StructLayout(LayoutKind.Explicit)]
    internal struct INPUT {
        [FieldOffset(0)] internal UInt32 type;
        [FieldOffset(4)] internal MOUSEINPUT mi;
        [FieldOffset(4)] internal KBDINPUT ki;
        [FieldOffset(4)] internal HARDWAREINPUT hi;        
    }
    #endregion

    /// <summary>
    /// Converts individual Stroke packets to a Win32 array of INPUT objects,
    /// which is then passed to the Win32 SendInput function to simulate user input.
    /// </summary>
    internal class SendInputInterop {
        #region MouseEvent static Definitions
        internal static readonly UInt32 MOUSEEVENTF_MOVE  = 0x001;          /* mouse move */
        internal static readonly UInt32 MOUSEEVENTF_LEFTDOWN  = 0x0002;     /* left button down */
        internal static readonly UInt32 MOUSEEVENTF_LEFTUP  = 0x0004;       /* left button up */
        internal static readonly UInt32 MOUSEEVENTF_RIGHTDOWN  = 0x0008;    /* right button down */
        internal static readonly UInt32 MOUSEEVENTF_RIGHTUP  = 0x0010;      /* right button up */
        internal static readonly UInt32 MOUSEEVENTF_MIDDLEDOWN  = 0x0020;   /* middle button down */
        internal static readonly UInt32 MOUSEEVENTF_MIDDLEUP  = 0x0040;     /* middle button up */
        internal static readonly UInt32 MOUSEEVENTF_XDOWN  = 0x0080;        /* x button down */
        internal static readonly UInt32 MOUSEEVENTF_XUP  = 0x0100;          /* x button up */
        internal static readonly UInt32 MOUSEEVENTF_WHEEL  = 0x0800;        /* wheel button rolled */
        internal static readonly UInt32 MOUSEEVENTF_VIRTUALDESK  = 0x4000;  /* map to entire virtual desktop */
        internal static readonly UInt32 MOUSEEVENTF_ABSOLUTE  = 0x8000;     /* absolute move */

        internal static readonly UInt32 INPUT_MOUSE = 0;
        internal static readonly UInt32 INPUT_KEYBOARD = 1;
        internal static readonly UInt32 INPUT_HARDWARE = 2;
        #endregion

        [DllImport( "user32.dll" )]
        internal static extern uint SendInput( uint inputCount, INPUT[] inputs, int size );

        [DllImport( "kernel32.dll" )]
        internal static extern uint GetTickCount();

        internal SendInputInterop() {
        }

        internal delegate Size GetBoundsCallback();
        internal delegate Point PointScreenCallback();

        /// <summary>
        ///  Converts Stroke PacketData to an Input array
        /// </summary>
        /// <param name="Stroke">
        /// The Stroke to convert from</param>           
        /// <param name="destinationRenderer">
        /// The renderer to use when converting from hi-metric to pixel coordinates</param>        
        /// <param name="horizontalScaleFactor">
        /// The horizontal scaling factor</param>
        /// <param name="verticalScaleFactor">
        /// The vertical scaling factor</param>       
        /// <param name="destinationControl">
        /// The control that ink data is played back to</param>
        /// <param name="inkOffset">
        /// The offset to the upper left of the ink data</param>
        internal static INPUT[] StrokeToInputs(
            Stroke sourceStroke, 
            Renderer destinationRenderer, 
            Graphics g, 
            float horizontalScaleFactor, 
            float verticalScaleFactor,
            Control destinationControl,
            Point inkOffset 
            ) 
        {
            int [] sourceStrokePacketData = sourceStroke.GetPacketData();
            INPUT[] mouseEvents = new INPUT[sourceStrokePacketData.Length/sourceStroke.PacketDescription.Length + 1];
            UInt32 tickCount = GetTickCount();
            GetBoundsCallback b = new GetBoundsCallback( delegate { return Screen.GetBounds(destinationControl).Size; } );
            Size screenSize = (Size)destinationControl.Invoke( b );

            // Iterate through each packet set and create a corresponding Mouse based Input structure
            for (int strokePacketIndex = 0, i = 0; strokePacketIndex < sourceStrokePacketData.Length; strokePacketIndex += sourceStroke.PacketDescription.Length, i++) {
                // Obtain the packet as a Point from the stroke
                Point packetPoint = new Point(sourceStrokePacketData[strokePacketIndex] - inkOffset.X, sourceStrokePacketData[strokePacketIndex+1] - inkOffset.Y);

                // Convert the inkspace coordinate of the packet to a display coordinate
                destinationRenderer.InkSpaceToPixel(g,ref packetPoint);                

                // Scale the location to the current window                
                packetPoint.X = (int) (packetPoint.X * horizontalScaleFactor);
                packetPoint.Y = (int) (packetPoint.Y * verticalScaleFactor);                
                
                // Avoid mouse' enabling and moving the border of the panel)
                if ( 0 == packetPoint.X) {
                    packetPoint.X = 1;
                }
                if ( 0 == packetPoint.Y) {
                    packetPoint.Y = 1;
                }

                // Convert the window coordinates to screen coordinates
                if( destinationControl.InvokeRequired ) {
                    PointScreenCallback z = new PointScreenCallback( delegate { return destinationControl.PointToScreen( packetPoint ); } );
                    packetPoint = (Point)destinationControl.Invoke( z );
                }
                else
                    packetPoint = destinationControl.PointToScreen( packetPoint );

                #region Create the Input structure for the Mouse Event
                
                mouseEvents[i].type = INPUT_MOUSE;
                mouseEvents[i].mi.dx = (packetPoint.X * 65535) / screenSize.Width;
                mouseEvents[i].mi.dy = (packetPoint.Y * 65535) / screenSize.Height;                
                mouseEvents[i].mi.mouseData = 0;
                
                if(0 == strokePacketIndex) {
                    // Move to the start of the stroke
                    mouseEvents[i].mi.dwFlags = MOUSEEVENTF_MOVE | MOUSEEVENTF_ABSOLUTE;
                    mouseEvents[i].mi.time = tickCount++;
                    mouseEvents[i].mi.dwExtraInfo = (UIntPtr)0;

                    // Start of the 'stroke'
                    mouseEvents[++i].type = INPUT_MOUSE;
                    mouseEvents[i].mi.dx = (packetPoint.X * 65535) / screenSize.Width;
                    mouseEvents[i].mi.dy = (packetPoint.Y * 65535) / screenSize.Height;
                    mouseEvents[i].mi.mouseData = 0;
                    mouseEvents[i].mi.dwFlags = MOUSEEVENTF_LEFTDOWN | MOUSEEVENTF_ABSOLUTE;
                } else if(sourceStrokePacketData.Length - sourceStroke.PacketDescription.Length <= strokePacketIndex) {
                    // End of the 'stroke'
                    mouseEvents[i].mi.dwFlags = MOUSEEVENTF_LEFTUP | MOUSEEVENTF_ABSOLUTE;
                } else {
                    // Body of the 'stroke'
                    mouseEvents[i].mi.dwFlags = MOUSEEVENTF_MOVE | MOUSEEVENTF_ABSOLUTE;
                }

                mouseEvents[i].mi.time = tickCount++;
                mouseEvents[i].mi.dwExtraInfo = (UIntPtr)0;
                
                #endregion
            }
            return mouseEvents;
        }
    }
}
