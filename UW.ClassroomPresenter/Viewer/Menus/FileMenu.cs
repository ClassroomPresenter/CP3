// $Id: FileMenu.cs 2233 2013-09-27 22:17:28Z fred $

using System;
using System.Windows.Forms;
using System.IO;

using UW.ClassroomPresenter.Decks;
using UW.ClassroomPresenter.Model;
using UW.ClassroomPresenter.Model.Presentation;
using System.Collections.Generic;
using UW.ClassroomPresenter.Viewer.PropertiesForm;
using System.Drawing.Printing;
using UW.ClassroomPresenter.Model.Network;

namespace UW.ClassroomPresenter.Viewer.Menus {
    
    public class FileMenu : MenuItem {
        private PresenterModel presenter_model_;
        OpenDeckMenuItem open_deck_;
        public FileMenu(ControlEventQueue dispatcher, PresenterModel model, DeckMarshalService marshal, CloseFormDelegate cfd) {
            this.Text = Strings.File;

            open_deck_ = new OpenDeckMenuItem(model, marshal);
            this.MenuItems.Add(open_deck_);
            this.MenuItems.Add(new CloseDeckMenuItem(dispatcher, model, marshal));
            this.MenuItems.Add(new MenuItem("-"));// Text of "-" signifies a separator bar.
            this.MenuItems.Add(new SaveDeckMenuItem(model, marshal));
            this.MenuItems.Add(new SaveDeckAsMenuItem(model, marshal));
            this.MenuItems.Add(new SaveAllDecksMenuItem(model, marshal));
            this.MenuItems.Add(new SaveAllDecksAsMenuItem(model, marshal));
            this.MenuItems.Add(new MenuItem("-")); // Text of "-" signifies a separator bar.
            this.MenuItems.Add(new ExportDeckAsImageItem(model));
            this.MenuItems.Add(new ExportDeckAsHTMLItem(model));
            this.MenuItems.Add(new ExportInkMenuItem(model));
            this.MenuItems.Add(new MenuItem("-"));
            this.MenuItems.Add(new PageSetupMenuItem(this, model));
            this.MenuItems.Add(new PrintPreviewMenuItem(this, model));
            this.MenuItems.Add(new PrintMenuItem(this, model));
            this.MenuItems.Add(new MenuItem( "-" ));
            this.MenuItems.Add(new ExitMenuItem(cfd));

            presenter_model_ = model;
            presenter_model_.Workspace.CurrentPresentation.ListenAndInitialize(dispatcher, new Property<PresentationModel>.EventHandler(this.HandlePresentationChanged));
            presenter_model_.Workspace.CurrentDeckTraversal.ListenAndInitialize(dispatcher, new Property<DeckTraversalModel>.EventHandler(this.HandleDeckChanged));
        }

        /// <summary>
        /// This method enables or disables menu items based on whether we have
        /// a presentation or not.
        /// </summary>
        /// <param name="args"></param>
        private void HandlePresentationChanged(Property<PresentationModel>.EventArgs args) {
            using (Synchronizer.Lock(presenter_model_.Workspace.CurrentPresentation.SyncRoot)) {
                if (~presenter_model_.Workspace.CurrentPresentation == null) {
                    // we don't have a current presentation
                    open_deck_.Enabled = false;
                } else {
                    open_deck_.Enabled = true;
                }
            }
        }
        /// <summary>
        /// This method enables or disables menu items based on whether
        /// we have a deck or not.
        /// if we have a deck, then we should enable all deck related items.
        /// otherwise, they should be disabled.
        /// </summary>
        /// <param name="args"></param>
        private void HandleDeckChanged(Property<DeckTraversalModel>.EventArgs args) {
            using (Synchronizer.Lock(presenter_model_.Workspace.CurrentDeckTraversal.SyncRoot)) {
                if (~presenter_model_.Workspace.CurrentDeckTraversal == null) {
                    //we do not have a deck, so all deck related items should be disabled.
                    foreach (MenuItem item in this.MenuItems) {
                        if (!(item is ExitMenuItem || item is OpenDeckMenuItem)) {
                            item.Enabled = false;
                        }
                    }
                } else {
                    // we have a deck
                    foreach (MenuItem item in this.MenuItems) {
                        if (!(item is ExitMenuItem && item is OpenDeckMenuItem)) {
                            item.Enabled = true;
                        }
                    }
                }
            }
        }

