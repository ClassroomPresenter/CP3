using System;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.Text;

namespace UW.ClassroomPresenter.Misc {
    class User32 {

        #region EnumDisplaySettings Consts

        public const uint ENUM_CURRENT_SETTINGS  = 0xFFFFFFFF;
        public const uint ENUM_REGISTRY_SETTINGS = 0xFFFFFFFE;
        
        #endregion

        #region ChangeDisplaySettings Consts

        /* Flags for ChangeDisplaySettings */
        public const int CDS_UPDATEREGISTRY  = 0x00000001;
        public const int CDS_TEST            = 0x00000002;
        public const int CDS_FULLSCREEN      = 0x00000004;
        public const int CDS_GLOBAL          = 0x00000008;
        public const int CDS_SET_PRIMARY     = 0x00000010;
        public const int CDS_VIDEOPARAMETERS = 0x00000020;
        public const int CDS_RESET           = 0x40000000;
        public const int CDS_NORESET         = 0x10000000;

        /* Return values for ChangeDisplaySettings */
        public const int DISP_CHANGE_SUCCESSFUL  = 0;
        public const int DISP_CHANGE_RESTART     = 1;
        public const int DISP_CHANGE_FAILED      = -1;
        public const int DISP_CHANGE_BADMODE     = -2;
        public const int DISP_CHANGE_NOTUPDATED  = -3;
        public const int DISP_CHANGE_BADFLAGS    = -4;
        public const int DISP_CHANGE_BADPARAM    = -5;
        public const int DISP_CHANGE_BADDUALVIEW = -6;

        #endregion

        #region Window Rectangle Struct 

        public struct Rect
        {
            public int left;
            public int top;
            public int right;
            public int bottom;

            public int Width
            {
                get { return right - left; }
            }

            public int Height
            {
                get { return bottom - top; }
            }
        }

        #endregion

        // Methods
        [DllImport("user32.dll",CharSet=CharSet.Unicode)]
        public static extern bool EnumDisplayDevices( string lpDevice,
                                                      uint iDevNum,
                                                      ref Misc.Gdi32.DISPLAY_DEVICE lpDisplayDevice,
                                                      uint dwFlags );
        [DllImport( "user32.dll", CharSet = CharSet.Unicode)]
        public static extern bool EnumDisplaySettings( string lpDevice, // display device
                                                       uint iModeNum,   // graphics mode
                                                       ref Misc.Gdi32.DEVMODE lpDevMode // graphics mode settings
                                                      );

        [DllImport("user32.dll",CharSet=CharSet.Unicode)]
        public static extern int ChangeDisplaySettings( ref Misc.Gdi32.DEVMODE lpDevMode,
                                                        int dwFlags );
        [DllImport( "user32.dll", CharSet = CharSet.Unicode )]
        public static extern int ChangeDisplaySettings( IntPtr lpDevMode,
                                                        int dwFlags );
        [DllImport( "user32.dll", CharSet = CharSet.Unicode )]
        public static extern long ChangeDisplaySettingsEx( string DeviceName,  // name of display device
                                                           ref Misc.Gdi32.DEVMODE lpDevMode,     // graphics mode
                                                           IntPtr hwnd, // not used; must be NULL
                                                           uint dwflags, // graphics mode options
                                                           IntPtr lParam // video parameters (or NULL)
                                                          );

        [DllImport( "user32.dll" )]
        public static extern IntPtr GetDC( IntPtr hwnd );
        [DllImport( "user32.dll" )]
        public static extern int ReleaseDC( IntPtr hwnd, IntPtr hdc );

        public delegate bool EnumWindowsProc(IntPtr window, int i);
        [DllImport("user32.dll")]
        public static extern bool EnumWindows(EnumWindowsProc callback,int i);
        [DllImport("user32.dll")]
        public static extern IntPtr GetParent(IntPtr window);
        [DllImport("user32.dll")]
        public static extern int IsWindowVisible(IntPtr window);
        [DllImport("user32.dll")]
        public static extern int GetWindowText(IntPtr window, [In][Out] StringBuilder text, int copyCount);
        [DllImport("user32.dll")]
        public static extern int GetWindowTextLength(IntPtr window);
        [DllImport("user32.dll", EntryPoint = "GetClassName")]
        public static extern int GetClassName(IntPtr window, [In][Out] StringBuilder text, int maxBytes);
        [DllImport("user32.dll")]
        public static extern bool BringWindowToTop(IntPtr window);
        [DllImport("user32.dll")]
        public static extern bool GetClientRect(IntPtr window, ref Rect rectangle);
        [DllImport("user32.dll")]
        public static extern IntPtr GetWindowDC(IntPtr window);
        [DllImport("user32.dll")]
        public static extern bool GetWindowRect(IntPtr window, ref Rect rectangle);
        [DllImport("gdi32.dll")]
        public static extern UInt64 BitBlt(IntPtr hDestDC, int x, int y, int nWidth, int nHeight,
            IntPtr hSrcDC, int xSrc, int ySrc, System.Int32 dwRop);
        [DllImport("user32.dll")]
        public static extern bool IsIconic(IntPtr window);
    }
}
