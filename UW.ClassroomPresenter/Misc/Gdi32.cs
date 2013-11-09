using System;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.Text;

namespace UW.ClassroomPresenter.Misc {
    class Gdi32 {

        #region GetDeviceCaps Consts

        /* Device Parameters for GetDeviceCaps() */
        public const int DRIVERVERSION  = 0;   // Device driver version
        public const int TECHNOLOGY     = 2;   // Device classification
        public const int HORZSIZE       = 4;   // Horizontal size in millimeters
        public const int VERTSIZE       = 6;   // Vertical size in millimeters
        public const int HORZRES        = 8;   // Horizontal width in pixels
        public const int VERTRES        = 10;  // Vertical height in pixels
        public const int BITSPIXEL      = 12;  // Number of bits per pixel
        public const int PLANES         = 14;  // Number of planes
        public const int NUMBRUSHES     = 16;  // Number of brushes the device has
        public const int NUMPENS        = 18;  // Number of pens the device has
        public const int NUMMARKERS     = 20;  // Number of markers the device has
        public const int NUMFONTS       = 22;  // Number of fonts the device has
        public const int NUMCOLORS      = 24;  // Number of colors the device supports
        public const int PDEVICESIZE    = 26;  // Size required for device descriptor
        public const int CURVECAPS      = 28;  // Curve capabilities
        public const int LINECAPS       = 30;  // Line capabilities
        public const int POLYGONALCAPS  = 32;  // Polygonal capabilities
        public const int TEXTCAPS       = 34;  // Text capabilities
        public const int CLIPCAPS       = 36;  // Clipping capabilities
        public const int RASTERCAPS     = 38;  // Bitblt capabilities
        public const int ASPECTX        = 40;  // Length of the X leg
        public const int ASPECTY        = 42;  // Length of the Y leg
        public const int ASPECTXY       = 44;  // Length of the hypotenuse

        public const int LOGPIXELSX     = 88;  // Logical pixels/inch in X
        public const int LOGPIXELSY     = 90;  // Logical pixels/inch in Y

        public const int SIZEPALETTE    = 104; // Number of entries in physical palette
        public const int NUMRESERVED    = 106; // Number of reserved entries in palette
        public const int COLORRES       = 108; // Actual color resolution

        public const int PHYSICALWIDTH  = 110; // Physical Width in device units
        public const int PHYSICALHEIGHT = 111; // Physical Height in device units
        public const int PHYSICALOFFSETX= 112; // Physical Printable Area x margin
        public const int PHYSICALOFFSETY= 113; // Physical Printable Area y margin
        public const int SCALINGFACTORX = 114; // Scaling factor x
        public const int SCALINGFACTORY = 115; // Scaling factor y

        public const int VREFRESH       = 116;  // Current vertical refresh rate of the display device (for displays only) in Hz
        public const int DESKTOPVERTRES = 117;  // Horizontal width of entire desktop in pixels
        public const int DESKTOPHORZRES = 118;  // Vertical height of entire desktop in pixels
        public const int BLTALIGNMENT   = 119;  // Preferred blt alignment

        public const int SHADEBLENDCAPS = 120;  // Shading and blending caps
        public const int COLORMGMTCAPS  = 121;  // Color Management caps

        #endregion

        #region DEV_MODE Consts

        /* field selection bits */
        public const int DM_ORIENTATION        = 0x00000001;
        public const int DM_PAPERSIZE          = 0x00000002;
        public const int DM_PAPERLENGTH        = 0x00000004;
        public const int DM_PAPERWIDTH         = 0x00000008;
        public const int DM_SCALE              = 0x00000010;
        public const int DM_POSITION           = 0x00000020;
        public const int DM_NUP                = 0x00000040;
        public const int DM_DISPLAYORIENTATION = 0x00000080;
        public const int DM_COPIES             = 0x00000100;
        public const int DM_DEFAULTSOURCE      = 0x00000200;
        public const int DM_PRINTQUALITY       = 0x00000400;
        public const int DM_COLOR              = 0x00000800;
        public const int DM_DUPLEX             = 0x00001000;
        public const int DM_YRESOLUTION        = 0x00002000;
        public const int DM_TTOPTION           = 0x00004000;
        public const int DM_COLLATE            = 0x00008000;
        public const int DM_FORMNAME           = 0x00010000;
        public const int DM_LOGPIXELS          = 0x00020000;
        public const int DM_BITSPERPEL         = 0x00040000;
        public const int DM_PELSWIDTH          = 0x00080000;
        public const int DM_PELSHEIGHT         = 0x00100000;
        public const int DM_DISPLAYFLAGS       = 0x00200000;
        public const int DM_DISPLAYFREQUENCY   = 0x00400000;
        public const int DM_ICMMETHOD          = 0x00800000;
        public const int DM_ICMINTENT          = 0x01000000;
        public const int DM_MEDIATYPE          = 0x02000000;
        public const int DM_DITHERTYPE         = 0x04000000;
        public const int DM_PANNINGWIDTH       = 0x08000000;
        public const int DM_PANNINGHEIGHT      = 0x10000000;
        public const int DM_DISPLAYFIXEDOUTPUT = 0x20000000;

