using System;
using System.Collections;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows.Forms;

using UW.ClassroomPresenter.Model.Presentation;
using UW.ClassroomPresenter.Model.Viewer;

using QuartzTypeLib;
using GraphCapture;

namespace UW.ClassroomPresenter.Camera
{
    public class CaptureGraphForm : Form
    {
        #region CLSID

        //CLSID_CaptureGraphBuilder2 (take from uuids.h DirectX include)
        private static Guid CLSID_CaptureGraphBuilder2 = new Guid("BF87B6E1-8C27-11d0-B3F0-00AA003761C5");
        //CLSID_SystemDeviceEnum
        private static Guid CLSID_SystemDeviceEnum = new Guid("62BE5D10-60EB-11d0-BD3B-00A0C911CE86");
        //CLSID_VideoInputDeviceCategory
        private static Guid CLSID_VideoInputDeviceCategory = new Guid("860BB310-5D01-11d0-BD3B-00A0C911CE86");
        //CLSID_SampleGrabber
        private static Guid CLSID_SampleGrabber = new Guid("C1F400A0-3F08-11d3-9F0B-006008039E37");

        #endregion

        #region IID

        // taken from strmif.h
        private static Guid IID_IBaseFilter = new Guid("56a86895-0ad4-11ce-b03a-0020af0ba770");
        private static Guid IID_ISampleGrabber = new Guid("6B652FFF-11FE-4FCE-92AD-0266B5D7C78F");
        private static Guid IID_IPropertyBag = new Guid("55272A00-42CB-11CE-8135-00AA004BB851");
        private static Guid IID_IAMStreamConfig = new Guid("C6E13340-30AC-11d0-A18C-00A0C9118956");

        #endregion

        #region Pin Category

        // needed for calls to RenderStream
        private static Guid PIN_CATEGORY_CAPTURE = new Guid("fb6c4281-0353-11d1-905f-0000c0cc16ba");

        #endregion

        #region Media Type

        // needed for calls to RenderStream
        private static Guid MEDIATYPE_Video = new Guid("73646976-0000-0010-8000-00AA00389B71");
        private static Guid MEDIASUBTYPE_RGB24 = new Guid("e436eb7d-524f-11ce-9f53-0020af0ba770");
        private static Guid FORMAT_VideoInfo = new Guid("05589f80-c356-11ce-bf01-00aa0055595a");

        #endregion

        #region Constant

        private const int WM_GRAPHNOTIFY = 0x00008001;
        private const int WS_CHILD = 0x40000000;
        private const int WS_CLIPCHILDREN = 0x02000000;
        private const int WS_CLIPSIBLINGS = 0x04000000;

        #endregion

        #region Capture Interface Instance

        private FilgraphManagerClass fmc = null;
        private ICaptureGraphBuilder2 icgb = null;
        private ISampleGrabber isg = null;
        private IMediaEventEx ime = null;
        private IVideoWindow ivw = null;

        private IBaseFilter sf = null;
        private IBaseFilter sgf = null;

        private IAMStreamConfig iamsc = null;
        private _AMMediaType SGMediaType;

        #endregion

        private ArrayList SourceFilterList = new ArrayList();
        private ArrayList OutPutSizeList = new ArrayList();
        private ArrayList MediaTypeList = new ArrayList();

        private readonly SlideModel m_Slide;
        private ViewerStateModel m_ViewerState;
        private Image m_Image;

        private string m_OutPutSize = string.Empty;
        private string m_DeviceName = string.Empty;

        private const int vertical_space = 22;

        #region Designer Generated Component

        private ComboBox cbxDevice;
        private ComboBox cbxSize;
        private Label lbDevice;
        private Label lbSize;

        private Button btnCapture;

        #endregion

        #region Constructor

        public CaptureGraphForm(SlideModel slide, ViewerStateModel viewerstate)//, Viewer.Slides.MainSlideViewer m)
        {
            this.m_ViewerState = viewerstate;
            using (Synchronizer.Lock(this.m_ViewerState.SyncRoot))
            {
                this.m_DeviceName = this.m_ViewerState.DeviceName;
                this.m_OutPutSize = this.m_ViewerState.OutPutSize;
            }
            this.m_Slide = slide;
            InitializeComponent();
        }

        #endregion

        #region Designer Generated Code

