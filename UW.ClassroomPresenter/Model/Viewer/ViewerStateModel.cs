// $Id: ViewerStateModel.cs 2233 2013-09-27 22:17:28Z fred $

using System;
using System.Threading;
using UW.ClassroomPresenter.Model;
using System.Windows.Forms;
using UW.ClassroomPresenter.Model.Network;
using UW.ClassroomPresenter.Model.Presentation;
using System.Drawing.Printing;
using System.Drawing.Drawing2D;
using System.Drawing;

namespace UW.ClassroomPresenter.Model.Viewer {

    /// <summary>
    /// Encapsulates the state of the local machine, such as whether the
    /// second monitor view can/should be displayed
    /// Can also be used to hold many of the local user-defined properties.
    /// </summary>
    public class ViewerStateModel : PropertyPublisher, ICloneable {

        // Shared constants
        public const float ZOOM_FACTOR = (3f/4f); // 133%

        private DiagnosticModel m_Diagnostic;
        private WebPerformanceModel m_WebPerformance;

        // Published properties:
        private bool m_SecondMonitorEnabled;        // Is the second monitor enabled
        private bool m_SecondMonitorWindowsAPIsEnabled; // Use windows APIs to switch between cloned and extended
        private bool m_SecondMonitorCustomCommandsEnabled; // Use custom commands to switch between cloned and extended
        private string m_SecondMonitorCustomCommandType; // The type of custom commands to use
        private string m_SecondMonitorCloneCommand; // Clone command for this adapter
        private string m_SecondMonitorExtendCommand; // The Extend command for this adapter
        private int m_NumberOfScreens;              // The number of desktop screens on the machine

        private bool m_SlidePreviewEnabled;         // Is the slide preview enabled
        private int m_SlidePreviewWidth;            // The width of slide preview window
        private int m_SlidePreviewHeight;           // The height of slide preview window
        private bool m_SlidePreviewVisible;         // Has the user invoked the slide preview
        private bool m_AutoScrollEnabled;           // Has the user invoked the autoscroll
        private bool m_FilmStripEnabled;            // Has the user invoded the filmstrip
        private DockStyle m_FilmStripAlignment;     // The alignment of the filmstrip
        private int m_FilmStripWidth;               // How many slides wide the (non student sub)filmstrip is 
        private int m_SSFilmStripWidth;             // How many slides wide the (student sub)filmstrip is 
        private bool m_PrimaryMonitorFullScreen;    // Is the primary slide viewer in full-screen mode
        private int m_FEC;                          // RTP Network Forward Error Correction percentage
        private int m_InterPacketDelay;             // Milliseconds between RTP packets
        private int m_BeaconInterval;               // Seconds between RTP beacons
        private bool m_StudentSubmissionSignal;     // Signal property that the user wants to send his/her ink
        private bool m_SaveOnClose;                 // Whether to prompt to save decks before closing
        private bool m_BroadcastDisabled;           // Instructor advertisement broadcast disabled
        private bool m_UseLightColorSet;            // Use light color set as the pen color set
        private bool m_ClassmateMode;               // Change the position of toolbar for Classmate PC
        private int m_DefaultPenWidth;              // Width of Pen
        private int m_DefaultHLWidth;               // Width of Highlighter
        private int m_StudentSubmissionInterval;    // Minium time interval for student submissions 
        private string m_Language;                  // Language using to display strings in UI
        private string m_DeviceName;                // Name of camera
        private string m_OutPutSize;                // Size of the camera output
        private LinkedDeckTraversalModel.NavigationSelector m_StudentNavigationType;        //Student navigation type.
        
        private int m_iRole;
        private bool m_Advanced;
        private String m_ManualConnectionButtonName;
        private string m_TCPaddress;
        private int m_TCPport = UW.ClassroomPresenter.Network.TCP.TCPServer.DefaultPort;
        private bool m_ShowIP;

        // Printing
        private PrintDocument m_Document;
        private DeckTraversalModel m_PrintableDeck;
        private int m_SlidesPerPage;

        private QuickPollModel.QuickPollStyle m_PollStyle;

        // Font
        private static Font m_FormFont;
        private static Font m_StringFont;
        private static Font m_StringFont1;
        private static Font m_StringFont2;
        
