// $Id: DeckBuilderForm.cs 1723 2008-08-28 18:54:34Z anderson $

using System;
using System.Drawing;
using System.Collections;
using System.ComponentModel;
using System.Windows.Forms;
using System.IO;
using System.Threading;
using System.Security.Cryptography;
using System.Collections.Generic;
using UW.ClassroomPresenter.Model.Presentation;
using UW.ClassroomPresenter.Decks;
using UW.ClassroomPresenter.Viewer.Slides;

namespace UW.ClassroomPresenter.DeckBuilderLibrary { 
    /// <summary>
    /// DeckBuilder GUI with command prompt support
    /// </summary>
    public class DeckBuilderForm : System.Windows.Forms.Form {

        #region Windows Forms Members

        private System.Windows.Forms.MenuItem fileMenuItem;
        private System.Windows.Forms.MenuItem editMenuItem;
        private System.Windows.Forms.MenuItem slidesMenuItem;
        private System.Windows.Forms.MenuItem viewMenuItem;
        private System.Windows.Forms.MenuItem helpMenuItem;
        private System.Windows.Forms.StatusBar statusBar;
        private System.Windows.Forms.StatusBarPanel slidenumStatusBarPanel;
        private System.Windows.Forms.StatusBarPanel titleStatusBarPanel;
        private System.Windows.Forms.StatusBarPanel sizeStatusBarPanel;
        private System.Windows.Forms.StatusBarPanel formatStatusBarPanel;
        private System.Windows.Forms.Button prevSlideButton;
        private System.Windows.Forms.Button nextSlideButton;
        private System.Windows.Forms.MainMenu mainMenu;
        private System.Windows.Forms.Panel toolbarPanel;
        private System.Windows.Forms.MenuItem openMenuItem;
        private System.Windows.Forms.MenuItem closeMenuItem;
        private System.Windows.Forms.MenuItem saveMenuItem;
        private System.Windows.Forms.MenuItem fileSeparator1MenuItem;
        private System.Windows.Forms.MenuItem fileSeparator2MenuItem;
        private System.Windows.Forms.MenuItem exitMenuItem;
        private System.Windows.Forms.MenuItem insertSlidesMenuItem;
        private System.Windows.Forms.MenuItem insertBlankSlideMenuItem;
        private System.Windows.Forms.MenuItem EditSeparator1MenuItem;
        private System.Windows.Forms.MenuItem insertImageMenuItem;
        private System.Windows.Forms.MenuItem insertImageSetMenuItem;
        private System.Windows.Forms.MenuItem editSeparator2MenuItem;
        private System.Windows.Forms.MenuItem removeSlideMenuItem;
        private System.Windows.Forms.MenuItem editSlideTitleMenuItem;
        private System.Windows.Forms.MenuItem aboutMenuItem;
        private System.Windows.Forms.MenuItem setBackgroundColorMenuItem;
        private System.Windows.Forms.MenuItem instructorModeMenuItem;
        private System.Windows.Forms.MenuItem studentModeMenuItem;
        private System.Windows.Forms.MenuItem publicModeMenuItem;
        private System.Windows.Forms.MenuItem slidesSeparatorMenuItem;

        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.Container components = null;

        #endregion

        #region Members
        
        private readonly UW.ClassroomPresenter.Model.EventQueue eventQueue;
        private bool isDirty = false;
        private DeckModel deck;
        private System.Windows.Forms.MenuItem backgroundMenuItem;
        private System.Windows.Forms.MenuItem viewSeparator1MenuItem;
        private DeckTraversalModel traversal;

        private UW.ClassroomPresenter.Viewer.Slides.MainSlideViewer slidePanel;
        private Viewer.Slides.DeckTraversalModelAdapter dtma;
        private string saveFilename = "";
        private System.Windows.Forms.MenuItem saveAsMenuItem;
        private bool insertBefore = true;
        private string pptFileName = "";
        private System.Windows.Forms.MenuItem deckBGColorMenuItem;
        private DeckMarshalService deckMarshalService = new DefaultDeckMarshalService();

        #endregion
        
        #region Constructors

        public DeckBuilderForm() {
            this.eventQueue = new UW.ClassroomPresenter.Model.ControlEventQueue(this);

            //This should be added to the Controls first
            this.slidePanel = new MainSlideViewer();
            this.slidePanel.Dock = DockStyle.Fill;
            this.slidePanel.CreateControl();
            this.Controls.Add(this.slidePanel);
            //
            // Required for Windows Form Designer support
            //
            InitializeComponent();

            //
            // TODO: Add any constructor code after InitializeComponent call
            //
            this.Closing += new CancelEventHandler(onClosing);
        }

        #endregion

        #region Event Handlers

        #region Menu Items

        #region File

        private void onOpenMenuClick(object sender, System.EventArgs e) {
            this.openFile();
        }

        private void onCloseMenuClick(object sender, System.EventArgs e) {
            this.closeDeck(false);
        }

        private void onSaveMenuClick(object sender, System.EventArgs e) {
            if (this.saveFilename == "") {
                this.saveFile();
            } else {
                this.saveFileHelper(new FileInfo(this.saveFilename));
            }
        }
        
        private void onSaveAsMenuClick(object sender, System.EventArgs e) {
            this.saveFile();
        }
 
        private void onExitMenuClick(object sender, System.EventArgs e) {
            this.Close();
        }

        #endregion

        #region Edit

        private void onInsertSlidesMenuClick(object sender, System.EventArgs e) {
            this.insertSlides();
        }

        private void onInsertBlankSlideMenuItemClick(object sender, System.EventArgs e) {
            this.insertBlankSlide();
        }

        private void onInsertImageMenuItemClick(object sender, System.EventArgs e) {
            this.insertImage();
        }

        private void onInsertImageSetMenuItemClick(object sender, System.EventArgs e) {
            this.insertImageSet();
        }

        private void onRemoveSlideMenuItemClick(object sender, System.EventArgs e) {
            this.removeSlide();
        }

        private void onEditSlideTitleMenuItemClick(object sender, System.EventArgs e) {
            this.editSlideTitle();
        }

        #endregion

        #region Slides

        private void onDeckBGColorMenuItemClick(object sender, System.EventArgs e) {
            this.changeDeckBackgroundColor();
        }

        private void onSetBackgroundColorMenuItemClick(object sender, System.EventArgs e) {
            this.changeSlideBackgroundColor();
        }

        #endregion

        #region View

        private void onInstructorModeMenuClick(object sender, System.EventArgs e) {
            this.instructorModeMenuItem.Checked = !this.instructorModeMenuItem.Checked;
            using(Synchronizer.Lock(this.slidePanel.SlideDisplay.SyncRoot)) {
                this.slidePanel.SlideDisplay.SheetDisposition ^= Model.Presentation.SheetDisposition.Instructor;
            }
        }