        #region PrintMenuItem

        public PageSettings PageSettings = new PageSettings();
        public PrinterSettings PrinterSettings = new PrinterSettings();

        public class PageSetupMenuItem : MenuItem {
            private readonly PresenterModel model;
            private readonly FileMenu m_Parent;

            public PageSetupMenuItem( FileMenu parent, PresenterModel m ) {
                this.model = m;
                this.Text = Strings.PageSetup;
                this.m_Parent = parent;

                // Set default values
                this.m_Parent.PageSettings.Margins = new Margins( 50, 50, 50, 50 );
            }

            protected override void OnClick( EventArgs e ) {
                base.OnClick( e );

                PrintDocument doc;
                using( Synchronizer.Lock( this.model.ViewerState.SyncRoot ) )
                    doc = this.model.ViewerState.Document;

                PageSetupDialog pageSetupDialog = new PageSetupDialog();
                pageSetupDialog.PageSettings = this.m_Parent.PageSettings;
                pageSetupDialog.PrinterSettings = this.m_Parent.PrinterSettings;
                pageSetupDialog.AllowOrientation = true;
                pageSetupDialog.AllowMargins = true;
                pageSetupDialog.ShowDialog();
            }
        }

        public class PrintPreviewMenuItem : MenuItem {
            private readonly PresenterModel model;
            private readonly FileMenu m_Parent;

            public PrintPreviewMenuItem( FileMenu parent, PresenterModel m ) {
                this.model = m;
                this.m_Parent = parent;
                this.Text = Strings.PrintPreview;
            }

            protected override void OnClick( EventArgs e ) {
                base.OnClick( e );

                PrintDocument doc;
                using( Synchronizer.Lock( this.model.ViewerState.SyncRoot ) )
                    doc = this.model.ViewerState.Document;
                PrintPreviewDialog dialog = new PrintPreviewDialog();
                dialog.Document = doc;

                doc.DefaultPageSettings = this.m_Parent.PageSettings;
                using( this.model.Workspace.Lock() )
                    using( Synchronizer.Lock( this.model.ViewerState.SyncRoot ) )
                        this.model.ViewerState.PrintableDeck = this.model.Workspace.CurrentDeckTraversal;
                dialog.ShowDialog();
            }
        }

        public class PrintMenuItem : MenuItem {
            private readonly PresenterModel model;
            private readonly FileMenu m_Parent;

            public PrintMenuItem( FileMenu parent, PresenterModel m ) {
                this.model = m;
                this.m_Parent = parent;
                this.Text = Strings.Print;
                this.Shortcut = Shortcut.CtrlP;
            }

            protected override void OnClick( EventArgs e ) {
                base.OnClick( e );

                PrintDocument doc;
                using( Synchronizer.Lock( this.model.ViewerState.SyncRoot ) )
                    doc = this.model.ViewerState.Document;
                PrintDialog dialog = new PrintDialog();
                dialog.AllowPrintToFile = true;
                dialog.AllowSelection = false;
                dialog.AllowCurrentPage = false;
                dialog.AllowSomePages = true;
                dialog.UseEXDialog = true;
                dialog.Document = doc;
                // Set the page settings
                doc.DefaultPageSettings = this.m_Parent.PageSettings;
                dialog.PrinterSettings.MinimumPage = 0;
                dialog.PrinterSettings.MaximumPage = 9999;

                if( dialog.ShowDialog() == DialogResult.OK ) {
                    // Set the deck
                    using( this.model.Workspace.Lock() ) 
                        using( Synchronizer.Lock( this.model.ViewerState.SyncRoot ) )
                            this.model.ViewerState.PrintableDeck = this.model.Workspace.CurrentDeckTraversal;

                    // Get the maximum and minimum page
                    using( Synchronizer.Lock( this.model.ViewerState.SyncRoot ) ) {
                        doc.PrinterSettings.MinimumPage = 1;
                        using( Synchronizer.Lock( this.model.ViewerState.PrintableDeck.SyncRoot ) )
                            using( Synchronizer.Lock( this.model.ViewerState.PrintableDeck.Deck.SyncRoot ) )
                                doc.PrinterSettings.MaximumPage = this.model.ViewerState.PrintableDeck.Deck.Slides.Count;
                    }

                    doc.Print();
                }
            }
        }

        #endregion

        #region ExitMenuItem

        public class ExitMenuItem : MenuItem {
            private readonly CloseFormDelegate m_CFD;

