using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;

namespace UW.ClassroomPresenter.Misc {
    class DispalyControlHelpers {

        /// <summary>
        /// Returns the number of available displays on this device
        /// </summary>
        /// <returns></returns>
        public static int GetNumberOfDisplays() {
            return 2;
        }

        /// <summary>
        /// TODO: This function should return the best guess procedure for this machine for changing displays
        /// </summary>
        public static void GetDefaultSettings() {
        }

        public static string nViewCloneCommand = "nvcpl.dll,dtcfg setview 1 clone DA AA";

        /// <summary>
        /// Execute a dll command using RunDLL32.exe
        /// </summary>
        /// <param name="cmd">The parameter to rundll32.exe</param>
        public static void InvokeRunDLL32Command( string cmd ) 
        {
            Process dllProcess = new Process();
            ProcessStartInfo startInfo = new ProcessStartInfo("rundll32.exe");
            startInfo.UseShellExecute = false;
            startInfo.RedirectStandardOutput = true;
            startInfo.Arguments = cmd;
            dllProcess.StartInfo = startInfo;

            dllProcess.Start();
        }

        /// <summary>
        /// Create the extended desktop
        /// </summary>
        /// <returns>True if successful, false otherwise</returns>
        public static bool AddUnattachedDisplayDeviceToDesktop() {
	        uint DispNum = 0;
	        Gdi32.DISPLAY_DEVICE DisplayDevice;
	        Gdi32.DEVMODE defaultMode;
	        IntPtr hdc;
	        int nWidth;
	        bool bFoundSecondary = false;

	        hdc = User32.GetDC( IntPtr.Zero );
	        nWidth = Gdi32.GetDeviceCaps( hdc, Gdi32.HORZRES );
	        User32.ReleaseDC( IntPtr.Zero, hdc );

	        // Initialize DisplayDevice
            DisplayDevice = Gdi32.CreateDISPLAY_DEVICE();

	        // Get display device
	        while( (User32.EnumDisplayDevices( null, DispNum, ref DisplayDevice, 0)) &&
		        (bFoundSecondary == false) )
	        {
                defaultMode = Gdi32.CreateDEVMODE();
		        if( !User32.EnumDisplaySettings( DisplayDevice.DeviceName,
								                 User32.ENUM_REGISTRY_SETTINGS, 
								                 ref defaultMode) )
		        {
			        return false;
		        }

		        if( (DisplayDevice.StateFlags & Gdi32.DISPLAY_DEVICE_PRIMARY_DEVICE) == 0 )
		        {
			        // Found the first secondary device
			        bFoundSecondary = true;
			        defaultMode.dmPositionX += nWidth;
			        defaultMode.dmFields = Gdi32.DM_POSITION;
			        User32.ChangeDisplaySettingsEx( DisplayDevice.DeviceName,
									                ref defaultMode,
									                IntPtr.Zero,
									                User32.CDS_NORESET|Misc.User32.CDS_UPDATEREGISTRY,
									                IntPtr.Zero );

			        // A second call to ChangeDisplaySettings updates the monitor.
			        User32.ChangeDisplaySettings( IntPtr.Zero, 0 );
		        }

		        // Reinitialize DisplayDevice
                DisplayDevice = Gdi32.CreateDISPLAY_DEVICE();
                DispNum++;
	        }

	        return true;
        }

        /// <summary>
        /// Create the cloned desktop
        /// </summary>
        /// <returns>True if successful, false otherwise</returns>
        public static bool RemoveAttachedDisplayDeviceFromDesktop() {
	        uint DispNum = 0;
	        Gdi32.DISPLAY_DEVICE DisplayDevice;
	        Gdi32.DEVMODE defaultMode;
	        bool bFoundSecondary = false;

	        // Initialize DisplayDevice
	        DisplayDevice = Gdi32.CreateDISPLAY_DEVICE();

	        // Initialize DefaultMode
	        defaultMode = Gdi32.CreateDEVMODE();

	        // Get display device
	        while( (User32.EnumDisplayDevices( null, DispNum, ref DisplayDevice, 0)) &&
		           (bFoundSecondary == false) ) 
            {
		        if( (DisplayDevice.StateFlags & Gdi32.DISPLAY_DEVICE_PRIMARY_DEVICE) == 0 )
		        {
			        // Found the first secondary device
			        bFoundSecondary = true;
			        defaultMode.dmPositionX = 0;
		        	defaultMode.dmPositionY = 0;
			        defaultMode.dmPelsHeight = 0;
			        defaultMode.dmPelsWidth = 0;
			        defaultMode.dmFields = Gdi32.DM_POSITION | Gdi32.DM_PELSWIDTH | Gdi32.DM_PELSHEIGHT;
			        User32.ChangeDisplaySettingsEx( DisplayDevice.DeviceName,
									                ref defaultMode,
									                IntPtr.Zero,
									                User32.CDS_NORESET|User32.CDS_UPDATEREGISTRY,
									                IntPtr.Zero );
									
			        // A second call to ChangeDisplaySettings updates the monitor.
			        User32.ChangeDisplaySettings( IntPtr.Zero, 0 );
		        }

      		    // Reinitialize DisplayDevice
                DisplayDevice = Gdi32.CreateDISPLAY_DEVICE();
		        DispNum++;
	        }
	        return true;
        }
    }
}