        private void onStudentModeMenuClick(object sender, System.EventArgs e) {
            this.studentModeMenuItem.Checked = !this.studentModeMenuItem.Checked;
            using(Synchronizer.Lock(this.slidePanel.SlideDisplay.SyncRoot)) {
                this.slidePanel.SlideDisplay.SheetDisposition ^= Model.Presentation.SheetDisposition.Student;
            }
        }

        private void onPublicModeMenuClick(object sender, System.EventArgs e) {
            this.publicModeMenuItem.Checked = !this.publicModeMenuItem.Checked;
            using(Synchronizer.Lock(this.slidePanel.SlideDisplay.SyncRoot)) {
                this.slidePanel.SlideDisplay.SheetDisposition ^= Model.Presentation.SheetDisposition.Public;
            }
        }

        private void onBackgroundMenuClick(object sender, System.EventArgs e) {
            this.backgroundMenuItem.Checked = !this.backgroundMenuItem.Checked;
            using(Synchronizer.Lock(this.slidePanel.SlideDisplay.SyncRoot)) {
                this.slidePanel.SlideDisplay.SheetDisposition ^= Model.Presentation.SheetDisposition.Background;
            }
        }

        #endregion

        #region Help

        private void onAboutMenuClick(object sender, System.EventArgs e) {
            Misc.AboutForm af = new Misc.AboutForm(Application.ProductVersion);
            af.ShowDialog();
        }

        #endregion

        #endregion

        #region Button Toolbar

        private void onNextSlideButtonClick(object sender, System.EventArgs e) {
            if (this.traversal != null) {
                using(Synchronizer.Lock(this.traversal.SyncRoot)) {
                    if (this.traversal.Next != null) {
                        this.traversal.Current = this.traversal.Next;
                    }  
                }
            }
        }

        private void onPrevSlideButtonClick(object sender, System.EventArgs e) {
            if (this.traversal != null) {
                using(Synchronizer.Lock(this.traversal.SyncRoot)) {
                    if (this.traversal.Previous != null) {
                        this.traversal.Current = this.traversal.Previous;
                    }
                }
            }
        }

        #endregion

        #region Windows Form Events

        private void onClosing(object sender, CancelEventArgs e) {
            //Check to see if we need to save
            if (this.dirtySave() == DialogResult.OK) {
                //Successful, continue closing
                e.Cancel = false;
            } else {
                //Unsuccessful, cancel closing
                e.Cancel = true;
            }
        }

        #endregion

        #region Model Events

        private void handleSlideIndexChange(object sender, Model.PropertyEventArgs args) {
            //The slide total or current slide index changed...
            if(this.InvokeRequired) {
                this.BeginInvoke(new MethodInvoker(this.handleSlideIndexChangeHelper));
            } else  {
                this.handleSlideIndexChangeHelper();
            }
        }

        private void handleSlideIndexChangeHelper() {
            //Update the numbers and the slide title too
            //Lock a ton of stuff        
            using(Synchronizer.Lock(this.traversal.SyncRoot)) {
                using(Synchronizer.Lock(this.deck.TableOfContents.SyncRoot)) {
                    using(Synchronizer.Lock(this.traversal.Current.Slide.SyncRoot)) {
                        this.statusBar.Panels[0].Text = "Slide " + this.traversal.AbsoluteCurrentSlideIndex + " / " + this.deck.TableOfContents.SlideCount;
                        this.statusBar.Panels[1].Text = this.traversal.AbsoluteCurrentSlideIndex + ". " + this.traversal.Current.Slide.Title;
                    }
                }
            }             
        }

        #endregion

        #endregion

        #region Methods

        #region Open/Save Methods

        private DialogResult saveFile() {
            //TODO: clear the dirty flag on successful save
            SaveFileDialog sfd = new SaveFileDialog();
            sfd.Filter = "Conferencing Slide Deck (*.cp3)|*.cp3";
            this.pptFileName = this.pptFileName.Replace(".ppt", "");
            sfd.FileName = this.pptFileName + ".cp3";
            if (sfd.ShowDialog() == DialogResult.OK) {
                return this.saveFileHelper(new FileInfo(sfd.FileName));
            } else {
                return DialogResult.Cancel;
            }
        }

        public DialogResult saveFileHelper(FileInfo filename) {
            if (filename.Extension == ".cp3") {
                
                PPTDeckIO.SaveAsCP3(filename, this.deck);
            } else {
                //Unrecognized file extension
                return DialogResult.Cancel;
            }
            //Clear the dirty flag
            this.isDirty = false;
            //Save the filename for future use 
            this.saveFilename = filename.FullName;
            return DialogResult.OK;
        }

        private void openFile() {
            //Check to see if there is an existing presentation open and if it is dirty, prompt to save
            if (this.dirtySave() == DialogResult.Cancel) {
                return;
            }



            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = "PPT Files (*.ppt)|*.ppt|CP3 Files (*.cp3)|*.cp3|All files (*.*)|*.*";
            if (ofd.ShowDialog() == DialogResult.OK) {
                this.OpenFileHelper(new FileInfo(ofd.FileName), true);
                this.saveFilename = "";                 // Clear the file name so as not to overwrite old file
            }
        }

        public void OpenFileHelper(FileInfo filename, bool UIEnabled) {
            //Continue opening by figuring out the extension and routing the open to the correct method
            if (filename.Extension == ".ppt" || filename.Extension == ".pptx") {  
                if (UIEnabled) {
                    Misc.ProgressBarForm pbf = new Misc.ProgressBarForm("Opening \"" + filename.Name + "\"...");
                    pbf.DoWork += new DoWorkEventHandler(this.openFileAsyncWorker);
                    pbf.Show();
                    pbf.StartWork(filename);

                    //Cache the PPT filename for reuse in the save dialogs
                    this.pptFileName = filename.Name;
                    //Set the dirty flag, since we want to ask if we want to lose the CP3
                    this.isDirty = true;
                } else {
                    this.openFileCompleteHelper(PPTDeckIO.OpenPPT(filename));
                }
            } else if (filename.Extension == ".cp3") {
                try {
                    this.openFileCompleteHelper(PPTDeckIO.OpenCP3(filename));
                } catch (System.Runtime.Serialization.SerializationException) {
                    MessageBox.Show("An error occurred while opening \"" + filename.Name + 
                        "\".  File is corrupted or is incompatible with the current version.", 
                        "DeckBuilder 3.0", MessageBoxButtons.OK, MessageBoxIcon.Exclamation, 
                        MessageBoxDefaultButton.Button1);
                } catch (Exception e) {
                    MessageBox.Show("An error occurred while opening \"" + filename.Name + 
                        "\".  " + e.ToString(), "DeckBuilder 3.0", MessageBoxButtons.OK, MessageBoxIcon.Exclamation,
                        MessageBoxDefaultButton.Button1);
                }
            } else {
                //Unrecognized file extension
                throw new Exception("Unknown File Type");
            }
            //Save the file name for future use
            if (filename.Extension != ".ppt") {
                this.saveFilename = filename.FullName;
            }
        }

