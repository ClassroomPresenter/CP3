// $Id: SaveDeckDialog.cs 1320 2007-03-22 19:59:36Z fred $

using System;
using System.IO;
using System.Windows.Forms;
using System.Drawing;

using UW.ClassroomPresenter.Model;
using UW.ClassroomPresenter.Model.Presentation;
using Decks = UW.ClassroomPresenter.Decks;

namespace UW.ClassroomPresenter.Decks {
    public class SaveDeckDialog {
        private readonly PresenterModel m_Model;
        private readonly DeckMarshalService m_Marshal;

        public SaveDeckDialog(PresenterModel model, DeckMarshalService marshal) {
            this.m_Model = model;
            this.m_Marshal = marshal;

            }

        public void SaveDeck(IWin32Window window) {
            SaveFileDialog save = new SaveFileDialog();

            save = new SaveFileDialog();
            //Set the default name
            using (this.m_Model.Workspace.Lock()) {
                using (Synchronizer.Lock((~this.m_Model.Workspace.CurrentDeckTraversal).SyncRoot)) {
                    using (Synchronizer.Lock((~this.m_Model.Workspace.CurrentDeckTraversal).Deck.SyncRoot)) {
                        save.FileName = (~this.m_Model.Workspace.CurrentDeckTraversal).Deck.HumanName;
                        }
                    }
                }
            save.Filter = "CP3 files (*.cp3)|*.cp3|All Files (*.*)|*.*";
            if (save.ShowDialog() == DialogResult.OK) {
                //Get the "current" deck
                //Here we define the current deck as the one whose tab is currently selected
                using (this.m_Model.Workspace.Lock()) {
                    using (Synchronizer.Lock((~this.m_Model.Workspace.CurrentDeckTraversal).SyncRoot)) {
                        using (Synchronizer.Lock((~this.m_Model.Workspace.CurrentDeckTraversal).Deck.SyncRoot)) {
                            Decks.PPTDeckIO.SaveAsCP3(new FileInfo(save.FileName), (~this.m_Model.Workspace.CurrentDeckTraversal).Deck);
                            }
                        }
                    }
                }
            }

        //Another version of the function that will work for the SaveAll menu items 
        //(and any other case where we need to save a deck that isn't the current deck.)
        public void SaveDeck(IWin32Window window, DeckTraversalModel deck) {
            SaveFileDialog save = new SaveFileDialog();

            save = new SaveFileDialog();

            using (Synchronizer.Lock(deck.Deck.SyncRoot)) {
                //Set the default name
                save.FileName = deck.Deck.HumanName;

                save.Filter = "CP3 files (*.cp3)|*.cp3|All Files (*.*)|*.*";
                if (save.ShowDialog() == DialogResult.OK) {
                    Decks.PPTDeckIO.SaveAsCP3(new FileInfo(save.FileName), deck.Deck);
                    }
                }
            }
        }
    public class SaveOnCloseDialog : Form{
        private readonly PresenterModel m_Model;
        private readonly DeckMarshalService m_Marshal;
        private SaveDeckDialog m_SaveDeckDialog;
        private EventArgs e;