        private const float CANNONICAL_DPI = 96.0f; // Ink on the network will be normalized to this DPI setting.

        #region Static
        private static float DpiX = CANNONICAL_DPI;
        private static float DpiY = CANNONICAL_DPI;
        private static Matrix SendMatrix = new Matrix();
        private static Matrix ReceiveMatrix = new Matrix();

        /// <summary>
        /// If true, the local system uses a DPI setting other than the standard (96), and ink scaling is required
        /// on send and receive.
        /// </summary>
        public static bool NonStandardDpi = false;

        /// <summary>
        /// Call once when app is launched to initialize for ink scaling with local system DPI settings
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        public static void SetLocalDpi(float x, float y) {
            DpiX = x;
            DpiY = y;
            if ((DpiX != CANNONICAL_DPI) || (DpiY != CANNONICAL_DPI)) {
                NonStandardDpi = true;
                SendMatrix = new Matrix(DpiX / CANNONICAL_DPI, 0.0f, 0.0f, DpiY / CANNONICAL_DPI, 0.0f, 0.0f);
                ReceiveMatrix = SendMatrix.Clone();
                ReceiveMatrix.Invert();
            }
        }

        /// <summary>
        /// If NonStandardDpi, use this matrix to transform ink prior to sending.
        /// </summary>
        public static Matrix DpiNormalizationSendMatrix {
            get { return SendMatrix; }
        }

        /// <summary>
        /// If NonStandardDpi, use this matrix to transform ink after receiving.
        /// </summary>
        public static Matrix DpiNormalizationReceiveMatrix {
            get { return ReceiveMatrix; }
        }

        public static void SetUIFont(string family, float size) {
            m_FormFont = new Font("Tahoma", size);
            m_StringFont = new Font(family, size);
            m_StringFont1 = new Font(family, size * 1.1F);
            m_StringFont2 = new Font(family, size * 1.2F);
        }

        /// <summary>
        /// UI Font used in Forms.
        /// </summary>
        public static Font FormFont
        {
            get { return m_FormFont; }
        }

        /// <summary>
        /// UI Font used in Strings.
        /// </summary>
        public static Font StringFont
        {
            get { return m_StringFont; }
        }

        /// <summary>
        /// UI Font used in Strings with factor 1.1F.
        /// </summary>
        public static Font StringFont1
        {
            get { return m_StringFont1; }
        }

        /// <summary>
        /// UI Font used in Strings with factor 1.2F.
        /// </summary>
        public static Font StringFont2
        {
            get { return m_StringFont2; }
        }

        #endregion Static

        #region Properties

        /// <summary>
        /// Value of the current default Quick Poll Style
        /// </summary>
        [Published] public QuickPollModel.QuickPollStyle PollStyle {
            get { return this.GetPublishedProperty( "PollStyle", ref this.m_PollStyle ); }
            set { this.SetPublishedProperty( "PollStyle", ref this.m_PollStyle, value ); }
        }

        /// <summary>
        /// Value of the current Windows Printer Document
        /// </summary>
        [Published] public PrintDocument Document {
            get { return this.GetPublishedProperty( "Document", ref this.m_Document ); }
            set { this.SetPublishedProperty( "Document", ref this.m_Document, value ); }
        }

        /// <summary>
        /// Value of the current document that we should use for printing
        /// </summary>
        [Published] public DeckTraversalModel PrintableDeck {
            get { return this.GetPublishedProperty( "PrintableDeck", ref this.m_PrintableDeck ); }
            set { this.SetPublishedProperty( "PrintableDeck", ref this.m_PrintableDeck, value ); }
        }

        /// <summary>
        /// Value of the number of pages per sheet for printing, valid values are 1, 2, 4, and 6
        /// </summary>
        [Published][Saved] public int SlidesPerPage {
            get { return this.GetPublishedProperty( "SlidesPerPage", ref this.m_SlidesPerPage ); }
            set { this.SetPublishedProperty( "SlidesPerPage", ref this.m_SlidesPerPage, value ); }
        }

