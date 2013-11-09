using System;
using System.Collections.Generic;
using System.Text;
using System.Reflection;
using System.Windows.Forms;

namespace UW.ClassroomPresenter.Misc {
    public class MenuShortcutHelper {
        /// <summary>
        /// Helpful function that allows us to set shortcut keys to any combination instead of just
        /// those supported in the .NET Framework.
        /// </summary>
        /// <param name="i">The menu item to st the shortcut key of</param>
        /// <param name="c">The key to set it to</param>
        public static void SetShortcut( MenuItem i, Keys c ) {
            Type t = typeof( MenuItem );
            FieldInfo fi = t.GetField( "data", BindingFlags.Instance | BindingFlags.NonPublic );
            object menuData = fi.GetValue( i );
            t = menuData.GetType();
            fi = t.GetField( "shortcut", BindingFlags.Instance | BindingFlags.NonPublic );
            fi.SetValue( menuData, (Shortcut)(c) );
        }
    }
}