            public ExitMenuItem(CloseFormDelegate cfd) {
                this.Text = Strings.Exit;
                this.m_CFD = cfd;
            }

            protected override void OnClick(EventArgs e) {
                //TODO: Need to implement dirty save prompting
                base.OnClick (e);
                this.m_CFD();
            }
        }

        public delegate void CloseFormDelegate();

        #endregion

        #region ExportDeckAsImageItem

        public class ExportDeckAsImageItem : MenuItem {
            private readonly PresenterModel m_Model;

            public ExportDeckAsImageItem(PresenterModel m) {
                this.m_Model = m;
                this.Text = Strings.ExportAsImage;
            }

            protected override void OnClick(EventArgs e) {
                base.OnClick (e);
                FolderBrowserDialog fbd = new FolderBrowserDialog();
                fbd.RootFolder = System.Environment.SpecialFolder.Desktop;
                if (fbd.ShowDialog() == DialogResult.OK) {
                    //TODO: Prompt for a file format
                    
                    //Execute
                    using (this.m_Model.Workspace.Lock()) {
                        DeckTraversalModel traversal = this.m_Model.Workspace.CurrentDeckTraversal;
                        DefaultDeckTraversalModel ddt = traversal as DefaultDeckTraversalModel;
                        if (ddt == null) {
                            LinkedDeckTraversalModel linked = traversal as LinkedDeckTraversalModel;
                            if (linked != null)
                                ddt = linked.LinkedModel as DefaultDeckTraversalModel;
                        }

                        if( ddt != null ) {
                            //PPTDeckIO.ExportDeck( ddt, fbd.SelectedPath, System.Drawing.Imaging.ImageFormat.Jpeg );
                            PPTDeckIO.ExportDeck( ddt, fbd.SelectedPath, System.Drawing.Imaging.ImageFormat.Png );
                        }
                    }
                }
            }

        }

        #endregion
        #region ExportDeckAsHTMLItem
        public class ExportDeckAsHTMLItem : MenuItem {
            private readonly PresenterModel model_;

            public bool ExportFancy = false;

            public ExportDeckAsHTMLItem(PresenterModel m) {
                model_ = m;
                this.Text = Strings.ExportAsHTML;
            }

            /// <summary>
            /// popus up a save dialogue, then creates a folder in the specified
            /// path containing an HTML file, along with images.
            /// </summary>
            /// <param name="e"></param>
            protected override void OnClick(EventArgs e) {
                base.OnClick(e);

                // Choose a destination folder and file name
                SaveFileDialog exportDeckToHTMLDialog = new SaveFileDialog();
                exportDeckToHTMLDialog.Title = "Name the new html file and folder";
                exportDeckToHTMLDialog.Filter = "HTML files (*.html;*.htm)|*.html;*.htm";
                string humanName = "";
                //set default name                
                using (this.model_.Workspace.Lock()) {
                    using (Synchronizer.Lock((~this.model_.Workspace.CurrentDeckTraversal).SyncRoot)) {
                        using (Synchronizer.Lock((~this.model_.Workspace.CurrentDeckTraversal).Deck.SyncRoot)) {
                            if ((~this.model_.Workspace.CurrentDeckTraversal).Deck.Filename != "")
                                humanName = exportDeckToHTMLDialog.FileName = (~this.model_.Workspace.CurrentDeckTraversal).Deck.Filename;
                            else humanName = exportDeckToHTMLDialog.FileName = (~this.model_.Workspace.CurrentDeckTraversal).Deck.HumanName;
                        }
                    }
                }
                ///Create a folder with the HTML file as well as the corresponding image
                ///files.
                if (exportDeckToHTMLDialog.ShowDialog() == DialogResult.OK) {
                    ///name of user's HTML file
                    string file_name = exportDeckToHTMLDialog.FileName;
                    ///name of folder that will hold the HTML files
                    string folder_name = Path.GetFullPath(file_name).Replace(Path.GetExtension(file_name),"") + "_files";
                    
                    
                    ///export the slide images to the folder
                    List<string> file_names;
                    using (this.model_.Workspace.Lock()) {
                        DeckTraversalModel traversal = this.model_.Workspace.CurrentDeckTraversal;
                        DefaultDeckTraversalModel ddt = traversal as DefaultDeckTraversalModel;
                        if (ddt == null) {
                            LinkedDeckTraversalModel linked = traversal as LinkedDeckTraversalModel;
                            if (linked != null)
                                ddt = linked.LinkedModel as DefaultDeckTraversalModel;
                        }

//                        file_names = PPTDeckIO.ExportDeck(ddt, folder_name, System.Drawing.Imaging.ImageFormat.Jpeg);
                        file_names = PPTDeckIO.ExportDeck( ddt, folder_name, System.Drawing.Imaging.ImageFormat.Png );
                    }

                    ///export row/column icons
                    if( ExportFancy ) {
                        UW.ClassroomPresenter.Properties.Resources.singleslide.Save( folder_name + "\\singleslide.jpg", System.Drawing.Imaging.ImageFormat.Jpeg );
                        UW.ClassroomPresenter.Properties.Resources.doubleslide.Save( folder_name + "\\doubleslide.jpg", System.Drawing.Imaging.ImageFormat.Jpeg );
                    }

                    ///Now, all of the images are in the specified folder, so we just need to make
                    ///the HTML file for them
                    string html = BuildHtml(file_names, folder_name, 1, file_name, humanName);
                    if (!ExportFancy) {
                        using (StreamWriter my_stream = new StreamWriter(file_name)) {
                            my_stream.Write(html);
                        }
                    }
                    else {
                        using (StreamWriter my_stream = new StreamWriter(file_name.Insert(file_name.LastIndexOf('.'), "_1"))) {
                            my_stream.Write(html);
                        }
                        html = BuildHtmlTable( file_names, folder_name, 2, file_name, humanName);
                        using( StreamWriter my_stream = new StreamWriter( file_name.Insert( file_name.LastIndexOf( '.' ), "_2" ) ) ) {
                            my_stream.Write( html );
                        }
                    }
                     

                }
            }