        /// <summary>
        /// Value whether logging is enabled or not
        /// </summary>
        private bool m_LoggingEnabled;
        [Published][Saved] public bool LoggingEnabled {
            get { return this.GetPublishedProperty( "LoggingEnabled", ref this.m_LoggingEnabled ); }
            set { this.SetPublishedProperty( "LoggingEnabled", ref this.m_LoggingEnabled, value ); }
        }

        /// <summary>
        /// The path to use to store log files
        /// </summary>
        private string m_LoggingPath;
        [Published][Saved] public string LoggingPath {
            get { return this.GetPublishedProperty( "LoggingPath", ref this.m_LoggingPath ); }
            set { this.SetPublishedProperty( "LoggingPath", ref this.m_LoggingPath, value ); }
        }

        /// <summary>
        /// Value whether the user has defined the second monitor to be enabled or not
        /// </summary>
        [Published][Saved] public bool SecondMonitorEnabled {
            get { return this.GetPublishedProperty("SecondMonitorEnabled", ref this.m_SecondMonitorEnabled); }
            set { this.SetPublishedProperty("SecondMonitorEnabled", ref this.m_SecondMonitorEnabled, value); }
        }
        /// <summary>
        /// Value whether the user has defined the second monitor to be enabled or not
        /// </summary>
        [Published][Saved]
        public bool SecondMonitorWindowsAPIsEnabled {
            get { return this.GetPublishedProperty( "SecondMonitorWindowsAPIsEnabled", ref this.m_SecondMonitorWindowsAPIsEnabled ); }
            set { this.SetPublishedProperty( "SecondMonitorWindowsAPIsEnabled", ref this.m_SecondMonitorWindowsAPIsEnabled, value ); }
        }
        /// <summary>
        /// Value whether the user has defined the second monitor to be enabled or not
        /// </summary>
        [Published][Saved]
        public bool SecondMonitorCustomCommandsEnabled {
            get { return this.GetPublishedProperty( "SecondMonitorCustomCommandsEnabled", ref this.m_SecondMonitorCustomCommandsEnabled ); }
            set { this.SetPublishedProperty( "SecondMonitorCustomCommandsEnabled", ref this.m_SecondMonitorCustomCommandsEnabled, value ); }
        }
        /// <summary>
        /// The type of commands used to clone/extend the desktop on the second monitor
        /// </summary>
        [Published][Saved]
        public string SecondMonitorCustomCommandType {
            get { return this.GetPublishedProperty( "SecondMonitorCustomCommandType", ref this.m_SecondMonitorCustomCommandType ); }
            set { this.SetPublishedProperty( "SecondMonitorCustomCommandType", ref this.m_SecondMonitorCustomCommandType, value ); }
        }
        /// <summary>
        /// The custom command used to clone the desktop to the second monitor
        /// </summary>
        [Published][Saved]
        public string SecondMonitorCloneCommand {
            get { return this.GetPublishedProperty( "SecondMonitorCloneCommand", ref this.m_SecondMonitorCloneCommand ); }
            set { this.SetPublishedProperty( "SecondMonitorCloneCommand", ref this.m_SecondMonitorCloneCommand, value ); }
        }
        /// <summary>
        /// The custom command used to extend the desktop on the second monitor
        /// </summary>
        [Published][Saved]
        public string SecondMonitorExtendCommand {
            get { return this.GetPublishedProperty( "SecondMonitorExtendCommand", ref this.m_SecondMonitorExtendCommand ); }
            set { this.SetPublishedProperty( "SecondMonitorExtendCommand", ref this.m_SecondMonitorExtendCommand, value ); }
        }


        /// <summary>
        /// Value whether the primary slide viewer should take up the full screen.
        /// </summary>
        [Published] public bool PrimaryMonitorFullScreen {
            get { return this.GetPublishedProperty("PrimaryMonitorFullScreen", ref this.m_PrimaryMonitorFullScreen); }
            set { this.SetPublishedProperty("PrimaryMonitorFullScreen", ref this.m_PrimaryMonitorFullScreen, value); }
        }

        /// <summary>
        /// The number of screens that are on this device
        /// </summary>
        [Published] public int NumberOfScreens {
            get { return this.GetPublishedProperty("NumberOfScreens", ref this.m_NumberOfScreens); }
            set { this.SetPublishedProperty("NumberOfScreens", ref this.m_NumberOfScreens, value); }
        }

