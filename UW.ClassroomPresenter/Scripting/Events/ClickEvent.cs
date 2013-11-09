using System;
using System.Windows.Forms;
using System.Collections;
using UW.ClassroomPresenter.Scripting;

namespace UW.ClassroomPresenter.Scripting.Events {
    public class ClickEvent : IScriptEvent {
        protected string[] m_Path;

        public ClickEvent( ArrayList param ) {
            if( param.Count >= 1 ) {
                this.m_Path = Script.ParsePath( (string)param[0] );
            }
        }

        public bool Execute( ScriptExecutionService service ) {
            System.Windows.Forms.Control c = service.GetControl( this.m_Path );
            if( c == null )
                return false;
            MessageSender.SendClick( c );
            return true;
        }
    }
}
