using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

using UW.ClassroomPresenter.Model;
using UW.ClassroomPresenter.Model.Presentation;
using UW.ClassroomPresenter.Viewer.Slides;
using UW.ClassroomPresenter.Misc;
using UW.ClassroomPresenter.Camera;

namespace UW.ClassroomPresenter.Viewer.Menus
{
    #region InsertMenu
    
    /// <summary>
    /// Reprents all the Image Insert menus from localfile, screen and camera
    /// </summary>
    public class InsertMenu : MenuItem
    {
        private readonly PresenterModel m_Model;

        public InsertMenu(PresenterModel model)
        {
            this.m_Model = model;
            this.Text = Strings.Insert;

            this.MenuItems.Add(new InsertImageFromFileMenuItem(model));
            this.MenuItems.Add(new InsertImageFromSnapshotMenuItem(model));
            this.MenuItems.Add(new InsertImageFromClipboardMenuItem(model));
            this.MenuItems.Add(new InsertImageFromCameraMenuItem(model));
            this.Popup += new EventHandler(InsertMenu_Popup);
        }

        /// <summary>
        /// This method enables or disables menu items dynamically.
        /// </summary>
        /// <param name="args"></param>
        private void InsertMenu_Popup(object sender, EventArgs e) {
            using (Synchronizer.Lock(this.m_Model.Workspace.CurrentDeckTraversal.SyncRoot)) {
                if (~this.m_Model.Workspace.CurrentDeckTraversal == null) {
                    //we do not have a deck, so all deck related items should be disabled.
                    foreach (MenuItem item in this.MenuItems) {
                        item.Enabled = false;
                    }
                } else {
                    // we have a deck
                    foreach (MenuItem item in this.MenuItems) {
                        if (item is InsertImageFromFileMenuItem || item is InsertImageFromSnapshotMenuItem)
                            item.Enabled = true;
                        else if (item is InsertImageFromClipboardMenuItem)
                            item.Enabled = (Clipboard.GetDataObject()).GetDataPresent(DataFormats.Bitmap);
                        else if (item is InsertImageFromCameraMenuItem)
                            item.Enabled = CaptureGraphForm.HasCamera();
                    }
                }
            }
        }

        #region Static Method

        public static void InsertImage(Image image, SlideModel slide) {
            int width = image.Width;
            int height = image.Height;
            int w = 0, h = 0;
            using (Synchronizer.Lock(slide.SyncRoot)) {
                w = slide.Bounds.Width;
                h = slide.Bounds.Height - 10;
            }

            if (width > w)
            {
                height = w * height / width;
                width = w;
            }
            if (height > h)
            {
                width = h * width / height;
                height = h;
            }
            /// add 4 to the with of image box as padding
            height = (width + 4) * height / width;
            width += 4;

            Point position = new Point(Math.Max((w - width) / 2, 0), Math.Max((h - height) / 2, 10));

            ImageSheetModel imagesheet = new ImageSheetModel(Guid.NewGuid(), image, true, position, new Size(width, height), 0);
            imagesheet.Visible = true;

            using (Synchronizer.Lock(slide.SyncRoot)) {
                slide.AnnotationSheets.Add(imagesheet);
            }
        }

        #endregion
    }

    #endregion

    #region InsertImageFromFileMenuItem

    /// <summary>
    /// Insert MenuItem for Image from local file
    /// </summary>
    public class InsertImageFromFileMenuItem : MenuItem
    {
        private readonly PresenterModel m_Model;

        public InsertImageFromFileMenuItem(PresenterModel model)
        {
            this.m_Model = model;
            this.Text = Strings.InsertImageFromFile;
        }

        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);