        /// <summary>
        /// Value indicating whether the user has enabled the slide preview feature or not
        /// </summary>
        [Published][Saved] public bool SlidePreviewEnabled {
            get { return this.GetPublishedProperty("SlidePreviewEnabled", ref this.m_SlidePreviewEnabled); }
            set { this.SetPublishedProperty("SlidePreviewEnabled", ref this.m_SlidePreviewEnabled, value); }
        }

        /// <summary>
        /// This is set true by a control when the UI actions of the user should display the slide preview
        /// </summary>
        [Published] public bool SlidePreviewVisible {
            get { return this.GetPublishedProperty("SlidePreviewVisible", ref this.m_SlidePreviewVisible); }
            set { this.SetPublishedProperty("SlidePreviewVisible", ref this.m_SlidePreviewVisible, value); }
        }

        /// <summary>
        /// The width of slide preview window
        /// </summary>
        [Published]
        [Saved]
        public int SlidePreviewWidth
        {
            get { return this.GetPublishedProperty("SlidePreviewWidth", ref this.m_SlidePreviewWidth); }
            set { this.SetPublishedProperty("SlidePreviewWidth", ref this.m_SlidePreviewWidth, value); }
        }

        /// <summary>
        /// The height of slide preview window
        /// </summary>
        [Published]
        [Saved]
        public int SlidePreviewHeight
        {
            get { return this.GetPublishedProperty("SlidePreviewHeight", ref this.m_SlidePreviewHeight); }
            set { this.SetPublishedProperty("SlidePreviewHeight", ref this.m_SlidePreviewHeight, value); }
        }

        /// <summary>
        /// If true, then we want to enable auto-scrolling, false otherwise
        /// </summary>
        [Published][Saved] public bool AutoScrollEnabled {
            get { return this.GetPublishedProperty("AutoScrollEnabled", ref this.m_AutoScrollEnabled); }
            set { this.SetPublishedProperty("AutoScrollEnabled", ref this.m_AutoScrollEnabled, value); }
        }

        /// <summary>
        /// If true, then we want to display the filmstrip, false otherwise
        /// </summary>
        [Published][Saved] public bool FilmStripEnabled {
            get { return this.GetPublishedProperty("FilmStripEnabled", ref this.m_FilmStripEnabled); }
            set { this.SetPublishedProperty("FilmStripEnabled", ref this.m_FilmStripEnabled, value); }
        }

        /// <summary>
        /// The alignment of the FilmStrip.
        /// </summary>
        [Published]
        [Saved]
        public DockStyle FilmStripAlignment {
            get { return this.GetPublishedProperty("FilmStripAlignment", ref this.m_FilmStripAlignment); }
            set { this.SetPublishedProperty("FilmStripAlignment", ref this.m_FilmStripAlignment, value); }
        }

   
        /// <summary>
        /// The width of the  non-SS FilmStrip (in slides)
        /// </summary>
        [Published]
        [Saved]
        public int FilmStripWidth
        {
            get { return this.GetPublishedProperty("FilmStripWidth", ref this.m_FilmStripWidth); }
            set { this.SetPublishedProperty("FilmStripWidth", ref this.m_FilmStripWidth, value); }
        }
        /// <summary>
        /// The width of the  SS FilmStrip (in slides)
        /// </summary>
        [Published]
        [Saved]
        public int SSFilmStripWidth
        {
            get { return this.GetPublishedProperty("SSFilmStripWidth", ref this.m_SSFilmStripWidth); }
            set { this.SetPublishedProperty("SSFilmStripWidth", ref this.m_SSFilmStripWidth, value); }
        }
        /// <summary>
        /// Percentage of Forward Error Correction used for RTP
        /// </summary>
        [Published][Saved] public int ForwardErrorCorrection {
            get { return this.GetPublishedProperty("ForwardErrorCorrection", ref this.m_FEC); }
            set { this.SetPublishedProperty("ForwardErrorCorrection", ref this.m_FEC, value); }
        }


        /// <summary>
        /// Seconds between RTP beacons
        /// </summary>
        [Published]
        [Saved]
        public int BeaconInterval {
            get { return this.GetPublishedProperty("BeaconInterval", ref this.m_BeaconInterval); }
            set { this.SetPublishedProperty("BeaconInterval", ref this.m_BeaconInterval, value); }
        }

