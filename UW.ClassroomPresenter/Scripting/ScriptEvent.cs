using System;
using System.Collections.Generic;
using System.Text;

namespace UW.ClassroomPresenter.Scripting {
    public interface IScriptEvent {
        bool Execute( ScriptExecutionService service );
    }
}
