using System;
using System.Collections;
using System.Drawing;
using System.Text;

namespace UW.ClassroomPresenter.Misc
{
    public class WindowSnapshotWrapper
    {
        #region Static Properties

        /// <summary>
        /// The List of current application windows
        /// </summary>
        public static ArrayList applicationWindows;

        public static ArrayList ApplicationWindows
        {
            get{
                applicationWindows=new ArrayList ();
                User32.EnumWindows(EnumApplicationCallBack,0);
                ArrayList apps = applicationWindows;
                applicationWindows = null;
                return apps;
            }
        }

        #endregion 

        /// <summary>
        /// Call back method for EnumApplication
        /// </summary>
        private static bool EnumApplicationCallBack(IntPtr pwindow, int i)
        {
            Win32Window window = new Win32Window(pwindow);

            if (window.Parent.Window != IntPtr.Zero) return true;
            if (window.Visible != true) return (true);
            if (window.Text == string.Empty) return (true);
            if (window.ClassName.Substring(0, 8) == "IDEOwner") return (true); 

            applicationWindows.Add(window);
            return true;
        }
    }

    /// <summary>
    /// Window of Win32
    /// </summary>
    public class Win32Window
    {
        #region Const

        const int SRCCOPY = 0x00CC0020;//for BitBlt

        #endregion 

        #region Properties

        /// <summary>
        /// IntPtr to the window
        /// </summary>
        private IntPtr m_Window;
        public IntPtr Window
        {
            get
            {
                return this.m_Window;
            }
        }

        /// <summary>
        /// Get the parent of this window. If this is a top level window, it will return null;
        /// </summary>
        public Win32Window Parent
        {
            get
            {
                return new Win32Window(User32.GetParent(this.m_Window));
            }
        }        

        /// <summary>
        /// The text in this window
        /// </summary>
        public string Text
        {
            get
            {
                int length = User32.GetWindowTextLength(this.m_Window);
                StringBuilder sb = new StringBuilder(length + 1);
                User32.GetWindowText(this.m_Window, sb, sb.Capacity);
                return sb.ToString();
            }
        }

        /// <summary>
        /// The class name for the window
        /// </summary>
        public string ClassName
        {
            get
            {
                int length = 254;
                StringBuilder sb = new StringBuilder(length + 1);
                User32.GetClassName(this.m_Window, sb, sb.Capacity);
                sb.Length = length;
                return sb.ToString();
            }
        }

        /// <summary>
        /// Get the Image of the windows
        /// </summary>
        public Image WindowAsBitmap
        {
            get
            {
                if (this.m_Window==IntPtr.Zero)
                    return null;

                User32.BringWindowToTop(this.m_Window);
                System.Threading.Thread.Sleep(500);

                User32.Rect rect = new User32.Rect();
                if (!User32.GetWindowRect(this.m_Window, ref rect))
                    return null;

               

                Image myImage = new Bitmap(rect.Width, rect.Height);
                Graphics gr1 = Graphics.FromImage(myImage);
                IntPtr dc1 = gr1.GetHdc();
                IntPtr dc2 = User32.GetWindowDC(this.m_Window);
                User32.BitBlt(dc1, 0, 0, rect.Width, rect.Height, dc2, 0, 0, SRCCOPY);
                gr1.ReleaseHdc(dc1);
                return myImage;
            }
        }

        /// <summary>
        /// Whether the window is minimized
        /// </summary>
        public bool Minimized
        {
            get
            {
                return User32.IsIconic(this.m_Window);
            }
        }

        /// <summary>
        /// Wheteher the windows is visible
        /// </summary>
        public bool Visible
        {
            get
            {
                return User32.IsWindowVisible(this.m_Window) != 0 ? true : false;
            }
        }

        #endregion

        /// <summary>
        /// Construct Win32Window
        /// </summary>
        public Win32Window(IntPtr window)
        {
            this.m_Window = window;
        }

        /// <summary>
        /// Override the ToString method
        /// </summary>
        public override string ToString()
        {
            return this.Text;
        }
    }
}