        /// <summary>
        /// Milliseconds between RTP packets
        /// </summary>
        [Published][Saved] public int InterPacketDelay {
            get { return this.GetPublishedProperty("InterPacketDelay", ref this.m_InterPacketDelay); }
            set { this.SetPublishedProperty("InterPacketDelay", ref this.m_InterPacketDelay, value); }
        }

        /// <summary>
        /// Property used to signal when the user wished to submit his/her ink
        /// </summary>
        [Published] public bool StudentSubmissionSignal {
            get { return this.GetPublishedProperty( "StudentSubmissionSignal", ref this.m_StudentSubmissionSignal ); }
            set { this.SetPublishedProperty( "StudentSubmissionSignal", ref this.m_StudentSubmissionSignal, value ); }
        }

        
        /// <summary>
        /// Instructor, Student, Public Display?
        /// </summary>
        [Published][Saved] public int iRole {
            get { return this.GetPublishedProperty("iRole", ref this.m_iRole); }
            set { this.SetPublishedProperty("iRole", ref this.m_iRole, value); }
        }

        /// <summary>
        /// In the connection sequence, does this user use the "advanced" or "broadcasted" tab?
        /// </summary>
        [Published][Saved] public bool Advanced { 
            get { return this.GetPublishedProperty("Advanced", ref this.m_Advanced); }
            set { this.SetPublishedProperty("Advanced", ref this.m_Advanced, value); }
        }

        /// <summary>
        /// In the connection sequence, which "advanced" option does this user choose?
        /// </summary>
        [Published][Saved] public String ManualConnectionButtonName {
            get { return this.GetPublishedProperty("ManualConnectionButtonName", ref this.m_ManualConnectionButtonName); }
            set { this.SetPublishedProperty("ManualConnectionButtonName", ref this.m_ManualConnectionButtonName, value); }
        }

        /// <summary>
        /// In the connection sequence, which TCP address has the user input?
        /// </summary>
        [Published]
        [Saved]
        public string TCPaddress {
            get { return this.GetPublishedProperty("TCPaddress", ref this.m_TCPaddress); }
            set { this.SetPublishedProperty("TCPaddress", ref this.m_TCPaddress, value); }
        }

        /// <summary>
        /// TCP Port used for a manual connection
        /// </summary>
        [Published]
        [Saved]
        public int TCPport {
            get { return this.GetPublishedProperty("TCPport", ref this.m_TCPport); }
            set { this.SetPublishedProperty("TCPport", ref this.m_TCPport, value); }
        }

        [Published]
        [Saved]
        public bool ShowIP {
            get { return this.GetPublishedProperty("ShowIP", ref this.m_ShowIP); }
            set { this.SetPublishedProperty("ShowIP", ref this.m_ShowIP, value); }
        }


        /// <summary>
        /// Whether to prompt to save decks before closing. - set from the properties form
        /// </summary>
        [Published]
        [Saved]
        public bool SaveOnClose {
            get { return this.GetPublishedProperty("SaveOnClose", ref this.m_SaveOnClose); }
            set { this.SetPublishedProperty("SaveOnClose", ref this.m_SaveOnClose, value); }
        }

        /// <summary>
        /// Instructor Advertisement Broadcasts are disabled.  
        /// </summary>
        [Published]
        [Saved]
        public bool BroadcastDisabled {
            get { return this.GetPublishedProperty("BroadcastDisabled", ref this.m_BroadcastDisabled); }
            set { this.SetPublishedProperty("BroadcastDisabled", ref this.m_BroadcastDisabled, value); }
        }

        /// <summary>
        /// Use light color set or not.  
        /// </summary>
        [Published]
        [Saved]
        public bool UseLightColorSet {
            get { return this.GetPublishedProperty("UseLightColorSet", ref this.m_UseLightColorSet); }
            set { this.SetPublishedProperty("UseLightColorSet", ref this.m_UseLightColorSet, value); }
        }

        /// <summary>
        /// Use light color set or not.  
        /// </summary>
        [Published]
        [Saved]
        public bool ClassmateMode {
            get { return this.GetPublishedProperty("ClassmateMode", ref this.m_ClassmateMode); }
            set { this.SetPublishedProperty("ClassmateMode", ref this.m_ClassmateMode, value); }
        }