        #endregion

        #region Devive Properties Consts

        public const int DM_GRAYSCALE = 1;
        public const int DM_INTERLACED = 2;
        
        public const int DISPLAY_DEVICE_ATTACHED_TO_DESKTOP = 0x00000001;
        public const int DISPLAY_DEVICE_MULTI_DRIVER        = 0x00000002;
        public const int DISPLAY_DEVICE_PRIMARY_DEVICE      = 0x00000004;
        public const int DISPLAY_DEVICE_MIRRORING_DRIVER    = 0x00000008;
        public const int DISPLAY_DEVICE_VGA_COMPATIBLE      = 0x00000010;
        public const int DISPLAY_DEVICE_REMOVABLE           = 0x00000020;
        public const int DISPLAY_DEVICE_MODESPRUNED         = 0x08000000;
        public const int DISPLAY_DEVICE_REMOTE              = 0x04000000; 
        public const int DISPLAY_DEVICE_DISCONNECT          = 0x02000000;

        #endregion

        /*
        typedef struct _DISPLAY_DEVICEW {
            DWORD  cb;
            WCHAR  DeviceName[32];
            WCHAR  DeviceString[128];
            DWORD  StateFlags;
            WCHAR  DeviceID[128];
            WCHAR  DeviceKey[128];
        } DISPLAY_DEVICEW, *PDISPLAY_DEVICEW, *LPDISPLAY_DEVICEW;
        */
        [StructLayout(LayoutKind.Sequential,Pack=1,CharSet=CharSet.Unicode)]
        public struct DISPLAY_DEVICE {
	        public int cb;
	        [MarshalAs(UnmanagedType.ByValTStr,SizeConst=32)]
		    public string DeviceName;
	        [MarshalAs(UnmanagedType.ByValTStr,SizeConst=128)]
	    	public string DeviceString;
	        public int StateFlags;
	        [MarshalAs(UnmanagedType.ByValTStr,SizeConst=128)]
		    public string DeviceID;
	        [MarshalAs(UnmanagedType.ByValTStr,SizeConst=128)]
		    public string DeviceKey;
        }
        public static DISPLAY_DEVICE CreateDISPLAY_DEVICE() {
            DISPLAY_DEVICE dd = new DISPLAY_DEVICE();
            dd.DeviceName = new String( new char[32] );
            dd.DeviceString = new String( new char[128] );
            dd.DeviceID = new String( new char[128] );
            dd.DeviceKey = new String( new char[128] );
            dd.cb = (int)Marshal.SizeOf( dd );
            return dd;
        }

        [StructLayout(LayoutKind.Sequential,Pack=1,CharSet=CharSet.Unicode)]
        public struct DEVMODE {
            [MarshalAs(UnmanagedType.ByValTStr,SizeConst=32)]
            public string dmDeviceName;
            public short dmSpecVersion;
            public short dmDriverVersion;
            public short dmSize;
            public short dmDriverExtra;
            public int dmFields;
            public int dmPositionX;
            public int dmPositionY;
            public int dmDisplayOrientation;
            public int dmDisplayFixedOutput;
            public short dmColor;
            public short dmDuplex;
            public short dmYResolution;
            public short dmTTOption;
            public short dmCollate;
            [MarshalAs(UnmanagedType.ByValTStr,SizeConst=32)]
            public string dmFormName;
            public short dmLogPixels;
            public short dmBitsPerPel;
            public int dmPelsWidth;
            public int dmPelsHeight;
            public int dmDisplayFlags;
            public int dmDisplayFrequency;
            public int dmICMMethod;
            public int dmICMIntent;
            public int dmMediaType;
            public int dmDitherType;
            public int dmReserved1;
            public int dmReserved2;
            public int dmPanningWidth;
            public int dmPanningHeight;
        };
        public static DEVMODE CreateDEVMODE() {
            DEVMODE dm = new DEVMODE();
            dm.dmDeviceName = new String( new char[32] );
            dm.dmFormName = new String( new char[32] );
            dm.dmSize = (short)Marshal.SizeOf( dm );
            return dm;
        }

        // Methods
        [DllImport( "gdi32.dll" )]
        public static extern int SelectClipRgn( IntPtr hdc, IntPtr hrgn );
        [DllImport( "gdi32.dll" )]
        public static extern int IntersectClipRect( IntPtr hdc, int left, int top, int right, int bottom );
        [DllImport( "gdi32.dll" )]
        public static extern bool RestoreDC( IntPtr hdc, int nSavedDC );
        [DllImport( "gdi32.dll" )]
        public static extern int SaveDC( IntPtr hdc );
        [DllImport( "gdi32.dll" )]
        public static extern int GetDeviceCaps( IntPtr hdc, int index );
    }
}
