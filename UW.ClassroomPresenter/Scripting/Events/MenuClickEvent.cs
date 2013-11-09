using System;
using System.Windows.Forms;
using System.Collections;
using UW.ClassroomPresenter.Scripting;

namespace UW.ClassroomPresenter.Scripting.Events {
    public class MenuClickEvent : IScriptEvent {
        protected string[] m_Path;

        public MenuClickEvent( ArrayList param ) {
            if( param.Count >= 1 ) {
                this.m_Path = Script.ParsePath( (string)param[0] );
            }
        }

        public bool Execute( ScriptExecutionService service ) {
            System.Windows.Forms.Menu m = service.GetMenuItem( this.m_Path );
            if( m == null )
                return false;
            if( m is MenuItem )
                ((MenuItem)m).PerformClick();
            else
                return false;
            return true;
        }
    }
}