        /// <summary>
        /// Pen width.  
        /// </summary>
        [Published]
        [Saved]
        public int DefaultPenWidth
        {
            get { return this.GetPublishedProperty("DefaultPenWidth", ref this.m_DefaultPenWidth); }
            set { this.SetPublishedProperty("DefaultPenWidth", ref this.m_DefaultPenWidth, value); }
        }

        [Published]
        [Saved]
        public int DefaultHLWidth
        {
            get { return this.GetPublishedProperty("DefaultHLWidth", ref this.m_DefaultHLWidth); }
            set { this.SetPublishedProperty("DefaultHLWidth", ref this.m_DefaultHLWidth, value); }
        }

        [Published]
        [Saved]
        public string Language
        {
            get { return this.GetPublishedProperty("Language", ref this.m_Language); }
            set { this.SetPublishedProperty("Language", ref this.m_Language, value); }
        }

        [Published]
        [Saved]
        public string DeviceName
        {
            get { return this.GetPublishedProperty("DeviceName", ref this.m_DeviceName); }
            set { this.SetPublishedProperty("DeviceName", ref this.m_DeviceName, value); }
        }

        [Published]
        [Saved]
        public string OutPutSize
        {
            get { return this.GetPublishedProperty("OutPutSize", ref this.m_OutPutSize); }
            set { this.SetPublishedProperty("OutPutSize", ref this.m_OutPutSize, value); }
        }

        [Published]
        [Saved]
        public int StudentSubmissionInterval
        {
            get { return this.GetPublishedProperty("StudentSubmissionInterval", ref this.m_StudentSubmissionInterval); }
            set { this.SetPublishedProperty("StudentSubmissionInterval", ref this.m_StudentSubmissionInterval, value); }
        }

        public DiagnosticModel Diagnostic {
            get { return this.m_Diagnostic; }
        }

        public WebPerformanceModel WebPerformance {
            get { return this.m_WebPerformance; }
        }

        [Published]
        public LinkedDeckTraversalModel.NavigationSelector StudentNavigationType {
            get { return this.GetPublishedProperty("StudentNavigationType", ref this.m_StudentNavigationType); }
            set { this.SetPublishedProperty("StudentNavigationType", ref this.m_StudentNavigationType, value); }
        }

        #endregion
        
        /// <summary>
        /// Instantiates a new <see cref="ViewerStateModel"/>.
        /// </summary>
        public ViewerStateModel() {
            this.m_PollStyle = QuickPollModel.QuickPollStyle.ABCD;
            this.m_Document = new PrintDocument();
            this.m_PrintableDeck = null;
            this.m_SlidesPerPage = 6;
            this.m_LoggingEnabled = true;
            this.m_LoggingPath = ".\\Logs";
            this.m_SecondMonitorEnabled = true;
            this.m_SecondMonitorWindowsAPIsEnabled = true;
            this.m_SecondMonitorCustomCommandsEnabled = false;
            this.m_SecondMonitorCustomCommandType = "";
            this.m_SecondMonitorCloneCommand = "";
            this.m_SecondMonitorExtendCommand = "";
            this.m_NumberOfScreens = System.Windows.Forms.Screen.AllScreens.Length;
            this.m_SlidePreviewEnabled = true;
            this.m_SlidePreviewVisible = false;
            this.m_SlidePreviewWidth = 400;
            this.m_SlidePreviewHeight = 300;
            this.m_AutoScrollEnabled = true;
            this.m_FilmStripEnabled = true;
            this.m_PrimaryMonitorFullScreen = false;
            this.m_FEC = 0;
            this.m_InterPacketDelay = 0;
            this.m_BeaconInterval = 5;
            this.m_FilmStripAlignment = DockStyle.Right;
            this.m_SaveOnClose = true;
            this.m_BroadcastDisabled = false;
            this.m_UseLightColorSet = false;
            this.m_ClassmateMode = false;
            this.m_DefaultPenWidth = 53;
            this.m_DefaultHLWidth = 318;
            this.m_StudentSubmissionInterval = 5;
            this.m_Language = "en";
            this.m_DeviceName = string.Empty;
            this.m_OutPutSize = "320x240";

            this.m_FilmStripWidth = 1;
            this.m_SSFilmStripWidth = 3;
            this.m_iRole = 0;//0-disconnected, 1-Viewer, 2-Presenter, 3-Public
            this.m_Advanced = false;
            this.m_ManualConnectionButtonName = "";
            this.m_TCPaddress = "";
            this.m_ShowIP = true;
            this.m_StudentNavigationType = LinkedDeckTraversalModel.NavigationSelector.Full;

            this.m_Diagnostic = new DiagnosticModel();
            this.m_WebPerformance = new WebPerformanceModel();
        }