        private void InitializeComponent()
        {
            this.btnCapture = new System.Windows.Forms.Button();
            this.cbxDevice = new System.Windows.Forms.ComboBox();
            this.cbxSize = new System.Windows.Forms.ComboBox();
            this.lbDevice = new System.Windows.Forms.Label();
            this.lbSize = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // btnCapture
            // 
            this.btnCapture.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnCapture.FlatStyle = System.Windows.Forms.FlatStyle.System;
            this.btnCapture.Font = Model.Viewer.ViewerStateModel.StringFont;
            this.btnCapture.Location = new System.Drawing.Point(244, 251);
            this.btnCapture.Name = "btnCapture";
            this.btnCapture.Size = new System.Drawing.Size(64, 48);
            this.btnCapture.TabIndex = 0;
            this.btnCapture.Text = Strings.Capture;
            this.btnCapture.UseVisualStyleBackColor = true;
            this.btnCapture.Click += new System.EventHandler(this.btnCapture_Click);
            // 
            // cbxDevice
            // 
            this.cbxDevice.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.cbxDevice.FlatStyle = System.Windows.Forms.FlatStyle.System;
            this.cbxDevice.Font = Model.Viewer.ViewerStateModel.StringFont;
            this.cbxDevice.Location = new System.Drawing.Point(69, 251);
            this.cbxDevice.Name = "cbxDevice";
            this.cbxDevice.Size = new System.Drawing.Size(160, 21);
            this.cbxDevice.TabIndex = 1;
            this.cbxDevice.SelectedIndexChanged += new System.EventHandler(this.cbxDevice_SelectedIndexChanged);
            // 
            // cbxSize
            // 
            this.cbxSize.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.cbxSize.FlatStyle = System.Windows.Forms.FlatStyle.System;
            this.cbxSize.Font = Model.Viewer.ViewerStateModel.StringFont;
            this.cbxSize.Location = new System.Drawing.Point(69, 278);
            this.cbxSize.Name = "cbxSize";
            this.cbxSize.Size = new System.Drawing.Size(160, 21);
            this.cbxSize.TabIndex = 2;
            this.cbxSize.SelectedIndexChanged += new System.EventHandler(this.cbxSize_SelectedIndexChanged);
            // 
            // lbDevice
            // 
            this.lbDevice.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.lbDevice.FlatStyle = System.Windows.Forms.FlatStyle.System;
            this.lbDevice.Font = Model.Viewer.ViewerStateModel.StringFont;
            this.lbDevice.Location = new System.Drawing.Point(12, 251);
            this.lbDevice.Name = "lbDevice";
            this.lbDevice.Size = new System.Drawing.Size(51, 21);
            this.lbDevice.TabIndex = 3;
            this.lbDevice.Text = Strings.Camera;
            this.lbDevice.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // lbSize
            // 
            this.lbSize.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.lbSize.FlatStyle = System.Windows.Forms.FlatStyle.System;
            this.lbSize.Font = Model.Viewer.ViewerStateModel.StringFont;
            this.lbSize.Location = new System.Drawing.Point(12, 278);
            this.lbSize.Name = "lbSize";
            this.lbSize.Size = new System.Drawing.Size(51, 21);
            this.lbSize.TabIndex = 4;
            this.lbSize.Text = Strings.Size;
            this.lbSize.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // CaptureGraphForm
            // 
            this.ClientSize = new System.Drawing.Size(320, 310);
            this.Controls.Add(this.lbSize);
            this.Controls.Add(this.lbDevice);
            this.Controls.Add(this.cbxSize);
            this.Controls.Add(this.cbxDevice);
            this.Controls.Add(this.btnCapture);
            this.Font = Model.Viewer.ViewerStateModel.FormFont;
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimumSize = new System.Drawing.Size(300, 300);
            this.Name = "CaptureGraphForm";
            this.StartPosition = FormStartPosition.CenterParent;
            this.Text = Strings.CaptureWindow;
            this.Load += new System.EventHandler(this.CaptureGraphForm_Load);
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.CaptureGraphForm_FormClosing);
            this.ResumeLayout(false);

        }

        #endregion

        #region Form_Load