            ///writes an HTML file that contains all of the images that have been saved
            ///in the folder 'folder_name' as a Table
            ///NOTE: this method assumes that images are named with their human name
            ///followed with their number (specified by a 2 digit number).
            ///<param name="file_names">the names of all of the image files we will be reffering to in HTML</param>
            private string BuildHtmlTable(List<string> file_names, string folder_path, int columns, string file_name, string humanName){

                string trimmed_file_name = file_name.Substring(file_name.LastIndexOf('\\') + 1);

                ///build the HTML from this data
                ///Intro stuff and title information
                string html = "<html>" +'\n' +
                    "<head> <font face = \" Arial \"> <title>" + humanName +"</title>" +
                    "<h2>" + humanName + "</h2>" + 
                    "\n <hr width = 720 align = left> "+
                    "</head>" + '\n' +
                    "<body bgcolor=#C0C0C0> " + '\n';                   // Hard coding grey
                ///get the correct directory from the folder_path
                string[] folder_path_parts = folder_path.Split('\\');
                string directory = folder_path_parts[folder_path_parts.Length - 1];

                html += "<table> \n";
                html += "<td align = left> Slide View: ";
                html += "<tr><td align = left>" +
                    " <a href = \"" + trimmed_file_name.Insert(trimmed_file_name.LastIndexOf('.'), "_1") + "\"> " +
                    "<img border = 0 src = \"" + directory + "\\singleslide.jpg\"></a>" +
                    "  <a href = \"" + trimmed_file_name.Insert(trimmed_file_name.LastIndexOf('.'), "_2") +"\"> " +
                    "<img border = 0 src = \"" + directory + "\\doubleslide.jpg\"> </a>";


                for (int i = 0; i < file_names.Count; i++) {
                    if (i % columns == 0) {
                        html += "<tr>";
                    }
                    html += "<td>";


                    ///width and height made to fit within page margins.
                    html += "<img width =" + 650/columns + 
                        " height = " + 488 / columns +
                        " src =\"" + directory + 
                        "\\" + file_names[i] + "\"> <br> \n";
                }
                html += "</table> \n";
                html += "</body> \n" +
                    "</html>";

                return html;
            }

