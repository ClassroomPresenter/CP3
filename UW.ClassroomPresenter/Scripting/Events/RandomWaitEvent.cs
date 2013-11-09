using System;
using System.Windows.Forms;
using System.Collections;
using UW.ClassroomPresenter.Scripting;

namespace UW.ClassroomPresenter.Scripting.Events {
    public class RandomWaitEvent : IScriptEvent {
        protected int m_LowTimeOut = 0;
        protected int m_HighTimeOut = 0;

        public RandomWaitEvent( ArrayList param ) {
            if( param.Count >= 2 ) {
                this.m_LowTimeOut = Convert.ToInt32((string)param[0]);
                this.m_HighTimeOut = Convert.ToInt32((string)param[0]);
            }
        }

        public bool Execute( ScriptExecutionService service ) {
            Random r = new Random();
            System.Threading.Thread.Sleep( r.Next( this.m_LowTimeOut, this.m_HighTimeOut+1 ) );
            return true;
        }
    }
}
