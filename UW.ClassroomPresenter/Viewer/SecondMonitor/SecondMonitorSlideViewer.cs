using System;
using System.Collections.Generic;
using System.Text;
using UW.ClassroomPresenter.Viewer.Slides;
using UW.ClassroomPresenter.Model.Viewer;
using UW.ClassroomPresenter.Model;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace UW.ClassroomPresenter.Viewer.SecondMonitor {
    /// <summary>
    /// This class encapsulates SlideViewers for the second monitor. It 
    /// is essentially a MainslideViewer whose SlideDisplay's SecondMonitor
    /// property is set to true :P
    /// </summary>
    class SecondMonitorSlideViewer : MainSlideViewer {
        public SecondMonitorSlideViewer(PresenterModel model) : base(model, false){
            this.m_SlideDisplay.IsSecondMonitorDisplay = true;

        }
        

    }
}