            ///writes an HTML file that contains all of the images that have been saved
            ///in the folder 'folder_name'.  Images are just given sequentially without any structure.
            ///NOTE: this method assumes that images are named with their human name
            ///followed with their number (specified by a 2 digit number).
            ///<param name="file_names">the names of all of the image files we will be reffering to in HTML</param>
            private string BuildHtml(List<string> file_names, string folder_path, int columns, string file_name, string humanName) {

                string trimmed_file_name = file_name.Substring(file_name.LastIndexOf('\\') + 1);

                ///build the HTML from this data
                ///Intro stuff and title information
                string html = "<html>" + '\n' +
                    "<head> <font face = \" Arial \"> <title>" + humanName + "</title>" +
                    "<h2>" + humanName + "</h2>" + 
                    "\n <hr width = 720 align = left> " +
                    "</head>\r\n" +  
                    "<body bgcolor=#C0C0C0> <br>" + "\r\n";                   // Hard coding grey
                ///get the correct directory from the folder_path
                string[] folder_path_parts = folder_path.Split('\\');
                string directory = folder_path_parts[folder_path_parts.Length - 1];
                string directory_slash = directory.Replace( '\\', '/' );

                if( ExportFancy ) {
                    html +=
                        " <a href = \"" + trimmed_file_name.Insert( trimmed_file_name.LastIndexOf( '.' ), "_1" ).Replace('\\', '/') + "\"> " +
                        "<img border = 0 src = \"" + directory_slash + "\\singleslide.jpg\"></a>" +
                        "  <a href = \"" + trimmed_file_name.Insert( trimmed_file_name.LastIndexOf( '.' ), "_2" ).Replace('\\', '/') + "\"> " +
                        "<img border = 0 src = \"" + directory_slash + "\\doubleslide.jpg\"> </a> <br>";
                }

                for (int i = 0; i < file_names.Count; i++) {
                    ///width and height made to fit within page margins.
                    html += "<img src =\"" + directory_slash +
                        "/" + file_names[i] + "\"> <br> \n";
                }

                html += "</body> \n" +
                    "</html>";

                return html;
            }
                
                
                

            
        }
        #endregion

        #region ExportInkMenuItem

        public class ExportInkMenuItem : MenuItem {
            private readonly PresenterModel m_Model;

            public ExportInkMenuItem(PresenterModel m) {
                this.m_Model = m;
                this.Text = Strings.ExportInk;
            }

            protected override void OnClick(EventArgs e) {
                base.OnClick (e);

                InkExport( Microsoft.Ink.PersistenceFormat.InkSerializedFormat, 
                    "Ink serialized format binary files (*.isf)|*.isf|All files (*.*)|*.*" );
            }

            public void InkExport( Microsoft.Ink.PersistenceFormat persistenceFormat, string filterString ) {
                byte[] buffer = null;

                //variables used to create filename
                string file = null;
                string ext = null;

                string defaultFileName = "SavedInk";

                int slideIndex = 0;
                //Create a new save file dialog
                SaveFileDialog saveFileDialog = new SaveFileDialog();
                saveFileDialog.Filter = filterString;
                saveFileDialog.FileName = defaultFileName;
                if( saveFileDialog.ShowDialog() == DialogResult.OK ) {
                    //Iterate throught the slide deck to check for ink on each one
                    using( this.m_Model.Workspace.Lock() ) {
                        using( Synchronizer.Lock( this.m_Model.Workspace.CurrentDeckTraversal.Value.SyncRoot ) ) {
                            DeckModel deck = this.m_Model.Workspace.CurrentDeckTraversal.Value.Deck;
                            using( Synchronizer.Lock( deck.SyncRoot ) ) {
                                foreach( SlideModel slide in deck.Slides ) {
                                    using( Synchronizer.Lock( slide.SyncRoot ) ) {
                                        foreach( SheetModel sheet in slide.AnnotationSheets ) {
                                            if( sheet is InkSheetModel ) {
                                                using( Synchronizer.Lock( sheet.SyncRoot ) ) {
                                                    Microsoft.Ink.Ink ink = ((InkSheetModel)sheet).Ink;
                                                    if( ink.Strokes.Count != 0 ) {
                                                        //figure out the filename
                                                        string fileNameTemp = saveFileDialog.FileName;
                                                        ext = Path.GetExtension( fileNameTemp );
                                                        file = Path.GetFileNameWithoutExtension( fileNameTemp );
                                                        string n = slideIndex.ToString();
                                                        string fileName = file + "[" + n + "]" + ext;
                                                        buffer = ink.Save( persistenceFormat );
                                                        
                                                        //open a stream and output the buffer
                                                        FileStream myStream = System.IO.File.Create( fileName );
                                                        myStream.Write( buffer, 0, buffer.Length );
                                                        myStream.Close();

                                                        slideIndex++;
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }

        #endregion

        }
    }
}