        public SaveOnCloseDialog (PresenterModel model, DeckMarshalService marshal, EventArgs ea) {
            this.m_Model = model;
            this.m_Marshal = marshal;
            this.m_SaveDeckDialog = new SaveDeckDialog(this.m_Model, this.m_Marshal);
            this.e = ea;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;

            this.Size = new Size(300, 110);
            this.Text = "Classroom Presenter";
            this.Location = new Point(1000, 1000);

            Label label = new Label();
            label.Text = "Would you like to save your decks?";
            label.Location = new Point(10, 10);
            label.Size = new Size(250, 20);
            this.Controls.Add(label);

            Button btnYes = new Button();
            btnYes.Parent = this;
            btnYes.Text = "Yes";
            btnYes.Location = new Point(20, 40);
            btnYes.DialogResult = DialogResult.OK;
            btnYes.Click += new EventHandler(btnYes_Click);

            Button btnNo = new Button();
            btnNo.Parent = this;
            btnNo.Text = "No";
            btnNo.Location = new Point(100, 40);
            btnNo.DialogResult = DialogResult.Cancel;//??
            btnNo.Click += new EventHandler(btnNo_Click);

            Button btnCancel = new Button();
            btnCancel.Parent = this;
            btnCancel.Text = "Cancel";
            btnCancel.Location = new Point(180, 40);
            btnCancel.DialogResult = DialogResult.Cancel;
            btnCancel.Click += new EventHandler(btnCancel_Click);


            }
        public void btnYes_Click(object sender, EventArgs e) {

            using (Synchronizer.Lock(this.m_Model.Workspace.CurrentPresentation.SyncRoot)) {
                //This collection contains decks that have been closed.  I'm not sure if this is 
                //the desired behavior.
                foreach (DeckTraversalModel deck in (~this.m_Model.Workspace.CurrentPresentation).DeckTraversals) {
                    using (this.m_Model.Workspace.Lock()) {
                        //Sanity check
                        if (deck == null)
                            return;

                        string filename = deck.Deck.Filename;

                        this.m_SaveDeckDialog.SaveDeck(null, deck);
                        }
                    }
                }
            }
        public void btnNo_Click(object sender, EventArgs e) {
            //Set a registry persisted variable so we don't show this next time...  or add third button for this, probably better.
            }
        public void btnCancel_Click(object sender, EventArgs e) { 
            ((FormClosingEventArgs)(this.e)).Cancel = true;
            }
        }
    public class SaveOnCloseDeckDialog : Form {
        private readonly PresenterModel m_Model;
        private readonly DeckMarshalService m_Marshal;
        private SaveDeckDialog m_SaveDeckDialog;
        private EventArgs e;
        public bool Cancel;

        public SaveOnCloseDeckDialog(PresenterModel model, DeckMarshalService marshal, EventArgs ea) {
            this.m_Model = model;
            this.m_Marshal = marshal;
            this.m_SaveDeckDialog = new SaveDeckDialog(this.m_Model, this.m_Marshal);
            this.e = ea;
            this.Cancel = false;

            this.Size = new Size(300, 100);
            this.Text = "Classroom Presenter";
            this.Location = new Point(1000, 1000);

            Label label = new Label();
            label.Text = "Would you like to save your deck?";
            label.Location = new Point(10, 10);
            label.Size = new Size(200, 20);
            this.Controls.Add(label);

            Button btnYes = new Button();
            btnYes.Parent = this;
            btnYes.Text = "Yes";
            btnYes.Location = new Point(20, 40);
            btnYes.DialogResult = DialogResult.OK;
            btnYes.Click += new EventHandler(btnYes_Click);

            Button btnNo = new Button();
            btnNo.Parent = this;
            btnNo.Text = "No";
            btnNo.Location = new Point(100, 40);
            btnNo.DialogResult = DialogResult.Cancel;//??
            btnNo.Click += new EventHandler(btnNo_Click);

            Button btnCancel = new Button();
            btnCancel.Parent = this;
            btnCancel.Text = "Cancel";
            btnCancel.Location = new Point(180, 40);
            btnCancel.DialogResult = DialogResult.Cancel;
            btnCancel.Click += new EventHandler(btnCancel_Click);


        }
        public void btnYes_Click(object sender, EventArgs e) {
            //At CloseDeckMenuItem I lock this...  a problem?
            using( Synchronizer.Lock(this.m_Model.Workspace.CurrentDeckTraversal.SyncRoot)){  
                        //Sanity check
                        if (this.m_Model.Workspace.CurrentDeckTraversal.Value == null)
                            return;

                        string filename = this.m_Model.Workspace.CurrentDeckTraversal.Value.Deck.Filename;

                        this.m_SaveDeckDialog.SaveDeck(null, this.m_Model.Workspace.CurrentDeckTraversal.Value);        
            }                   
        }

            
        public void btnNo_Click(object sender, EventArgs e) {
            //Set a registry persisted variable so we don't show this next time...  or add third button for this, probably better.
        }
        public void btnCancel_Click(object sender, EventArgs e) {
            this.Cancel = true;
        }
    }
    }
