// $Id: SaveDeckMenuItem.cs 1629 2008-07-07 18:01:29Z cmprince $

using System;
using System.IO;
using System.Threading;
using System.Windows.Forms;

using UW.ClassroomPresenter.Decks;

using UW.ClassroomPresenter.Model;
using UW.ClassroomPresenter.Model.Presentation;

namespace UW.ClassroomPresenter.Viewer.Menus {
    public class SaveDeckMenuItem : MenuItem {
        private readonly PresenterModel m_Model;
        private readonly SaveDeckDialog m_SaveDeckDialog;

        public SaveDeckMenuItem(PresenterModel model, DeckMarshalService marshal) {
            this.m_Model = model;
            this.m_SaveDeckDialog = new SaveDeckDialog(model, marshal);

            this.Text = Strings.SaveDeck;
            this.Shortcut = Shortcut.CtrlS;
            this.ShowShortcut = true;
        }

        protected override void OnClick(EventArgs e) {
            base.OnClick(e);
            //Check to see if we already have a filename
            bool showDialog = false;
            using (this.m_Model.Workspace.Lock()) {
                //Sanity check
                if (this.m_Model.Workspace.CurrentDeckTraversal == null)
                    return;
                using (Synchronizer.Lock((~this.m_Model.Workspace.CurrentDeckTraversal).SyncRoot)) {
                    using (Synchronizer.Lock((~this.m_Model.Workspace.CurrentDeckTraversal).Deck.SyncRoot)) {
                        string filename = (~this.m_Model.Workspace.CurrentDeckTraversal).Deck.Filename;
                        if (filename != "") {
                            UW.ClassroomPresenter.Decks.PPTDeckIO.SaveAsCP3(new FileInfo(filename), (~this.m_Model.Workspace.CurrentDeckTraversal).Deck);
                            this.m_Model.Workspace.CurrentDeckTraversal.Value.Deck.Dirty = false;
                        } else {
                            showDialog = true;
                        }
                    }
                }
            }
            if (showDialog) {
                this.m_SaveDeckDialog.SaveDeck(null);
            }

        }
    }

    
    public class SaveDeckAsMenuItem : MenuItem {
        private readonly PresenterModel m_Model;
        private readonly SaveDeckDialog m_SaveDeckDialog;

        public SaveDeckAsMenuItem(PresenterModel model, DeckMarshalService marshal) {
            this.m_Model = model;
            this.m_SaveDeckDialog = new SaveDeckDialog(model, marshal);

            this.Text = Strings.SaveDeckAs;
            this.Shortcut = Shortcut.F12;
            this.ShowShortcut = true;
        }

        protected override void OnClick(EventArgs e) {
            base.OnClick(e);
            this.m_SaveDeckDialog.SaveDeck(null);
        }
    }
   
    public class SaveAllDecksMenuItem : MenuItem
        {

        private readonly PresenterModel m_Model;
        private readonly SaveDeckDialog m_SaveDeckDialog;
        public SaveAllDecksMenuItem(PresenterModel model, DeckMarshalService marshal)
            {
            this.m_Model = model;
            this.m_SaveDeckDialog = new SaveDeckDialog(model, marshal);

            this.Text = Strings.SaveAllDecks;
            }

        protected override void OnClick(EventArgs e)
            {
            base.OnClick(e);

            //Check to see if we already have a filename
            using (Synchronizer.Lock(this.m_Model.Workspace.CurrentPresentation.SyncRoot))
                {
                //This collection contains decks that have been closed.  I'm not sure if this is 
                //the desired behavior.  It saves closed decks correctly, so I think it might be 
                //what we want...
                foreach (DeckTraversalModel deck in (~this.m_Model.Workspace.CurrentPresentation).DeckTraversals)
                    {
                    bool showDialog = false;
                    using (this.m_Model.Workspace.Lock())
                        {
                        //Sanity check
                        if (deck == null)
                            return;

                        string filename = deck.Deck.Filename;
                        if (filename != "") {
                            UW.ClassroomPresenter.Decks.PPTDeckIO.SaveAsCP3(new FileInfo(filename), deck.Deck);
                            using (Synchronizer.Lock(deck.Deck.SyncRoot)) {
                                deck.Deck.Dirty = false;
                            }
                        }
                        else {
                            showDialog = true;
                        }
                        }


                    if (showDialog)
                        {
                        this.m_SaveDeckDialog.SaveDeck(null, deck);
                        }
                    }
                }
            }
        }
    
    
    public class SaveAllDecksAsMenuItem : MenuItem
        {

        private readonly PresenterModel m_Model;
        private readonly SaveDeckDialog m_SaveDeckDialog;
        public SaveAllDecksAsMenuItem(PresenterModel model, DeckMarshalService marshal)
            {
            this.m_Model = model;
            this.m_SaveDeckDialog = new SaveDeckDialog(model, marshal);

            this.Text = Strings.SaveAllDecksAs;
            }

        protected override void OnClick(EventArgs e)
            {
            base.OnClick(e);
            //Check to see if we already have a filename
            using (Synchronizer.Lock(this.m_Model.Workspace.CurrentPresentation.SyncRoot))
                {
                //This collection contains decks that have been closed.  I'm not sure if this is 
                //the desired behavior.
                foreach (DeckTraversalModel deck in (~this.m_Model.Workspace.CurrentPresentation).DeckTraversals)
                    {
                    using (this.m_Model.Workspace.Lock())
                        {
                        //Sanity check
                        if (deck == null)
                            return;

                        string filename = deck.Deck.Filename;
                        
                        this.m_SaveDeckDialog.SaveDeck(null, deck);
                        }
                    }
                }
            }
        }
        
}
