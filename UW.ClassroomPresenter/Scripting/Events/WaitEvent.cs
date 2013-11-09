using System;
using System.Windows.Forms;
using System.Collections;
using UW.ClassroomPresenter.Scripting;

namespace UW.ClassroomPresenter.Scripting.Events {
    public class WaitEvent : IScriptEvent {
        protected int m_TimeOut;

        public WaitEvent( ArrayList param ) {
            if( param.Count >= 1 ) {
                this.m_TimeOut = Convert.ToInt32((string)param[0]);
            }
        }

        public bool Execute( ScriptExecutionService service ) {
            System.Threading.Thread.Sleep( this.m_TimeOut );
            return true;
        }
    }
}