        public void UpdateValues(object model) {
            // Make sure both objects exist and aren't the same
            if (model != null &&
                model is ViewerStateModel &&
                model != this) {

                ViewerStateModel m = (ViewerStateModel)model;

                // Lock both objects
                using (Synchronizer.Lock(this.SyncRoot)) {
                    using (Synchronizer.Lock(m.SyncRoot)) {
                        // Copy over the values
                        if (this.SlidesPerPage != m.SlidesPerPage)
                            this.SlidesPerPage = m.SlidesPerPage;
                        if (this.LoggingPath != m.LoggingPath)
                            this.LoggingPath = m.LoggingPath;
                        if (this.LoggingEnabled != m.LoggingEnabled)
                            this.LoggingEnabled = m.LoggingEnabled;
                        if (this.NumberOfScreens != m.NumberOfScreens)
                            this.NumberOfScreens = m.NumberOfScreens;
                        if (this.SecondMonitorEnabled != m.SecondMonitorEnabled)
                            this.SecondMonitorEnabled = m.SecondMonitorEnabled;
                        if (this.SecondMonitorWindowsAPIsEnabled != m.SecondMonitorWindowsAPIsEnabled)
                            this.SecondMonitorWindowsAPIsEnabled = m.SecondMonitorWindowsAPIsEnabled;
                        if (this.SecondMonitorCustomCommandsEnabled != m.SecondMonitorCustomCommandsEnabled)
                            this.SecondMonitorCustomCommandsEnabled = m.SecondMonitorCustomCommandsEnabled;
                        if (this.SecondMonitorCustomCommandType != m.SecondMonitorCustomCommandType)
                            this.SecondMonitorCustomCommandType = m.SecondMonitorCustomCommandType;
                        if (this.SecondMonitorCloneCommand != m.SecondMonitorCloneCommand)
                            this.SecondMonitorCloneCommand = m.SecondMonitorCloneCommand;
                        if (this.SecondMonitorExtendCommand != m.SecondMonitorExtendCommand)
                            this.SecondMonitorExtendCommand = m.SecondMonitorExtendCommand;
                        if (this.SlidePreviewEnabled != m.SlidePreviewEnabled)
                            this.SlidePreviewEnabled = m.SlidePreviewEnabled;
                        if (this.SlidePreviewWidth != m.SlidePreviewWidth)
                            this.SlidePreviewWidth = m.SlidePreviewWidth;
                        if (this.SlidePreviewHeight != m.SlidePreviewHeight)
                            this.SlidePreviewHeight = m.SlidePreviewHeight;
                        if (this.AutoScrollEnabled != m.AutoScrollEnabled)
                            this.AutoScrollEnabled = m.AutoScrollEnabled;
                        if (this.ForwardErrorCorrection != m.ForwardErrorCorrection)
                            this.ForwardErrorCorrection = m.ForwardErrorCorrection;
                        if (this.InterPacketDelay != m.InterPacketDelay)
                            this.InterPacketDelay = m.InterPacketDelay;
                        if (this.BeaconInterval != m.BeaconInterval)
                            this.BeaconInterval = m.BeaconInterval;
                        if (this.SaveOnClose != m.SaveOnClose)
                            this.SaveOnClose = m.SaveOnClose;
                        if (this.ShowIP != m.ShowIP)
                            this.ShowIP = m.ShowIP;
                        if (this.BroadcastDisabled != m.BroadcastDisabled)
                            this.BroadcastDisabled = m.BroadcastDisabled;
                        if (this.UseLightColorSet != m.UseLightColorSet)
                            this.UseLightColorSet = m.UseLightColorSet;
                        if (this.ClassmateMode != m.ClassmateMode)
                            this.ClassmateMode = m.ClassmateMode;
                        if (this.DefaultPenWidth != m.DefaultPenWidth)
                            this.DefaultPenWidth = m.DefaultPenWidth;
                        if (this.DefaultHLWidth != m.DefaultHLWidth)
                            this.DefaultHLWidth = m.DefaultHLWidth;
                        if (this.Language != m.Language)
                            this.Language = m.Language;
                        if (this.DeviceName != m.DeviceName)
                            this.DeviceName = m.DeviceName;
                        if (this.OutPutSize != m.OutPutSize)
                            this.OutPutSize = m.OutPutSize;
                        if (this.StudentSubmissionInterval != m.StudentSubmissionInterval)
                            this.StudentSubmissionInterval = m.StudentSubmissionInterval;
                        if (this.StudentNavigationType != m.StudentNavigationType)
                            this.StudentNavigationType = m.StudentNavigationType;
                    }
                }
            }
        }

