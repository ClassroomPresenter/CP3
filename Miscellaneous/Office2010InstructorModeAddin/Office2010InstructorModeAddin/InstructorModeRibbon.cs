using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using Office = Microsoft.Office.Core;
using System.Windows.Forms;

// TODO:  Follow these steps to enable the Ribbon (XML) item:

// 1: Copy the following code block into the ThisAddin, ThisWorkbook, or ThisDocument class.

//  protected override Microsoft.Office.Core.IRibbonExtensibility CreateRibbonExtensibilityObject()
//  {
//      return new InstructorModeRibbon();
//  }

// 2. Create callback methods in the "Ribbon Callbacks" region of this class to handle user
//    actions, such as clicking a button. Note: if you have exported this Ribbon from the Ribbon designer,
//    move your code from the event handlers to the callback methods and modify the code to work with the
//    Ribbon extensibility (RibbonX) programming model.

// 3. Assign attributes to the control tags in the Ribbon XML file to identify the appropriate callback methods in your code.  

// For more information, see the Ribbon XML documentation in the Visual Studio Tools for Office Help.


namespace Office2010InstructorModeAddin {
    public delegate void ChangedEventHandler(bool newValue);

    [ComVisible(true)]
    public class InstructorModeRibbon : Office.IRibbonExtensibility {
        private Office.IRibbonUI ribbon;

        // OnClick is an event, implemented by a delegate ButtonEventHandler.
        public event ChangedEventHandler PresentationModeChanged;
        public event ChangedEventHandler ShapeModeChanged;

        public InstructorModeRibbon() {
        }

        #region IRibbonExtensibility Members

        public string GetCustomUI(string ribbonID) {
            return GetResourceText("Office2010InstructorModeAddin.InstructorModeRibbon.xml");
        }

        #endregion

        #region Properties

        private bool projectedViewState = true;
        public bool ProjectedViewState {
            get {
                return projectedViewState;
            }
            set {
                if (value != projectedViewState) {
                    projectedViewState = value;
                    PresentationModeChanged(value);
                }
                this.InvalidateButtons();
            }
        }

        private bool unrestrictedState = true;
        public bool UnrestrictedState {
            get {
                return unrestrictedState;
            }
            set {
                if (value != unrestrictedState) {
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
        public bool ProjectedViewEnabled {
            get {
                return projectedViewEnabled;
            }
            set {
                projectedViewEnabled = value;
                this.InvalidateButtons();
            }
        }
        private bool projectedSetEnabled = true;
        public bool ProjectedSetEnabled {
            get {
                return projectedSetEnabled;
            }
            set {
                projectedSetEnabled = value;
                this.InvalidateButtons();
            }
        }

        private bool instructorViewEnabled = true;
        public bool InstructorViewEnabled {
            get {
                return instructorViewEnabled;
            }
            set {
                instructorViewEnabled = value;
                this.InvalidateButtons();
            }
        }
        private bool instructorSetEnabled = true;
        public bool InstructorSetEnabled {
            get {
                return instructorSetEnabled;
            }
            set {
                instructorSetEnabled = value;
                this.InvalidateButtons();
            }
        }

        #endregion Properties

        #region Ribbon Callbacks
        //Create callback methods here. For more information about adding callback methods, select the Ribbon XML item in Solution Explorer and then press F1

        public void Ribbon_Load(Office.IRibbonUI ribbonUI) {
            this.ribbon = ribbonUI;
        }

        public bool OnGetEnabled(Office.IRibbonControl control) {
            switch (control.Id) {
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

        public bool OnGetPressed(Office.IRibbonControl control) {
            switch (control.Id) {
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

        public void OnClick(Office.IRibbonControl control, bool isPressed) {
            switch (control.Id) {
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

        public void OnExport(Office.IRibbonControl control) {
            MessageBox.Show("Saving...NOT IMPLEMENTED");
        }

        #endregion

        private void InvalidateButtons() {
            if (this.ribbon != null) {
                this.ribbon.InvalidateControl(ProjectedView);
                this.ribbon.InvalidateControl(InstructorView);
                this.ribbon.InvalidateControl(ProjectedSet);
                this.ribbon.InvalidateControl(InstructorSet);
            }
        }

        #region Helpers

        private static string GetResourceText(string resourceName) {
            Assembly asm = Assembly.GetExecutingAssembly();
            string[] resourceNames = asm.GetManifestResourceNames();
            for (int i = 0; i < resourceNames.Length; ++i) {
                if (string.Compare(resourceName, resourceNames[i], StringComparison.OrdinalIgnoreCase) == 0) {
                    using (StreamReader resourceReader = new StreamReader(asm.GetManifestResourceStream(resourceNames[i]))) {
                        if (resourceReader != null) {
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
