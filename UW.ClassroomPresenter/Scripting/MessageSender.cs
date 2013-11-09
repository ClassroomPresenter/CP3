using System;
using System.Windows.Forms;
using System.Runtime.InteropServices;

namespace UW.ClassroomPresenter.Scripting {
    /// <summary>
    /// Helper class designed to send messages to windows controls
    /// </summary>
    public sealed class MessageSender {
        private const UInt32 WM_LBUTTONDOWN = 0x201;
        private const UInt32 WM_LBUTTONUP = 0x202;

        [DllImport( "user32.dll" )]
        private static extern int SendMessage( 
            IntPtr handle, // Handle to the control you want to send a message to
            UInt32 message, // The Message ID you want to send
            int wParam, // WPARAM for the message
            int lParam  // LPARAM for the message
            );

        public static void SendClick( Control receiver ) {
            if( receiver != null ) {
                SendMessage( receiver.Handle, WM_LBUTTONDOWN, 0, 0 );
                SendMessage( receiver.Handle, WM_LBUTTONUP, 0, 0 );
            }
        }
    }
}