        #region ICloneable Members

        /// <summary>
        /// Clones this model object
        /// </summary>
        /// <returns>A Deep copy of this structure</returns>
        public object Clone() {
            // Create the clone
            ViewerStateModel clonedModel = new ViewerStateModel();

            // Copy the values
            clonedModel.m_Document = this.m_Document;
            clonedModel.m_PrintableDeck = this.m_PrintableDeck;
            clonedModel.m_SlidesPerPage = this.m_SlidesPerPage;
            clonedModel.m_LoggingEnabled = this.m_LoggingEnabled;
            clonedModel.m_LoggingPath = this.m_LoggingPath;
            clonedModel.m_NumberOfScreens = this.m_NumberOfScreens;
            clonedModel.m_SecondMonitorEnabled = this.m_SecondMonitorEnabled;
            clonedModel.m_SecondMonitorWindowsAPIsEnabled = this.m_SecondMonitorWindowsAPIsEnabled;
            clonedModel.m_SecondMonitorCustomCommandsEnabled = this.m_SecondMonitorCustomCommandsEnabled;
            clonedModel.m_SecondMonitorCustomCommandType = this.m_SecondMonitorCustomCommandType;
            clonedModel.m_SecondMonitorCloneCommand = this.m_SecondMonitorCloneCommand;
            clonedModel.m_SecondMonitorExtendCommand = this.m_SecondMonitorExtendCommand;
            clonedModel.m_SlidePreviewEnabled = this.m_SlidePreviewEnabled;
            clonedModel.m_SlidePreviewWidth = this.m_SlidePreviewWidth;
            clonedModel.m_SlidePreviewHeight = this.m_SlidePreviewHeight;
            clonedModel.m_AutoScrollEnabled = this.m_AutoScrollEnabled;
            clonedModel.m_FEC = this.m_FEC;
            clonedModel.m_InterPacketDelay = this.m_InterPacketDelay;
            clonedModel.m_BeaconInterval = this.m_BeaconInterval;
            clonedModel.m_Diagnostic = (DiagnosticModel) this.m_Diagnostic.Clone();
            clonedModel.m_WebPerformance = (WebPerformanceModel)this.m_WebPerformance.Clone();
            clonedModel.m_SaveOnClose = this.m_SaveOnClose;
            clonedModel.m_ShowIP = this.m_ShowIP;
            clonedModel.m_BroadcastDisabled = this.m_BroadcastDisabled;
            clonedModel.m_UseLightColorSet = this.m_UseLightColorSet;
            clonedModel.m_ClassmateMode = this.m_ClassmateMode;
            clonedModel.m_DefaultPenWidth = this.m_DefaultPenWidth;
            clonedModel.m_DefaultHLWidth = this.m_DefaultHLWidth;
            clonedModel.m_Language = this.m_Language;
            clonedModel.m_DeviceName = this.m_DeviceName;
            clonedModel.m_OutPutSize = this.m_OutPutSize;
            clonedModel.m_StudentSubmissionInterval = this.m_StudentSubmissionInterval;
            clonedModel.m_StudentNavigationType = this.m_StudentNavigationType;

            return clonedModel;
        }

        #endregion
    }
}