        private void openFileAsyncWorker(object sender, DoWorkEventArgs progress) {
            DeckModel deck = this.deckMarshalService.ReadDeckAsync(((FileInfo)progress.Argument),
                ((BackgroundWorker)sender), progress);
            this.openFileCompleteHelper(deck);
        }

        private void openFileCompleteHelper(DeckModel deck) {
            if (deck != null) {
                this.deck = deck;

                //Finish the opening process
                this.traversal = new SlideDeckTraversalModel(Guid.NewGuid(), this.deck);
                this.dtma = new Viewer.Slides.DeckTraversalModelAdapter(this.eventQueue, this.slidePanel, this.traversal);
                //Set up event handlers and refresh slide counts
                this.blankDeckPostCheck(true);
            }
        }

        private void closeDeck(bool force) {
            if (this.deck != null) {
                //Check if it needs to be saved
                if (!force && this.dirtySave() == DialogResult.Cancel) {
                    //Cancel!
                    return;
                }
                this.deck.TableOfContents.Changed["SlideCount"].Remove(new Model.PropertyEventHandler(this.handleSlideIndexChange));
                this.traversal.Changed["AbsoluteCurrentSlideIndex"].Remove(new Model.PropertyEventHandler(this.handleSlideIndexChange));
                this.deck = null;
                this.traversal = null;
                this.dtma.Dispose();
                this.dtma = null;
                this.slidePanel.Slide = null;
                this.statusBar.Panels[0].Text = "";
                this.updateSlideMenu();
                //Clear dirty flag, since nothing is open anymore
                this.isDirty = false;
            }
        }

        #endregion

        #region Insert Methods

        private void insertSlides() {
            //If we don't have a deck/traversal open, just open a deck...
            if (this.deck == null || this.traversal == null || this.dtma == null) {
                this.openFile();
                return;
            }
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = "PPT Files (*.ppt)|*.ppt|CP3 Files (*.cp3)|*.cp3|All files (*.*)|*.*";
            if (ofd.ShowDialog() == DialogResult.OK) {
                FileInfo file = new FileInfo(ofd.FileName);
                Cursor.Current = Cursors.WaitCursor;
                DeckModel deckToInsert = this.deck;
                if (file.Extension == ".ppt") {
                    Misc.ProgressBarForm pbf = new Misc.ProgressBarForm("Opening \"" + file.Name + "\"...");
                    pbf.DoWork += new DoWorkEventHandler(this.InsertSlidesLoadAsyncWorker);
                    pbf.Show();
                    pbf.StartWork(file);
                } else if (file.Extension == ".cp3") {
                    deckToInsert = Decks.PPTDeckIO.OpenCP3(new FileInfo(ofd.FileName));
                    this.insertSlidesHelper(deckToInsert);
                } else {
                    //TODO
                    return;
                }
            }
        }

        private void InsertSlidesLoadAsyncWorker(object sender, DoWorkEventArgs progress) {
            DeckModel deck = this.deckMarshalService.ReadDeckAsync(((FileInfo)progress.Argument),
                ((BackgroundWorker)sender), progress);
            this.insertSlidesHelper(deck); 
        }

        private void insertSlidesHelper(DeckModel deckToInsert) {
            if (deckToInsert == null)
                return;

            //Now insert the deck after the current location
            //Do this in from the back forward so that we use the same index to insert
            //Start locking everything
            using (Synchronizer.Lock(this.traversal.SyncRoot)) {         
                using (Synchronizer.Lock(this.deck.SyncRoot)) {
                    using (Synchronizer.Lock(this.traversal.Current.SyncRoot)) {
                        using (Synchronizer.Lock(deckToInsert.SyncRoot)) {
                            int[] path = this.traversal.Current.PathFromRoot;
                            int insertIndex = 0;
                            if (this.insertBefore) {
                                insertIndex = 0;
                            } else {
                                insertIndex = 1;
                            }
                            if (path.Length == 0) {
                                //This is already at the root
                                insertIndex += this.deck.TableOfContents.Entries.IndexOf(this.traversal.Current);
                            } else {
                                insertIndex += path[0];
                            }
                                
                            //Iterate through each slide
                            for (int i = deckToInsert.TableOfContents.Entries.Count - 1; i >= 0; i--) {
                                Model.Presentation.TableOfContentsModel.Entry currentEntry = deckToInsert.TableOfContents.Entries[i];
                                SlideModel currentSlide = currentEntry.Slide;
                                using (Synchronizer.Lock(currentSlide.SyncRoot)) {
                                    //Copy the annotation and content sheets to the new deck
                                    foreach (SheetModel sm in currentSlide.ContentSheets) {
                                        if (sm is Model.Presentation.ImageSheetModel) {
                                            //Grab the MD5 and image from HT
                                            Image temp = deckToInsert.GetSlideContent(((ImageSheetModel)sm).MD5);
                                            this.deck.AddSlideContent(((ImageSheetModel)sm).MD5, temp);
                                        }
                                    }
                                    foreach (SheetModel sm in currentSlide.AnnotationSheets) {
                                        if (sm is Model.Presentation.ImageSheetModel) {
                                            //Grab the MD5 and image from HT
                                            Image temp = deckToInsert.GetSlideContent(((ImageSheetModel)sm).MD5);
                                            this.deck.AddSlideContent(((ImageSheetModel)sm).MD5, temp);
                                        }
                                    }
                                        
                                    //Add slide to deck
                                    this.deck.InsertSlide(currentSlide);
                                    //Create a new entry
                                    Model.Presentation.TableOfContentsModel.Entry newEntry = new UW.ClassroomPresenter.Model.Presentation.TableOfContentsModel.Entry(Guid.NewGuid(), this.deck.TableOfContents, currentSlide);
                                    //Add entry to TOC
                                    this.deck.TableOfContents.Entries.Insert(insertIndex, newEntry);
                                    //If this is the last slide we add, then this is the first slide of the set
                                    //Navigate to it
                                    if (i == 0) {
                                        this.traversal.Current = newEntry;
                                    }
                                }
                            }
                        }
                    }
                }
            }
            Cursor.Current = Cursors.Default;
            this.BeginInvoke(new MethodInvoker(this.updateSlideMenu));
            this.isDirty = true;
        }

