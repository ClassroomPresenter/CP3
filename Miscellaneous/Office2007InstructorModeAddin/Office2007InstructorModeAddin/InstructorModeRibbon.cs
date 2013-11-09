using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Office = Microsoft.Office.Core;

namespace Office2007InstructorModeAddin
{
    public partial class ThisAddIn
    {
        private InstructorModeRibbon ribbon;
    
        protected override object RequestService(Guid serviceGuid)
        {
            if (serviceGuid == typeof(Office.IRibbonExtensibility).GUID)
            {
                if (ribbon == null)
                    ribbon = new InstructorModeRibbon();

                return ribbon;
            }
    
            return base.RequestService(serviceGuid);
        }
    }

    public delegate void ChangedEventHandler( bool newValue );

    [ComVisible(true)]
    public class InstructorModeRibbon : Office.IRibbonExtensibility
    {
        private Office.IRibbonUI ribbon;

        // OnClick is an event, implemented by a delegate ButtonEventHandler.
        public event ChangedEventHandler PresentationModeChanged;
        public event ChangedEventHandler ShapeModeChanged;

        public InstructorModeRibbon()
        {
        }

        #region IRibbonExtensibility Members

        public string GetCustomUI(string ribbonID)
        {
            return GetResourceText("Office2007InstructorModeAddin.InstructorModeRibbon.xml");
        }

        #endregion
        #region Properties

        private bool projectedViewState = true;
        public bool ProjectedViewState
        {
            get
            {
                return projectedViewState;
            }
            set
            {
                if (value != projectedViewState)
                {
                    projectedViewState = value;
                    PresentationModeChanged(value);
                }
                this.InvalidateButtons();
            }
        }

        private bool unrestrictedState = true;
        public bool UnrestrictedState
        {
            get
            {
                return unrestrictedState;
            }
            set
            {
                if (value != unrestrictedState)
                {
                    unrestrictedState = value;
                    ShapeModeChanged(value);
                }
                this.InvalidateButtons();
            }
        }

        private const string ProjectedView = "ProjectedView";
        private const string ProjectedSet = "ProjectedSet";
        private const string InstructorView = "InstructorView";
        private const string InstructorSet = "InstructorSet";

        private bool projectedViewEnabled = true;
        public bool ProjectedViewEnabled
        {
            get
            {
                return projectedViewEnabled;
            }
            set
            {
                projectedViewEnabled = value;
                this.InvalidateButtons();
            }
        }
        private bool projectedSetEnabled = true;
        public bool ProjectedSetEnabled
        {
            get
            {
                return projectedSetEnabled;
            }
            set
            {
                projectedSetEnabled = value;
                this.InvalidateButtons();
            }
        }

        private bool instructorViewEnabled = true;
        public bool InstructorViewEnabled
        {
            get
            {
                return instructorViewEnabled;
            }
            set
            {
                instructorViewEnabled = value;
                this.InvalidateButtons();
            }
        }
        private bool instructorSetEnabled = true;
        public bool InstructorSetEnabled
        {
            get
            {
                return instructorSetEnabled;
            }
            set
            {
                instructorSetEnabled = value;
                this.InvalidateButtons();
            }
        }

        #endregion Properties

        #region Ribbon Callbacks

        public void OnLoad(Office.IRibbonUI ribbonUI)
        {
            this.ribbon = ribbonUI;
        }

        public bool OnGetEnabled(Office.IRibbonControl control)
        {
            switch (control.Id)
            {
                case ProjectedView:
                    return ProjectedViewEnabled;
                case InstructorView:
                    return InstructorViewEnabled;
                case ProjectedSet:
                    return ProjectedSetEnabled;
                case InstructorSet:
                    return InstructorSetEnabled;
            }

            return false;
        }

        public bool OnGetPressed(Office.IRibbonControl control)
        {
            switch (control.Id)
            {
                case ProjectedView:
                    return this.ProjectedViewState;
                case InstructorView:
                    return !this.ProjectedViewState;
                case ProjectedSet:
                    return this.UnrestrictedState;
                case InstructorSet:
                    return !this.UnrestrictedState;
            }

            return false;
        }

        public void OnClick(Office.IRibbonControl control, bool isPressed)
        {
            switch (control.Id ) {
                case ProjectedView:
                    this.ProjectedViewState = true;
                    break;
                case InstructorView:
                    this.ProjectedViewState = false;
                    break;
                case ProjectedSet:
                    this.UnrestrictedState = true;
                    break;
                case InstructorSet:
                    this.UnrestrictedState = false;
                    break;
            }

            this.InvalidateButtons();
        }

        public void OnExport(Office.IRibbonControl control)
        {
            MessageBox.Show("Saving...NOT IMPLEMENTED");
        }

        #endregion Ribbon Callbacks

        private void InvalidateButtons()
        {
            if (this.ribbon != null)
            {
                this.ribbon.InvalidateControl(ProjectedView);
                this.ribbon.InvalidateControl(InstructorView);
                this.ribbon.InvalidateControl(ProjectedSet);
                this.ribbon.InvalidateControl(InstructorSet);
            }
        }

        #region Helpers

        private static string GetResourceText(string resourceName)
        {
            Assembly asm = Assembly.GetExecutingAssembly();
            string[] resourceNames = asm.GetManifestResourceNames();
            for (int i = 0; i < resourceNames.Length; ++i)
            {
                if (string.Compare(resourceName, resourceNames[i], StringComparison.OrdinalIgnoreCase) == 0)
                {
                    using (StreamReader resourceReader = new StreamReader(asm.GetManifestResourceStream(resourceNames[i])))
                    {
                        if (resourceReader != null)
                        {
                            return resourceReader.ReadToEnd();
                        }
                    }
                }
            }
            return null;
        }

        #endregion
    }
}
