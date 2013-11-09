// $Id: OpenDeckDialog.cs 1881 2009-06-08 20:01:08Z cmprince $

using System;
using System.IO;
using System.ComponentModel;
using System.Windows.Forms;

using UW.ClassroomPresenter.Model;
using UW.ClassroomPresenter.Model.Presentation;

namespace UW.ClassroomPresenter.Decks {
    public class OpenDeckDialog {
        private readonly PresenterModel m_Model;
        private readonly DeckMarshalService m_Marshal;
        ///we will use this in order to set the HumanName of the deck, which will
        ///give us the correct filename when we look at it in the tab view for the deck.
        ///Since we can't really pass variables in delegates, we have to make this variable more global.
        private FileInfo file_to_open_;

        public OpenDeckDialog(PresenterModel model, DeckMarshalService marshal) {
            this.m_Model = model;
            this.m_Marshal = marshal;
        }

        public void OpenDeck(IWin32Window window) {
            OpenFileDialog open = new OpenFileDialog();
            open.Filter = "CP3, PPT, PPTX, XPS files (*.cp3; *.ppt; *.pptx; *.xps)|*.cp3;*.ppt;*.pptx;*.xps";  
            if (open.ShowDialog(window) == DialogResult.OK) {
                this.OpenDeck(new FileInfo(open.FileName));
            }
        }

        public void OpenDeck(FileInfo file) {
            ///we will use this in order to set the HumanName of the deck, which will
            ///give us the correct filename when we look at it in the tab view for the deck.
            file_to_open_ = file;
            
            if (file_to_open_.Name.EndsWith("xps")) {
                OpenXps(file_to_open_);
                return;
            }

            Misc.ProgressBarForm pbf = new Misc.ProgressBarForm("Opening \"" + file.Name + "\"...");
            pbf.DoWork += new DoWorkEventHandler(this.RunWorker);
            pbf.Show();
            pbf.StartWork(file);
        }

        private void RunWorker(object sender, DoWorkEventArgs progress) {
            DeckModel deck = this.m_Marshal.ReadDeckAsync(((FileInfo)progress.Argument),
                ((BackgroundWorker)sender), progress);
            // Failure is indicated by a null deck - not clear if this is the best way to handle things
            if (deck != null) {

                using (Synchronizer.Lock(deck)) {
                    ///in order to have the tabbed files display correctly, we need to set their
                    ///'HumanName' property. Since most people don't want to see the '.cp3'  or '.ppt'part 
                    ///we'll just erase this. BUG 961 fixed
                    ///Also erase '.pptx', BUG 1130 fixed
                    string fileName = file_to_open_.Name;
                    if (file_to_open_.Name.EndsWith(".cp3") || file_to_open_.Name.EndsWith(".ppt"))
                        fileName = fileName.Remove(fileName.Length - 4);
                    if (file_to_open_.Name.EndsWith(".pptx"))
                        fileName = fileName.Remove(fileName.Length - 5);
                    deck.HumanName = fileName;

                    //Since opened from a file, the dirty bit should be false, even if
                    //it's a student submission deck.
                    deck.Dirty = false;
                    if (deck.Disposition == DeckDisposition.StudentSubmission) {
                        deck.Group = Network.Groups.Group.Submissions;
                    }

                }
                DeckTraversalModel traversal = new SlideDeckTraversalModel(Guid.NewGuid(), deck);

#if WEBSERVER
                // TODO CMPRINCE WEBSERVER: This isn't an ideal solution
                // Export the deck now as images for the web server
                using (Synchronizer.Lock(traversal.SyncRoot)) {
                    using (Synchronizer.Lock(traversal.Deck.SyncRoot)) {
                        string imagePath = Path.Combine( Web.WebService.WebRoot, "images\\decks\\" + traversal.Deck.HumanName + "\\" + traversal.Deck.HumanName + "\\" );
                        PPTDeckIO.ExportDeck( (DefaultDeckTraversalModel)traversal, imagePath, System.Drawing.Imaging.ImageFormat.Png );
                        string imagePath2 = Path.Combine(Web.WebService.WebRoot, "images\\decks\\" + traversal.Deck.HumanName + "\\" + traversal.Deck.HumanName + "\\thumbnails\\");
                        PPTDeckIO.ExportDeck((DefaultDeckTraversalModel)traversal, imagePath2, System.Drawing.Imaging.ImageFormat.Png, 0, 0, 0.5f);
                    }
                }
#endif

                using (this.m_Model.Workspace.Lock()) {
                    if (~this.m_Model.Workspace.CurrentPresentation != null) {
                        using (Synchronizer.Lock((~this.m_Model.Workspace.CurrentPresentation).SyncRoot)) {
                            (~this.m_Model.Workspace.CurrentPresentation).DeckTraversals.Add(traversal);
                        }
                    }
                    else {
                        this.m_Model.Workspace.DeckTraversals.Add(traversal);
                    }
                }
            }
        }

        private void OpenXps(FileInfo file)
        {
             DeckModel deck = this.m_Marshal.ReadDeckAsync(file, null ,null);

            if (deck != null) {
                using (Synchronizer.Lock(deck)) {
                    string fileName = file_to_open_.Name;
                    if (file_to_open_.Name.EndsWith(".xps"))
                        fileName = fileName.Remove(fileName.Length - 4);
                    else return;

                    deck.HumanName = fileName;

                    deck.Dirty = false;
                    if (deck.Disposition == DeckDisposition.StudentSubmission) {
                        deck.Group = Network.Groups.Group.Submissions;
                    }
                }
                DeckTraversalModel traversal = new SlideDeckTraversalModel(Guid.NewGuid(), deck);

                using (this.m_Model.Workspace.Lock()) {
                    if (~this.m_Model.Workspace.CurrentPresentation != null) {
                        using (Synchronizer.Lock((~this.m_Model.Workspace.CurrentPresentation).SyncRoot)) {
                            (~this.m_Model.Workspace.CurrentPresentation).DeckTraversals.Add(traversal);
                        }
                    }
                    else {
                        this.m_Model.Workspace.DeckTraversals.Add(traversal);
                    }
                }
            }
        }
    }
}