        private void insertBlankSlide() {
            //If we have nothing open, create a new deck and traversal
            bool newDeck = this.blankDeckPreCheck();

            using(Synchronizer.Lock(this.traversal.SyncRoot)) {
                using(Synchronizer.Lock(this.deck.SyncRoot)) {
                    using(Synchronizer.Lock(this.deck.TableOfContents.SyncRoot)) {
                        //TODO: The slide bounds should not be hardcoded...
                        SlideModel newSlideModel = new SlideModel(Guid.NewGuid(), new LocalId(), SlideDisposition.Empty, new Rectangle(0, 0, 720, 540));
                        this.deck.InsertSlide(newSlideModel);
                        TableOfContentsModel.Entry newEntry = new TableOfContentsModel.Entry(Guid.NewGuid(), this.deck.TableOfContents, newSlideModel);
                        if (newDeck) {
                            this.deck.TableOfContents.Entries.Add(newEntry);
                        } else {
                            int index = this.traversal.AbsoluteCurrentSlideIndex;
                            if (this.insertBefore) {
                                index--;
                            }
                            this.deck.TableOfContents.Entries.Insert(index, newEntry);
                        }
                        //Navigate to that blank slide
                        this.traversal.Current = newEntry;
                    }
                }
            }
            this.isDirty = true;

            //Add event listeners for new decks
            this.blankDeckPostCheck(newDeck);

        }

        private void insertImage() {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = "All Image Types (*.JPEG;*.JPG;*.JPE;*.JFIF;*.BMP;*.GIF;*.TIF;*.TIFF;*.PNG)|(*.JPEG;*.JPG;*.JPE;*.JFIF;*.BMP;*.GIF;*.TIF;*.TIFF;*.PNG)|" + 
                "JPEG Files (*.JPEG;*.JPG;*.JPE;*.JFIF)|*.JPEG;*.JPG;*.JPE;*.JFIF|Bitmap Files (*.BMP)|*.BMP|GIF Files (*.GIF)|*.GIF|" + 
                "TIFF Files (*.TIF;*.TIFF)|*.TIF;*.TIFF|PNG Files (*.PNG)|*.PNG|All files (*.*)|*.*";
            if (ofd.ShowDialog() == DialogResult.OK) {
                //If we have nothing open, create a new deck and traversal
                bool newDeck = this.blankDeckPreCheck();

                //Now try to open it
                using(Synchronizer.Lock(this.traversal.SyncRoot)) {
                    int index = 0;
                    if (!newDeck) {
                        index = this.traversal.AbsoluteCurrentSlideIndex;
                        if (this.insertBefore) {
                            index--;
                        }
                    }
                    TableOfContentsModel.Entry entry = this.insertImageHelper(index, ofd.FileName);
                    if (entry != null) {
                        this.traversal.Current = entry;
                    }
                }

                //Add event listeners for new decks
                this.blankDeckPostCheck(newDeck);
            }
        }

        private void insertImageSet() {
            FolderBrowserDialog fbd = new FolderBrowserDialog();
            fbd.ShowNewFolderButton = false;
            fbd.RootFolder = System.Environment.SpecialFolder.Desktop;
            if (fbd.ShowDialog() == DialogResult.OK) {
                string[] filenames = SelectImageFiles(Directory.GetFiles(fbd.SelectedPath));
                Array.Sort(filenames);                          // Order not guaranteed - so we need to sort, Bug 941

                Misc.ProgressBarForm pbf = new Misc.ProgressBarForm("Loading...");
                pbf.DoWork += new DoWorkEventHandler(this.insertImagesWorker);
                pbf.Show();
                pbf.StartWork(filenames);
            }
        }

        /// <summary>
        /// From a list of file names, return those with extentions that correspond to image files
        /// </summary>
        /// <param name="filenames">Array of file names</param>
        /// <returns>Array of file names with image extentions</returns>
        private static string[] SelectImageFiles(string[] filenames) {
            List<string> imageFilenames = new List<string>();
            foreach (string s in filenames) {
                if (ImageExtension(s))
                    imageFilenames.Add(s);
            }
            return imageFilenames.ToArray();
        }

        /// <summary>
        /// Test if an extention corresponds to an image
        /// </summary>
        /// <param name="filename"></param>
        /// <returns></returns>
        private static bool ImageExtension(string filename) {
            string[] extentions = { ".jpg", ".jpeg", "jpe", "jfif", ".bmp", "gif", "tif", "tiff", "png" };
            foreach (string ext in extentions) {
                if (filename.ToLower().EndsWith(ext))
                    return true;
            }
            return false;
        }

        private void insertImagesWorker(object sender, DoWorkEventArgs progress) {
            BackgroundWorker async = (BackgroundWorker)sender;
            string[] filenames = (string[])progress.Argument;

            //If we have nothing open, create a new deck and traversal
            bool newDeck = this.blankDeckPreCheck();

            using (Synchronizer.Lock(this.traversal.SyncRoot)) {
                int index = 0;
                if (!newDeck) {
                    index = this.traversal.AbsoluteCurrentSlideIndex;
                    if (this.insertBefore) {
                        index--;
                    }
                }
                async.ReportProgress(0, "Initializing...");
                for (int i = filenames.Length - 1; i >= 0; i--) {
                    try {
                        TableOfContentsModel.Entry entry = this.insertImageHelper(index, filenames[i]);
                        //If this is the first entry, navigate to it
                        if (i == 0 && entry != null) {
                            this.traversal.Current = entry;
                        }
                    } catch (System.ArgumentException) {
                        //Picked up a bad file, skip it
                    } finally {
                        async.ReportProgress((i * 100) / filenames.Length, "Reading Images...");
                    }
                }
                async.ReportProgress(100, "Done!");
            }

            //Add event listeners for new decks
            this.blankDeckPostCheck(newDeck);
        }