            //Show up the OpenFileDialog
            //get the image that the user wants to load
            OpenFileDialog file_dialog = new OpenFileDialog();
            file_dialog.Filter = "JPEG files (*.jpg)|*.jpg|BMP files (*.bmp)|*.bmp|PNG files (*.png)|*.png";
            if (file_dialog.ShowDialog() == DialogResult.OK) {
                Image image = Image.FromFile(file_dialog.FileName);
                SlideModel slide = null;

                using (Synchronizer.Lock(this.m_Model.Workspace.CurrentDeckTraversal.SyncRoot)) {
                    if ((~this.m_Model.Workspace.CurrentDeckTraversal) != null) {
                        using (Synchronizer.Lock((~this.m_Model.Workspace.CurrentDeckTraversal).SyncRoot)) {
                            slide = (~this.m_Model.Workspace.CurrentDeckTraversal).Current.Slide;
                        }
                    }
                }

                if (slide != null)
                    InsertMenu.InsertImage(image, slide);
            }
        }
    }

    #endregion

    #region InsertImageFromSnapshotMenuItem

    /// <summary>
    /// Insert MenuItem for Screen Snapshot Image  
    /// </summary>
    public class InsertImageFromSnapshotMenuItem : MenuItem
    {
        private PresenterModel m_Model;

        public InsertImageFromSnapshotMenuItem(PresenterModel model)
        {
            this.m_Model = model;
            this.Text = Strings.InsertImageFromSnapshot;

            foreach (Win32Window window in WindowSnapshotWrapper.ApplicationWindows)
            {
                if (!window.Minimized)
                {
                    WindowSnapshotMenuItem appMenuItem = new WindowSnapshotMenuItem(this.m_Model, window);
                    this.MenuItems.Add(appMenuItem);
                }
            }

            this.Popup += new EventHandler(InsertImageFromSnapshotMenuItem_Popup);
        }

        private void InsertImageFromSnapshotMenuItem_Popup(object sender, EventArgs e)
        {
            this.MenuItems.Clear();
            foreach (Win32Window window in WindowSnapshotWrapper.ApplicationWindows)
            {
                if (!window.Minimized)
                {
                    WindowSnapshotMenuItem appMenuItem = new WindowSnapshotMenuItem(this.m_Model, window);
                    this.MenuItems.Add(appMenuItem);
                }
            }
        }  
    }

    /// <summary>
    /// Popup Insert MenuItem for specified application snapshot
    /// </summary>
    public class WindowSnapshotMenuItem : MenuItem
    {
        private PresenterModel m_Model;
        private Win32Window m_AppWindow;

        public WindowSnapshotMenuItem(PresenterModel model, Win32Window window)
        {
            this.m_AppWindow = window;
            this.m_Model = model;

            this.Text = this.m_AppWindow.Text;

            this.Click += new EventHandler(WindowSnapshotMenuItem_Click);
        }

        private void WindowSnapshotMenuItem_Click(object sender, EventArgs e)
        {
            if (this.m_AppWindow != null && !this.m_AppWindow.Minimized) {
                MemoryStream ms = new MemoryStream();
                try {
                    this.m_AppWindow.WindowAsBitmap.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                } catch {
                    return;
                }
                ms.Position = 0;
                Image image = Image.FromStream(ms);

                SlideModel slide = null;
                using (Synchronizer.Lock(this.m_Model.Workspace.CurrentDeckTraversal.SyncRoot)) {
                    if ((~this.m_Model.Workspace.CurrentDeckTraversal) != null) {
                        using (Synchronizer.Lock((~this.m_Model.Workspace.CurrentDeckTraversal).SyncRoot)) {
                            slide = (~this.m_Model.Workspace.CurrentDeckTraversal).Current.Slide;
                        }
                    }
                }

                if (slide != null)
                    InsertMenu.InsertImage(image, slide);
            }
        }
    }

    #endregion

    #region InsertImageFromClipboardMenuItem

    public class InsertImageFromClipboardMenuItem : MenuItem
    {
        private readonly PresenterModel m_Model;

        public InsertImageFromClipboardMenuItem(PresenterModel model)
        {
            this.m_Model = model;
            this.Text = Strings.InsertImageFromClipboard;
        }

        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);

            IDataObject clipboardDateObject= Clipboard.GetDataObject();
            if (clipboardDateObject.GetDataPresent(DataFormats.Bitmap)) {
                Image image = (Bitmap)clipboardDateObject.GetData(DataFormats.Bitmap);
                
                SlideModel slide = null;
                using (Synchronizer.Lock(this.m_Model.Workspace.CurrentDeckTraversal.SyncRoot)) {
                    if ((~this.m_Model.Workspace.CurrentDeckTraversal) != null) {
                        using (Synchronizer.Lock((~this.m_Model.Workspace.CurrentDeckTraversal).SyncRoot)) {
                            slide = (~this.m_Model.Workspace.CurrentDeckTraversal).Current.Slide;
                        }
                    }
                }

                if (slide != null)
                    InsertMenu.InsertImage(image, slide);
            }
        }
    }

    #endregion

    #region InsertImageFromCameraMenuItem

    public class InsertImageFromCameraMenuItem : MenuItem
    {
        private readonly PresenterModel m_Model;

        public InsertImageFromCameraMenuItem(PresenterModel model)
        {
            this.m_Model = model;
            this.Text = Strings.InsertImageFromCamera;
        }

        protected override void OnClick(EventArgs e)
        {
            SlideModel slide = null;
            Model.Viewer.ViewerStateModel vsm = null;

            using (Synchronizer.Lock(this.m_Model.Workspace.CurrentDeckTraversal.SyncRoot)) {
                if ((~this.m_Model.Workspace.CurrentDeckTraversal) != null) {
                    using (Synchronizer.Lock((~this.m_Model.Workspace.CurrentDeckTraversal).SyncRoot)) {
                        slide = (~this.m_Model.Workspace.CurrentDeckTraversal).Current.Slide;
                    }
                }
            }

            using (Synchronizer.Lock(this.m_Model.SyncRoot)) {
                vsm = this.m_Model.ViewerState;
            }

            if (slide != null && vsm != null) {
                CaptureGraphForm cg_form = new CaptureGraphForm(slide, vsm);
                if (cg_form.ShowDialog() == DialogResult.OK)
                    InsertMenu.InsertImage(cg_form.Image, slide);
            }
        }
    }

    #endregion
}