        private void CaptureGraphForm_Load(object sender, EventArgs e)
        {
            FindDevice();

            // look for a capture device
            if (this.cbxDevice.Items.Count <= 0)
            {
                MessageBox.Show("Can't find capture device.");
                return;
            }

            if (this.m_DeviceName != string.Empty)
            {
                for (int i = 0; i < this.cbxDevice.Items.Count; i++)
                {
                    if (string.Equals(this.m_DeviceName, this.cbxDevice.Items[i]))
                    {
                        this.cbxDevice.SelectedIndex = i;
                        break;
                    }
                }
            }
            if (this.cbxDevice.SelectedIndex == -1)
                this.cbxDevice.SelectedIndex = 0;

            InitCaptureInterface();

            if (SetupOutPut(false))
                Run();
        }

        #endregion

        #region Form_Closing

        private void CaptureGraphForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            Cleanup(true);
            using (Synchronizer.Lock(this.m_ViewerState.SyncRoot))
            {
                this.m_ViewerState.DeviceName = this.m_DeviceName;
                this.m_ViewerState.OutPutSize = this.m_OutPutSize;
            }
        }

        #endregion

        #region FindDevice
        //
        // get a list of video capture devices found in the system
        //
        private void FindDevice()
        {
            ICreateDevEnum de = null;
            try
            {
                // create the device enumerator (COM) object
                Type t = Type.GetTypeFromCLSID(CLSID_SystemDeviceEnum);
                de = (ICreateDevEnum)Activator.CreateInstance(t);

                IEnumMoniker em;

                // get an enumerator for the video caputre devices
                de.CreateClassEnumerator(ref CLSID_VideoInputDeviceCategory, out em, 0);

                IMoniker mon;
                uint result;
                object o, name;
                IPropertyBag pBag;

                // get a reference to the 1st device
                em.RemoteNext(1, out mon, out result);

                while (result == 1)
                {
                    // get our device ready
                    mon.RemoteBindToObject(null, null, ref IID_IBaseFilter, out o);
                    this.SourceFilterList.Add(o);
                    // get device FriendlyName
                    mon.RemoteBindToStorage(null, null, ref IID_IPropertyBag, out o);
                    pBag = (IPropertyBag)o;
                    pBag.RemoteRead("FriendlyName", out name, null, 0, null);

                    this.cbxDevice.Items.Add((string)name);
                    // go to next device
                    em.RemoteNext(1, out mon, out result);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error when find device: " + ex.Message);
            }
            finally
            {
                Marshal.ReleaseComObject(de);
            }
        }

        #endregion

        #region Initialize Video Capture interface

        private void InitCaptureInterface()
        {
            // release com object (useless here but can't hurt)
            Cleanup(true);

            this.fmc = new FilgraphManagerClass();

            // create the cg object and add the filter graph to it
            Type t = Type.GetTypeFromCLSID(CLSID_CaptureGraphBuilder2);
            this.icgb = (ICaptureGraphBuilder2)Activator.CreateInstance(t);

            t = Type.GetTypeFromCLSID(CLSID_SampleGrabber);
            this.isg = (ISampleGrabber)Activator.CreateInstance(t);

            // source filter (the capture device)
            this.sf = (IBaseFilter)this.SourceFilterList[this.cbxDevice.SelectedIndex];
            // sample grabber filter
            this.sgf = (IBaseFilter)this.isg;

            object o = null;
            this.icgb.RemoteFindInterface(ref PIN_CATEGORY_CAPTURE, ref MEDIATYPE_Video, sf, ref IID_IAMStreamConfig, out o);
            this.iamsc = (IAMStreamConfig)o;

            // set sample grabber media type
            this.SGMediaType = new _AMMediaType();
            this.SGMediaType.majortype = MEDIATYPE_Video;
            this.SGMediaType.subtype = MEDIASUBTYPE_RGB24;
            this.SGMediaType.formattype = FORMAT_VideoInfo;
            this.isg.SetMediaType(ref SGMediaType);

            this.isg.SetOneShot(0);
            this.isg.SetBufferSamples(1);
        }

        #endregion

        #region Setup OutPut

        private bool SetupOutPut(bool output)
        {
            if (!output)
            {
                GetOutPutCapability();

                if (this.OutPutSizeList.Count <= 0)
                    return false;

                if (this.m_OutPutSize != string.Empty)
                {
                    for (int i = 0; i < this.cbxSize.Items.Count; i++)
                    {
                        if (string.Equals(this.m_OutPutSize, this.cbxSize.Items[i]))
                        {
                            this.cbxSize.SelectedIndex = i;
                            break;
                        }
                    }
                }
            }
            if (this.cbxSize.SelectedIndex == -1)
                this.cbxSize.SelectedIndex = 0;

            return SetOutPutSize();
        }

        #endregion

        #region Run

        private void Run()
        {
            if (this.fmc == null)
                return;

            IGraphBuilder igb = (IGraphBuilder)this.fmc;

            try
            {
                // tell the graph builder about the filter graph
                this.icgb.SetFiltergraph(igb);

                igb.AddFilter(this.sf, "Video Capture");
                igb.AddFilter(this.sgf, "Sample Grabber");

                this.icgb.RenderStream(ref PIN_CATEGORY_CAPTURE, ref MEDIATYPE_Video, this.sf, this.sgf, null);

                // access different interfaces, ask runtime to notify this window
                this.ime = (IMediaEventEx)this.fmc;
                this.ime.SetNotifyWindow((int)this.Handle, WM_GRAPHNOTIFY, 0);

                // sets the video owner and style of this window
                this.ivw = (IVideoWindow)this.fmc;
                this.ivw.Owner = (int)this.Handle;
                this.ivw.WindowStyle = WS_CHILD | WS_CLIPCHILDREN | WS_CLIPSIBLINGS;

                ivw.SetWindowPosition(this.ClientRectangle.Left, this.ClientRectangle.Top, this.ClientRectangle.Width, this.ClientSize.Height - this.btnCapture.Height - vertical_space);

                // get the ball rolling
                this.fmc.Run();
            }
            catch (Exception ex)
            {
                Cleanup(false);
                MessageBox.Show("Couldn't run: " + ex.Message);
            }
        }

        #endregion

        #region Get OutPut Capability

        private void GetOutPutCapability()
        {
            if (this.iamsc == null)
                return;

            int iCount = 0, iSize = 0;
            VIDEO_STREAM_CONFIG_CAPS scc;
            IntPtr ipscc = Marshal.AllocCoTaskMem(Marshal.SizeOf(typeof(VIDEO_STREAM_CONFIG_CAPS)));
            _AMMediaType mt, cmt;
            IntPtr ipmt = Marshal.AllocCoTaskMem(Marshal.SizeOf(typeof(_AMMediaType)));
            IntPtr ipcmt = Marshal.AllocCoTaskMem(Marshal.SizeOf(typeof(_AMMediaType)));

            try
            {
                // clear output size list first
                this.OutPutSizeList.Clear();
                this.MediaTypeList.Clear();
                this.cbxSize.Items.Clear();

                iamsc.GetFormat(out ipmt);

                cmt = (_AMMediaType)Marshal.PtrToStructure(ipmt, typeof(_AMMediaType));

                iamsc.GetNumberOfCapabilities(out iCount, out iSize);

                if (iSize == Marshal.SizeOf(typeof(VIDEO_STREAM_CONFIG_CAPS)))
                {
                    for (int iFormat = 0; iFormat < iCount; iFormat++)
                    {
                        iamsc.GetStreamCaps(iFormat, out ipmt, ipscc);
                        mt = (_AMMediaType)Marshal.PtrToStructure(ipmt, typeof(_AMMediaType));
                        scc = (VIDEO_STREAM_CONFIG_CAPS)Marshal.PtrToStructure(ipscc, typeof(VIDEO_STREAM_CONFIG_CAPS));

                        if ((mt.majortype == cmt.majortype) &&
                            (mt.subtype == cmt.subtype) &&
                            (mt.formattype == cmt.formattype) &&
                            (mt.cbFormat >= Marshal.SizeOf(typeof(VIDEOINFOHEADER))) &&
                            (mt.pbFormat != null))
                        {
                            this.OutPutSizeList.Add(scc.MaxOutputSize);

                            VIDEOINFOHEADER vih = (VIDEOINFOHEADER)Marshal.PtrToStructure(mt.pbFormat,
                                typeof(VIDEOINFOHEADER));
                            BITMAPINFOHEADER bih = vih.BitmapInfo;

                            bih.Width = scc.MaxOutputSize.Width;
                            bih.Height = scc.MaxOutputSize.Height;
                            bih.SizeImage = BitmapSize(vih.BitmapInfo);
                            vih.BitmapInfo = bih;
                            this.MediaTypeList.Add(mt);
                            this.cbxSize.Items.Add(scc.MaxOutputSize.Width.ToString() + "x" + scc.MaxOutputSize.Height.ToString());
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Fail to get output size: " + ex.Message);
            }
            finally
            {
                if (ipcmt != IntPtr.Zero)
                    Marshal.FreeCoTaskMem(ipcmt);
                if (ipscc != IntPtr.Zero)
                    Marshal.FreeCoTaskMem(ipscc);
                if(ipmt != IntPtr.Zero)
                    Marshal.FreeCoTaskMem(ipmt);
            }
        }

        #endregion

        #region Set OutPut Size

        private bool SetOutPutSize()
        {
            Size videosize = (Size)this.OutPutSizeList[this.cbxSize.SelectedIndex];
            _AMMediaType mt = (_AMMediaType)this.MediaTypeList[this.cbxSize.SelectedIndex];

            if (this.iamsc == null)
                return false;

            try
            {
                iamsc.SetFormat(ref mt);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Fail to set output size: " + ex.Message);
                return false;
            }
            if (videosize.Width < 320)
            {
                videosize.Width = 320;
                videosize.Height = 240;
            }
            this.ClientSize = new Size(videosize.Width, videosize.Height + this.btnCapture.Height + vertical_space);

            return true;
        }

        #endregion

        #region Cleanup

        private void Cleanup(bool stop)
        {
            if (this.fmc != null && stop)
            {
                this.fmc.Stop();
            }
            if (this.ime != null)
            {
                this.ime.SetNotifyWindow(0, WM_GRAPHNOTIFY, 0);
                this.ime = null;
            }
            if (this.ivw != null)
            {
                this.ivw.Owner = 0;
                this.ivw = null;
            }
            if (this.isg != null)
                Marshal.ReleaseComObject(this.isg);
            if (this.fmc != null)
                Marshal.ReleaseComObject(this.fmc);
            if (this.iamsc != null)
                Marshal.ReleaseComObject(this.iamsc);
            if (this.icgb != null)
                Marshal.ReleaseComObject(this.icgb);
        }

        #endregion

        #region Static Method

        private static uint BitmapSize(BITMAPINFOHEADER bih)
        {
            uint bis = (uint)((((uint)bih.Width * (uint)bih.BitCount) + 31) & (~31)) / 8;

            return (uint)((uint)bih.Height * bis);
        }

        public static bool HasCamera()
        {
            bool hascamera = false;
            ICreateDevEnum de = null;
            try
            {
                // create the device enumerator (COM) object
                Type t = Type.GetTypeFromCLSID(CLSID_SystemDeviceEnum);
                de = (ICreateDevEnum)Activator.CreateInstance(t);

                IEnumMoniker em;

                // get an enumerator for the video caputre devices
                de.CreateClassEnumerator(ref CLSID_VideoInputDeviceCategory, out em, 0);

                IMoniker mon;
                uint result = 0;

                // get a reference to the 1st device
                if (em != null)
                    em.RemoteNext(1, out mon, out result);

                if (result == 1)
                    hascamera = true;
            }
            catch (Exception)
            {
                
            }
            finally
            {
                Marshal.ReleaseComObject(de);
            }

            return hascamera;
        }
        #endregion

        #region Event Handle

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_GRAPHNOTIFY)
            {
                if (ime != null)
                    OnGraphNotify();
                return;
            }
            base.WndProc(ref m);
        }

        private void OnGraphNotify()
        {
            int p1, p2;
            int code;
            try
            {
                do
                {
                    if (ime == null)
                        return;
                    ime.GetEvent(out code, out p1, out p2, 0);
                    ime.FreeEventParams(code, p1, p2);
                }
                while (true);
            }
            catch (Exception) { }
        }

        #endregion

        #region btnCapture Click

        private void btnCapture_Click(object sender, EventArgs e)
        {
            isg.GetConnectedMediaType(ref SGMediaType);

            VIDEOINFOHEADER vih = (VIDEOINFOHEADER)Marshal.PtrToStructure(SGMediaType.pbFormat,
                typeof(VIDEOINFOHEADER));
            // Get a copy of the BITMAPINFOHEADER, to be used in the BITMAPFILEHEADER
            BITMAPINFOHEADER bih = vih.BitmapInfo;
            int len = (int)BitmapSize(bih);
            // Allocate bytes, plus room for a BitmapFileHeader
            int sizeOfBFH = Marshal.SizeOf(typeof(BITMAPFILEHEADER));
            int sizeOfBIH = Marshal.SizeOf(typeof(BITMAPINFOHEADER));
            IntPtr ptrBlock = Marshal.AllocCoTaskMem(len + sizeOfBFH + sizeOfBIH);
            IntPtr ptrBIH = new IntPtr(ptrBlock.ToInt64() + sizeOfBFH);
            IntPtr ptrImg = new IntPtr(ptrBlock.ToInt64() + sizeOfBFH + sizeOfBIH);

            try
            {
                // Get the DIB
                isg.GetCurrentBuffer(ref len, ptrImg);

                // Create header for a file of type .bmp
                BITMAPFILEHEADER bfh = new BITMAPFILEHEADER();
                bfh.Type = (UInt16)((((byte)'M') << 8) | ((byte)'B'));
                bfh.Size = (uint)(len + sizeOfBFH + sizeOfBIH);
                bfh.Reserved1 = 0;
                bfh.Reserved2 = 0;
                bfh.OffBits = (uint)(sizeOfBFH + sizeOfBIH);

                // Copy the BFH into unmanaged memory, so that we can copy
                // everything into a managed byte array all at once
                Marshal.StructureToPtr(bfh, ptrBlock, false);

                Marshal.StructureToPtr(bih, ptrBIH, false);

                // Pull it out of unmanaged memory into a managed byte[]
                byte[] img = new byte[len + sizeOfBFH + sizeOfBIH];
                Marshal.Copy(ptrBlock, img, 0, len + sizeOfBFH + sizeOfBIH);

                //System.IO.File.WriteAllBytes("cxp.bmp", img);

                System.IO.MemoryStream m = new System.IO.MemoryStream(img);
                this.m_Image = Image.FromStream(m);
                this.DialogResult = DialogResult.OK;
            }
            finally
            {
                // Free the unmanaged memory
                if (ptrBlock != IntPtr.Zero)
                {
                    Marshal.FreeCoTaskMem(ptrBlock);
                }
                this.Close();
            }
        }

        #endregion

        #region cbxSize SelectedIndexChanged

        private void cbxSize_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (ivw != null)
            {
                InitCaptureInterface();
                if (SetupOutPut(true))
                    Run();
                this.m_OutPutSize = (string)this.cbxSize.Items[this.cbxSize.SelectedIndex];
            }
        }

        #endregion

        #region cbxDevice SelectedIndexChanged

        private void cbxDevice_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (ivw != null)
            {
                InitCaptureInterface();
                if (SetupOutPut(false))
                    Run();
                this.m_DeviceName = (string)this.cbxDevice.Items[this.cbxDevice.SelectedIndex];
            }
        }

        #endregion

        public Image Image
        {
            get { return this.m_Image; }
        }
    }

    #region Structure

    /// <summary>
    /// From WinGDI.h
    /// </summary>
    [Serializable]
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct BITMAPFILEHEADER
    {
        public UInt16 Type;
        public UInt32 Size;
        public UInt16 Reserved1;
        public UInt16 Reserved2;
        public UInt32 OffBits;
    }


    /// <summary>
    /// Located in Platform SDK\include\WinDefs.h, WTypes.h, WTypes.idl
    /// </summary>
    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        private int left;
        private int top;
        private int right;
        private int bottom;

        public int Left
        {
            get { return left; }
            set { left = value; }
        }

        public int Top
        {
            get { return top; }
            set { top = value; }
        }

        public int Right
        {
            get { return right; }
            set { right = value; }
        }

        public int Bottom
        {
            get { return bottom; }
            set { bottom = value; }
        }


        public override string ToString()
        {
            return string.Format(CultureInfo.InvariantCulture, "L: {0}, T: {1}, R: {2}, B: {3}",
                left, top, right, bottom);
        }

    }

    /// <summary>
    /// Located in Platform SDK\include\Amvideo.h
    /// </summary>
    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    public struct VIDEOINFOHEADER
    {
        private RECT source;
        private RECT target;
        private uint bitRate;
        private uint bitErrorRate;
        private long avgTimePerFrame;
        private BITMAPINFOHEADER bitmapInfo;

        public RECT Source
        {
            get { return source; }
            set { source = value; }
        }

        public RECT Target
        {
            get { return target; }
            set { target = value; }
        }

        public uint BitRate
        {
            get { return bitRate; }
            set { bitRate = value; }
        }

        public uint BitErrorRate
        {
            get { return bitErrorRate; }
            set { bitErrorRate = value; }
        }

        public long AvgTimePerFrame
        {
            get { return avgTimePerFrame; }
            set { avgTimePerFrame = value; }
        }

        public BITMAPINFOHEADER BitmapInfo
        {
            get { return bitmapInfo; }
            set { bitmapInfo = value; }
        }

        public double FrameRate
        {
            get { return Math.Round(10000000D / AvgTimePerFrame, 2); }
            set { AvgTimePerFrame = (long)(10000000D / value); }
        }


        public override string ToString()
        {
            return string.Format(CultureInfo.CurrentCulture,
                "VIDEOINFOHEADER - Source: {0}  Target: {1}  BitRate: {2}  BitErrorRate: {3}  AvgTimePerFrame: {4}\n{5}",
                Source.ToString(), Target.ToString(), BitRate, BitErrorRate, AvgTimePerFrame, BitmapInfo.ToString());
        }
    }

    /// <summary>
    /// Located in Platform SDK\include\WinGDI.h
    /// </summary>
    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    public struct BITMAPINFOHEADER
    {
        private uint size;
        private int width;
        private int height;
        private ushort planes;
        private ushort bitCount;
        private uint compression;
        private uint sizeImage;
        private int xPelsPerMeter;
        private int yPelsPerMeter;
        private uint clrUsed;
        private uint clrImportant;

        public uint Size
        {
            get { return size; }
            set { size = value; }
        }

        public int Width
        {
            get { return width; }
            set { width = value; }
        }

        public int Height
        {
            get { return height; }
            set { height = value; }
        }

        public ushort Planes
        {
            get { return planes; }
            set { planes = value; }
        }

        public ushort BitCount
        {
            get { return bitCount; }
            set { bitCount = value; }
        }

        public uint Compression
        {
            get { return compression; }
            set { compression = value; }
        }

        public uint SizeImage
        {
            get { return sizeImage; }
            set { sizeImage = value; }
        }

        public int XPelsPerMeter
        {
            get { return xPelsPerMeter; }
            set { xPelsPerMeter = value; }
        }

        public int YPelsPerMeter
        {
            get { return yPelsPerMeter; }
            set { yPelsPerMeter = value; }
        }

        public uint ClrUsed
        {
            get { return clrUsed; }
            set { clrUsed = value; }
        }

        public uint ClrImportant
        {
            get { return clrImportant; }
            set { clrImportant = value; }
        }


        public override string ToString()
        {
            return string.Format(CultureInfo.CurrentCulture,
                "BITMAPINFOHEADER - Size: {0},  Width: {1},  Height: {2},  Planes: {3},  BitCount: {4},  " +
                "Compression: {5},  SizeImage: {6},  XPelsPermeter: {7},  YPelsPerMeter: {8},  ClrUsed: {9},  ClrImportant: {10}",
                Size, Width, Height, Planes, BitCount, Compression, SizeImage, XPelsPerMeter, YPelsPerMeter,
                ClrUsed, ClrImportant);
        }
    }

    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    public struct VIDEO_STREAM_CONFIG_CAPS
    {
        public Guid guid;
        public uint VideoStandard;
        public Size InputSize;
        public Size MinCroppingSize;
        public Size MaxCroppingSize;
        public int CropGranularityX;
        public int CropGranularityY;
        public int CropAlignX;
        public int CropAlignY;
        public Size MinOutputSize;
        public Size MaxOutputSize;
        public int OutputGranularityX;
        public int OutputGranularityY;
        public int StretchTapsX;
        public int StretchTapsY;
        public int ShrinkTapsX;
        public int ShrinkTapsY;
        public long MinFrameInterval;
        public long MaxFrameInterval;
        public int MinBitsPerSecond;
        public int MaxBitsPerSecond;
    }

    #endregion
}