        private TableOfContentsModel.Entry insertImageHelper(int index, string filename) {
            //Assume that traversal is already locked!           
            using(Synchronizer.Lock(this.deck.SyncRoot)) {  
                using(Synchronizer.Lock(this.deck.TableOfContents.SyncRoot)) {
                    //Create a new Background layer
                    //Compute the MD5 of the BG
                    FileStream fs = new FileStream(filename, FileMode.Open, FileAccess.Read);
                    MD5 md5Provider = new MD5CryptoServiceProvider();
                    byte[] md5 = md5Provider.ComputeHash(fs);
   //                 fs.Seek(0, SeekOrigin.Begin);
   //                 Image imageFile = Image.FromStream(fs);
   //                 fs.Close();
                    fs.Close();
                    Image imageFile;
                    try {
                        imageFile = Image.FromFile(filename);
                    }
                    catch (Exception e) {
                        MessageBox.Show("Error reading in file:\r\n" + filename + "\r\n" + e.Message);
                        imageFile = new Bitmap(10, 10);

                    }


                    //Create a new SlideModel
                    SlideModel newSlideModel = new SlideModel(Guid.NewGuid(), new LocalId(), SlideDisposition.Empty, new Rectangle(new Point(0, 0), imageFile.Size));
                    using(Synchronizer.Lock(newSlideModel.SyncRoot)) {
                        //Set the slide's title
                        newSlideModel.Title = filename;

                        //Create the ImageSheet
                        ImageSheetModel sheet = new ImageSheetModel(deck, Guid.NewGuid(), Model.Presentation.SheetDisposition.Background,
                            new Rectangle(new Point(0, 0), imageFile.Size), (ByteArray)md5, 1);
                        //Add the ImageSheet to the Slide
                        newSlideModel.ContentSheets.Add(sheet);
                        TableOfContentsModel.Entry newEntry;
                    
                        //Add the Image+MD5 to the deck
                        this.deck.AddSlideContent((ByteArray)md5, imageFile);
                        //Add SlideModel to the deck
                        this.deck.InsertSlide(newSlideModel);
                        //Create a new Entry + reference SlideModel
                        newEntry = new TableOfContentsModel.Entry(Guid.NewGuid(), deck.TableOfContents, newSlideModel);
                        //Add Entry to TOC
                        this.deck.TableOfContents.Entries.Insert(index, newEntry);
                        this.isDirty = true;

                        //Return the entry so we can navigate to it, if we wish
                        return newEntry;
                    }
                }
            }
        }

        private bool blankDeckPreCheck() {
            bool newDeck = false;
            if (this.deck == null) {
                this.deck = new DeckModel(Guid.NewGuid(), DeckDisposition.Empty);
                newDeck = true;
            }
            
            if (this.traversal == null) {
                this.traversal = new SlideDeckTraversalModel(Guid.NewGuid(), this.deck);
                newDeck = true;
            }
            if (this.dtma == null) {
                this.dtma = new Viewer.Slides.DeckTraversalModelAdapter(this.eventQueue, this.slidePanel, this.traversal);
                newDeck = true;
            }
            return newDeck;
        }

        private void blankDeckPostCheck(bool newDeck) {
            if (newDeck) {
                this.deck.TableOfContents.Changed["SlideCount"].Add(new Model.PropertyEventHandler(this.handleSlideIndexChange));
                this.traversal.Changed["AbsoluteCurrentSlideIndex"].Add(new Model.PropertyEventHandler(this.handleSlideIndexChange));
                this.handleSlideIndexChange(this, null);
            }
            this.BeginInvoke(new MethodInvoker(this.updateSlideMenu));
        }

        #endregion

        #region Remove Methods

        private void removeSlide() {
            //We can remove what's not there...
            if (this.deck != null && this.traversal != null && this.dtma != null) {
                using(Synchronizer.Lock(this.traversal.SyncRoot)) {
                    TableOfContentsModel.Entry navigateHere;
                    using(Synchronizer.Lock(this.deck.SyncRoot)) {
                        using(Synchronizer.Lock(this.deck.TableOfContents.SyncRoot)) {
                            TableOfContentsModel.Entry entry = this.traversal.Current;
                            
                            if (this.traversal.Next != null) {
                                navigateHere = this.traversal.Next;
                            } else if (this.traversal.Previous != null) {
                                navigateHere = this.traversal.Previous;
                            } else {
                                //No more slides!
                                navigateHere = null;
                            }
                            SlideModel toRemove = entry.Slide;
                            //FIXME: I don't think this searches recursively for the entry to remove...
                            this.deck.TableOfContents.Entries.Remove(entry);
                            this.deck.DeleteSlide(toRemove);
                            //Set the dirty flag
                            this.isDirty = true;
                        }
                    }
                    if (navigateHere != null) {
                        this.traversal.Current = navigateHere;
                    } else {
                        //Just close the deck...
                        this.closeDeck(true);
                    }
                }
                //Update the slide menu
                this.updateSlideMenu();
            }
        }

        #endregion

        #region Slide Modification Methods

        private void changeDeckBackgroundColor() {
            ColorDialog cd = new ColorDialog();
            cd.AllowFullOpen = true;
            cd.AnyColor = true;
            cd.SolidColorOnly = false;
            if (cd.ShowDialog() == DialogResult.OK) {
                using (Synchronizer.Lock(this.deck.SyncRoot)) {
                    this.deck.DeckBackgroundColor = cd.Color;
                }
                this.isDirty = true;
            }
        }

        private void changeSlideBackgroundColor() {
            ColorDialog cd = new ColorDialog();
            cd.AllowFullOpen = true;
            cd.AnyColor = true;
            cd.SolidColorOnly = false;
            if (cd.ShowDialog() == DialogResult.OK) {
                using (Synchronizer.Lock(this.traversal.SyncRoot)) {
                    using (Synchronizer.Lock(this.traversal.Current.Slide.SyncRoot)) {
                        this.traversal.Current.Slide.BackgroundColor = cd.Color;
                    }
                }
                this.isDirty = true;
            }
        }
        
        private void editSlideTitle() {
            if (this.traversal == null)                 // Guard against the case of no slide deck
                return;

            //Get the current slide title
            string currentTitle = "";
            using (Synchronizer.Lock(this.traversal.SyncRoot)) {
                using (Synchronizer.Lock(this.traversal.Current.SyncRoot)) {
                    using (Synchronizer.Lock(this.traversal.Current.Slide.SyncRoot)) {
                        currentTitle = this.traversal.Current.Slide.Title;
                    }
                }
            }
            //Create the message box and display it
            Misc.TextInputMessageBox tibm = new Misc.TextInputMessageBox("New Slide Title", "DeckBuilder 3.0", currentTitle);
            tibm.ShowDialog();
            //Update if necessary
            if (tibm.Result == DialogResult.OK) {
                if (tibm.InputText != currentTitle) {
                    using (Synchronizer.Lock(this.traversal.SyncRoot)) {
                        using (Synchronizer.Lock(this.traversal.Current.SyncRoot)) {
                            using (Synchronizer.Lock(this.traversal.Current.Slide.SyncRoot)) {
                                this.traversal.Current.Slide.Title = tibm.InputText;
                            }
                        }
                    }
                    this.isDirty = true;
                    this.updateSlideMenu();
                    this.handleSlideIndexChangeHelper();
                }
            }
        }

