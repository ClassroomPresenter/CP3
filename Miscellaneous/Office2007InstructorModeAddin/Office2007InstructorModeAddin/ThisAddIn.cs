using System;
using System.Windows.Forms;
using Microsoft.VisualStudio.Tools.Applications.Runtime;
using PowerPoint = Microsoft.Office.Interop.PowerPoint;
using Office = Microsoft.Office.Core;

namespace Office2007InstructorModeAddin
{
    public partial class ThisAddIn
    {
        public PPTLibrary.PPT ppt;
        public PPTPaneManagement.PPTPaneManager pptpm;

        private void ThisAddIn_Startup(object sender, System.EventArgs e)
        {
            ppt = new PPTLibrary.PPT(this.Application);
            pptpm = new PPTPaneManagement.PPTPaneManager(ppt, ribbon);
        }

        private void ThisAddIn_Shutdown(object sender, System.EventArgs e)
        {
        }

        #region VSTO generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InternalStartup()
        {
            this.Startup += new System.EventHandler(ThisAddIn_Startup);
            this.Shutdown += new System.EventHandler(ThisAddIn_Shutdown);
        }

        #endregion
    }
}