        #endregion

        #region Update Methods

        private void updateSlideMenu() {
            //Clear the Slides menu
            this.slidesMenuItem.MenuItems.Clear();
            //Add the deck default background and splitter item back
            this.slidesMenuItem.MenuItems.AddRange(new System.Windows.Forms.MenuItem[] {
                                                                                           this.deckBGColorMenuItem,
                                                                                           this.setBackgroundColorMenuItem,
                                                                                           this.slidesSeparatorMenuItem});
            //Sanity check
            if (this.traversal != null && this.deck != null && this.deck.TableOfContents != null) {

                using (Synchronizer.Lock(this.traversal.SyncRoot)) {
                    using (Synchronizer.Lock(this.deck.SyncRoot)) {
                        using (Synchronizer.Lock(this.deck.TableOfContents.SyncRoot)) {
                            //Iterate over the deck and construct menu items (Flat view)
                            //Get the first entry
                            TableOfContentsModel.Entry currentEntry = this.deck.TableOfContents.Entries[0];
                            int slideCount = 1;
                            while (currentEntry != null) {
                                //Retrieve the associated slide
                                if (currentEntry.Slide != null) {
                                    this.slidesMenuItem.MenuItems.Add(new SlideMenuItem(currentEntry, this.traversal, slideCount));
                                }
                                //Done, increment currentEntry and slideCount
                                currentEntry = ((DefaultDeckTraversalModel)this.traversal).FindNext(currentEntry);
                                slideCount++;
                            }
                        }
                    }
                }
            }
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Helper function that handles dirty saving.
        /// </summary>
        /// <returns>DialogResult which signifies whether the caller should proceed or not based on the result of saving.</returns>
        private DialogResult dirtySave() {
            if (this.isDirty) {
                DialogResult dr = System.Windows.Forms.MessageBox.Show("Do you want to save changes to the current deck?", "DeckBuilder 3.0", 
                    MessageBoxButtons.YesNoCancel, MessageBoxIcon.Exclamation, MessageBoxDefaultButton.Button1);
                if (dr == DialogResult.Yes) {
                    //Save
                    try {
                        return this.saveFile();
                    } catch (SaveFileException) {
                        //File save unsuccessful = exit
                        return DialogResult.Cancel;
                    }
                } else if (dr == DialogResult.No) {
                    //Just continue
                    return DialogResult.OK;
                } else {
                    //Cancel close
                    return DialogResult.Cancel;
                }
            }
            //No save required
            return DialogResult.OK;
        }

        #endregion

        #endregion 

        #region Windows Form Designer generated code
        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        protected override void Dispose( bool disposing ) {
            if( disposing ) {
                if(components != null) {
                    components.Dispose();
                }
            }
            base.Dispose( disposing );
        }
        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent() {
            System.Resources.ResourceManager resources = new System.Resources.ResourceManager(typeof(DeckBuilderForm));
            this.mainMenu = new System.Windows.Forms.MainMenu();
            this.fileMenuItem = new System.Windows.Forms.MenuItem();
            this.openMenuItem = new System.Windows.Forms.MenuItem();
            this.closeMenuItem = new System.Windows.Forms.MenuItem();
            this.fileSeparator1MenuItem = new System.Windows.Forms.MenuItem();
            this.saveMenuItem = new System.Windows.Forms.MenuItem();
            this.saveAsMenuItem = new System.Windows.Forms.MenuItem();
            this.fileSeparator2MenuItem = new System.Windows.Forms.MenuItem();
            this.exitMenuItem = new System.Windows.Forms.MenuItem();
            this.editMenuItem = new System.Windows.Forms.MenuItem();
            this.insertSlidesMenuItem = new System.Windows.Forms.MenuItem();
            this.insertBlankSlideMenuItem = new System.Windows.Forms.MenuItem();
            this.EditSeparator1MenuItem = new System.Windows.Forms.MenuItem();
            this.insertImageMenuItem = new System.Windows.Forms.MenuItem();
            this.insertImageSetMenuItem = new System.Windows.Forms.MenuItem();
            this.editSeparator2MenuItem = new System.Windows.Forms.MenuItem();
            this.removeSlideMenuItem = new System.Windows.Forms.MenuItem();
            this.editSlideTitleMenuItem = new System.Windows.Forms.MenuItem();
            this.slidesMenuItem = new System.Windows.Forms.MenuItem();
            this.deckBGColorMenuItem = new System.Windows.Forms.MenuItem();
            this.setBackgroundColorMenuItem = new System.Windows.Forms.MenuItem();
            this.slidesSeparatorMenuItem = new System.Windows.Forms.MenuItem();
            this.viewMenuItem = new System.Windows.Forms.MenuItem();
            this.instructorModeMenuItem = new System.Windows.Forms.MenuItem();
            this.studentModeMenuItem = new System.Windows.Forms.MenuItem();
            this.publicModeMenuItem = new System.Windows.Forms.MenuItem();
            this.viewSeparator1MenuItem = new System.Windows.Forms.MenuItem();
            this.backgroundMenuItem = new System.Windows.Forms.MenuItem();
            this.helpMenuItem = new System.Windows.Forms.MenuItem();
            this.aboutMenuItem = new System.Windows.Forms.MenuItem();
            this.statusBar = new System.Windows.Forms.StatusBar();
            this.slidenumStatusBarPanel = new System.Windows.Forms.StatusBarPanel();
            this.titleStatusBarPanel = new System.Windows.Forms.StatusBarPanel();
            this.sizeStatusBarPanel = new System.Windows.Forms.StatusBarPanel();
            this.formatStatusBarPanel = new System.Windows.Forms.StatusBarPanel();
            this.prevSlideButton = new System.Windows.Forms.Button();
            this.nextSlideButton = new System.Windows.Forms.Button();
            this.toolbarPanel = new System.Windows.Forms.Panel();
            ((System.ComponentModel.ISupportInitialize)(this.slidenumStatusBarPanel)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.titleStatusBarPanel)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.sizeStatusBarPanel)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.formatStatusBarPanel)).BeginInit();
            this.toolbarPanel.SuspendLayout();
            this.SuspendLayout();
            // 
            // mainMenu
            // 
            this.mainMenu.MenuItems.AddRange(new System.Windows.Forms.MenuItem[] {
                                                                                     this.fileMenuItem,
                                                                                     this.editMenuItem,
                                                                                     this.slidesMenuItem,
                                                                                     this.viewMenuItem,
                                                                                     this.helpMenuItem});
            // 
            // fileMenuItem
            // 
            this.fileMenuItem.Index = 0;
            this.fileMenuItem.MenuItems.AddRange(new System.Windows.Forms.MenuItem[] {
                                                                                         this.openMenuItem,
                                                                                         this.closeMenuItem,
                                                                                         this.fileSeparator1MenuItem,
                                                                                         this.saveMenuItem,
                                                                                         this.saveAsMenuItem,
                                                                                         this.fileSeparator2MenuItem,
                                                                                         this.exitMenuItem});
            this.fileMenuItem.Text = "&File";
            // 
            // openMenuItem
            // 
            this.openMenuItem.Index = 0;
            this.openMenuItem.Text = "&Open...";
            this.openMenuItem.Click += new System.EventHandler(this.onOpenMenuClick);
            // 
            // closeMenuItem
            // 
            this.closeMenuItem.Index = 1;
            this.closeMenuItem.Text = "&Close";
            this.closeMenuItem.Click += new System.EventHandler(this.onCloseMenuClick);
            // 
            // fileSeparator1MenuItem
            // 
            this.fileSeparator1MenuItem.Index = 2;
            this.fileSeparator1MenuItem.Text = "-";
            // 
            // saveMenuItem
            // 
            this.saveMenuItem.Index = 3;
            this.saveMenuItem.Text = "&Save";
            this.saveMenuItem.Click += new System.EventHandler(this.onSaveMenuClick);
            // 
            // saveAsMenuItem
            // 
            this.saveAsMenuItem.Index = 4;
            this.saveAsMenuItem.Text = "Save &As...";
            this.saveAsMenuItem.Click += new System.EventHandler(this.onSaveAsMenuClick);
            // 
            // fileSeparator2MenuItem
            // 
            this.fileSeparator2MenuItem.Index = 5;
            this.fileSeparator2MenuItem.Text = "-";
            // 
            // exitMenuItem
            // 
            this.exitMenuItem.Index = 6;
            this.exitMenuItem.Text = "E&xit";
            this.exitMenuItem.Click += new System.EventHandler(this.onExitMenuClick);
            // 
            // editMenuItem
            // 
            this.editMenuItem.Index = 1;
            this.editMenuItem.MenuItems.AddRange(new System.Windows.Forms.MenuItem[] {
                                                                                         this.insertSlidesMenuItem,
                                                                                         this.insertBlankSlideMenuItem,
                                                                                         this.EditSeparator1MenuItem,
                                                                                         this.insertImageMenuItem,
                                                                                         this.insertImageSetMenuItem,
                                                                                         this.editSeparator2MenuItem,
                                                                                         this.removeSlideMenuItem,
                                                                                         this.editSlideTitleMenuItem});
            this.editMenuItem.Text = "&Edit";
            // 
            // insertSlidesMenuItem
            // 
            this.insertSlidesMenuItem.Index = 0;
            this.insertSlidesMenuItem.Text = "&Insert Slides...";
            this.insertSlidesMenuItem.Click += new System.EventHandler(this.onInsertSlidesMenuClick);
            // 
            // insertBlankSlideMenuItem
            // 
            this.insertBlankSlideMenuItem.Index = 1;
            this.insertBlankSlideMenuItem.Text = "Insert &Blank Slide";
            this.insertBlankSlideMenuItem.Click += new System.EventHandler(this.onInsertBlankSlideMenuItemClick);
            // 
            // EditSeparator1MenuItem
            // 
            this.EditSeparator1MenuItem.Index = 2;
            this.EditSeparator1MenuItem.Text = "-";
            // 
            // insertImageMenuItem
            // 
            this.insertImageMenuItem.Index = 3;
            this.insertImageMenuItem.Text = "Insert I&mage...";
            this.insertImageMenuItem.Click += new System.EventHandler(this.onInsertImageMenuItemClick);
            // 
            // insertImageSetMenuItem
            // 
            this.insertImageSetMenuItem.Index = 4;
            this.insertImageSetMenuItem.Text = "Insert Image &Set...";
            this.insertImageSetMenuItem.Click += new System.EventHandler(this.onInsertImageSetMenuItemClick);
            // 
            // editSeparator2MenuItem
            // 
            this.editSeparator2MenuItem.Index = 5;
            this.editSeparator2MenuItem.Text = "-";
            // 
            // removeSlideMenuItem
            // 
            this.removeSlideMenuItem.Index = 6;
            this.removeSlideMenuItem.Text = "&Remove Slide";
            this.removeSlideMenuItem.Click += new System.EventHandler(this.onRemoveSlideMenuItemClick);
            // 
            // editSlideTitleMenuItem
            // 
            this.editSlideTitleMenuItem.Index = 7;
            this.editSlideTitleMenuItem.Text = "&Edit Slide Title...";
            this.editSlideTitleMenuItem.Click += new System.EventHandler(this.onEditSlideTitleMenuItemClick);
            // 
            // slidesMenuItem
            // 
            this.slidesMenuItem.Index = 2;
            this.slidesMenuItem.MenuItems.AddRange(new System.Windows.Forms.MenuItem[] {
                                                                                           this.deckBGColorMenuItem,
                                                                                           this.setBackgroundColorMenuItem,
                                                                                           this.slidesSeparatorMenuItem});
            this.slidesMenuItem.Text = "&Slides";
            // 
            // deckBGColorMenuItem
            // 
            this.deckBGColorMenuItem.Index = 0;
            this.deckBGColorMenuItem.Text = "Set &Deck Background Color...";
            this.deckBGColorMenuItem.Click += new System.EventHandler(this.onDeckBGColorMenuItemClick);
            // 
            // setBackgroundColorMenuItem
            // 
            this.setBackgroundColorMenuItem.Index = 1;
            this.setBackgroundColorMenuItem.Text = "Set Slide &Background Color...";
            this.setBackgroundColorMenuItem.Click += new System.EventHandler(this.onSetBackgroundColorMenuItemClick);
            // 
            // slidesSeparatorMenuItem
            // 
            this.slidesSeparatorMenuItem.Index = 2;
            this.slidesSeparatorMenuItem.Text = "-";
            // 
            // viewMenuItem
            // 
            this.viewMenuItem.Index = 3;
            this.viewMenuItem.MenuItems.AddRange(new System.Windows.Forms.MenuItem[] {
                                                                                         this.instructorModeMenuItem,
                                                                                         this.studentModeMenuItem,
                                                                                         this.publicModeMenuItem,
                                                                                         this.viewSeparator1MenuItem,
                                                                                         this.backgroundMenuItem});
            this.viewMenuItem.Text = "&View";
            // 
            // instructorModeMenuItem
            // 
            this.instructorModeMenuItem.Index = 0;
            this.instructorModeMenuItem.Text = "&Instructor Mode";
            this.instructorModeMenuItem.Click += new System.EventHandler(this.onInstructorModeMenuClick);
            // 
            // studentModeMenuItem
            // 
            this.studentModeMenuItem.Index = 1;
            this.studentModeMenuItem.Text = "&Student Mode";
            this.studentModeMenuItem.Click += new System.EventHandler(this.onStudentModeMenuClick);
            // 
            // publicModeMenuItem
            // 
            this.publicModeMenuItem.Index = 2;
            this.publicModeMenuItem.Text = "&Public Mode";
            this.publicModeMenuItem.Click += new System.EventHandler(this.onPublicModeMenuClick);
            // 
            // viewSeparator1MenuItem
            // 
            this.viewSeparator1MenuItem.Index = 3;
            this.viewSeparator1MenuItem.Text = "-";
            // 
            // backgroundMenuItem
            // 
            this.backgroundMenuItem.Checked = true;
            this.backgroundMenuItem.Index = 4;
            this.backgroundMenuItem.Text = "&Background";
            this.backgroundMenuItem.Click += new System.EventHandler(this.onBackgroundMenuClick);
            // 
            // helpMenuItem
            // 
            this.helpMenuItem.Index = 4;
            this.helpMenuItem.MenuItems.AddRange(new System.Windows.Forms.MenuItem[] {
                                                                                         this.aboutMenuItem});
            this.helpMenuItem.Text = "&Help";
            // 
            // aboutMenuItem
            // 
            this.aboutMenuItem.Index = 0;
            this.aboutMenuItem.Text = "&About DeckBuilder...";
            this.aboutMenuItem.Click += new System.EventHandler(this.onAboutMenuClick);
            // 
            // statusBar
            // 
            this.statusBar.Location = new System.Drawing.Point(0, 639);
            this.statusBar.Name = "statusBar";
            this.statusBar.Panels.AddRange(new System.Windows.Forms.StatusBarPanel[] {
                                                                                         this.slidenumStatusBarPanel,
                                                                                         this.titleStatusBarPanel,
                                                                                         this.sizeStatusBarPanel,
                                                                                         this.formatStatusBarPanel});
            this.statusBar.ShowPanels = true;
            this.statusBar.Size = new System.Drawing.Size(800, 22);
            this.statusBar.TabIndex = 0;
            // 
            // titleStatusBarPanel
            // 
            this.titleStatusBarPanel.Width = 300;
            // 
            // prevSlideButton
            // 
            this.prevSlideButton.Image = ((System.Drawing.Image)(resources.GetObject("prevSlideButton.Image")));
            this.prevSlideButton.Location = new System.Drawing.Point(0, 0);
            this.prevSlideButton.Name = "prevSlideButton";
            this.prevSlideButton.Size = new System.Drawing.Size(40, 40);
            this.prevSlideButton.TabIndex = 2;
            this.prevSlideButton.Click += new System.EventHandler(this.onPrevSlideButtonClick);
            // 
            // nextSlideButton
            // 
            this.nextSlideButton.Image = ((System.Drawing.Image)(resources.GetObject("nextSlideButton.Image")));
            this.nextSlideButton.Location = new System.Drawing.Point(40, 0);
            this.nextSlideButton.Name = "nextSlideButton";
            this.nextSlideButton.Size = new System.Drawing.Size(40, 40);
            this.nextSlideButton.TabIndex = 3;
            this.nextSlideButton.Click += new System.EventHandler(this.onNextSlideButtonClick);
            // 
            // toolbarPanel
            // 
            this.toolbarPanel.Controls.Add(this.prevSlideButton);
            this.toolbarPanel.Controls.Add(this.nextSlideButton);
            this.toolbarPanel.Dock = System.Windows.Forms.DockStyle.Top;
            this.toolbarPanel.Location = new System.Drawing.Point(0, 0);
            this.toolbarPanel.Name = "toolbarPanel";
            this.toolbarPanel.Size = new System.Drawing.Size(800, 40);
            this.toolbarPanel.TabIndex = 5;
            // 
            // DeckBuilderForm
            // 
            this.AutoScaleBaseSize = new System.Drawing.Size(5, 13);
            this.ClientSize = new System.Drawing.Size(800, 661);
            this.Controls.Add(this.toolbarPanel);
            this.Controls.Add(this.statusBar);
            this.Menu = this.mainMenu;
            this.Name = "DeckBuilderForm";
            this.Text = "Deck Builder" + " (Version " + Application.ProductVersion + ") - ";
            ((System.ComponentModel.ISupportInitialize)(this.slidenumStatusBarPanel)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.titleStatusBarPanel)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.sizeStatusBarPanel)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.formatStatusBarPanel)).EndInit();
            this.toolbarPanel.ResumeLayout(false);
            this.ResumeLayout(false);

        }
        #endregion
    }

    public class SaveFileException : Exception {}

    public class SlideMenuItem : MenuItem {
        public readonly TableOfContentsModel.Entry Entry;
        public readonly DeckTraversalModel Traversal;

        public SlideMenuItem(TableOfContentsModel.Entry entry, DeckTraversalModel traversal, int slideCount) {
            this.Entry = entry;
            this.Traversal = traversal;
            using (Synchronizer.Lock(this.Entry.Slide.SyncRoot)) {
                //Set a 50 char limit on the title
                string toDisplay = this.Entry.Slide.Title;
                if (toDisplay.Length > 50) {
                    toDisplay = toDisplay.Substring(0, 50) + "...";
                }
                this.Text = "" + slideCount + ". " + toDisplay;
            }
            using (Synchronizer.Lock(this.Traversal)) {
                this.Traversal.Changed["Current"].Add(new Model.PropertyEventHandler(this.OnUpdate));
            }
            this.OnUpdate(this, null);
        }

        protected override void Dispose(bool disposing) {
            if (disposing) {
                this.Traversal.Changed["Current"].Remove(new Model.PropertyEventHandler(this.OnUpdate));
            }
            base.Dispose (disposing);
        }

        protected override void OnClick(EventArgs e) {
            //Sanity check
            if (this.Traversal != null) {
                using (Synchronizer.Lock(this.Traversal.SyncRoot)) {
                    this.Traversal.Current = this.Entry;
                }
            }
            base.OnClick (e);
        }

        private void OnUpdate(object sender, Model.PropertyEventArgs args) {
            using (Synchronizer.Lock(this.Traversal)) {
                if (this.Traversal.Current == this.Entry) {
                    this.Checked = true;
                } else {
                    this.Checked = false;
                }
            }
        }
    }

}
