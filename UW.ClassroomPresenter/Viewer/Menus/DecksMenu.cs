// $Id: DecksMenu.cs 1787 2008-10-09 23:17:16Z lining $

using System;
using System.Drawing;
using System.Diagnostics;
using System.Threading;
using System.Windows.Forms;

using UW.ClassroomPresenter.Model;
using UW.ClassroomPresenter.Model.Presentation;
using UW.ClassroomPresenter.Viewer.PropertiesForm;

namespace UW.ClassroomPresenter.Viewer.Menus {
    public class DecksMenu : MenuItem {
        private readonly PresenterModel m_Model;
        private DeckTraversalModel current_deck_;
        private EventQueue queue_;
        IDisposable current_deck_traversal_listener_;
        private bool m_Disposed = false;

        public DecksMenu(ControlEventQueue dispatcher, PresenterModel model) {
            this.m_Model = model;

            this.Text = Strings.Decks;

            queue_ = dispatcher;

            using (Synchronizer.Lock(this.m_Model.Workspace.CurrentDeckTraversal.SyncRoot)) {
                this.current_deck_traversal_listener_ = this.m_Model.Workspace.CurrentDeckTraversal.ListenAndInitialize(queue_, delegate(Property<DeckTraversalModel>.EventArgs args) {
                    ///remove all the current slide menu items
                    for (int i = this.MenuItems.Count; --i >= 0; ) {
                        if ((this.MenuItems[i] is UW.ClassroomPresenter.Viewer.FilmStrips.FilmStripContextMenu.InsertSlidesFromFileMenuItem) ||
                            (this.MenuItems[i] is UW.ClassroomPresenter.Viewer.FilmStrips.FilmStripContextMenu.RemoveSlideMenuItem))

                            this.MenuItems.RemoveAt(i);
                        else if ((this.MenuItems[i] is UW.ClassroomPresenter.Viewer.FilmStrips.FilmStripContextMenu.InsertSlideMenuItem)) {
                            this.MenuItems.RemoveAt(i);
                            this.MenuItems.RemoveAt(--i);
                        }
                    }

                    current_deck_ = args.New;

                    if (current_deck_ != null) {
                        ///add the new, fresh items with the correct deck on.
                        ///
                        this.MenuItems.Add(new MenuItem("-")); // Text of "-" signifies a separator bar.
                        this.MenuItems.Add(new UW.ClassroomPresenter.Viewer.FilmStrips.FilmStripContextMenu.InsertSlideMenuItem(current_deck_, this.m_Model));
                        this.MenuItems.Add(new UW.ClassroomPresenter.Viewer.FilmStrips.FilmStripContextMenu.InsertSlidesFromFileMenuItem(current_deck_, this.m_Model));
                        this.MenuItems.Add(new UW.ClassroomPresenter.Viewer.FilmStrips.FilmStripContextMenu.RemoveSlideMenuItem(current_deck_, this.m_Model));

                        if ((current_deck_.Deck.Disposition & DeckDisposition.Remote) != 0) {
                            for (int i = 0; i < this.MenuItems.Count; i++) {
                                if (this.MenuItems[i] is SetDeckBackgroundTemplateMenuItem || this.MenuItems[i] is SetDeckBkgColorMenuItem
                                    || this.MenuItems[i] is SetSlideBackgroundTemplateMenuItem || this.MenuItems[i] is SetSlideBkgColorMenuItem)
                                    this.MenuItems[i].Enabled = false;
                            }
                        }
                        else {
                            for (int i = 0; i < this.MenuItems.Count; i++) {
                                if (this.MenuItems[i] is SetDeckBackgroundTemplateMenuItem || this.MenuItems[i] is SetDeckBkgColorMenuItem
                                    || this.MenuItems[i] is SetSlideBackgroundTemplateMenuItem || this.MenuItems[i] is SetSlideBkgColorMenuItem)
                                    this.MenuItems[i].Enabled = true;
                            }
                        }
                    }
                });
            }

            this.MenuItems.Add(new CreateBlankWhiteboardDeckMenuItem(this.m_Model));
            this.MenuItems.Add(new MenuItem("-")); // Text of "-" signifies a separator bar.

            // Note: These used to be in the "Slides" menu (see Bug 988).
            this.MenuItems.Add(new SetDeckBkgColorMenuItem(model));
            this.MenuItems.Add(new SetSlideBkgColorMenuItem(model));
            this.MenuItems.Add(new SetDeckBackgroundTemplateMenuItem(model));
            this.MenuItems.Add(new SetSlideBackgroundTemplateMenuItem(model));
//            this.MenuItems.Add(new DeckMatcherMenuItem(model));
//            this.MenuItems.Add(new MenuItem("-")); // Text of "-" signifies a separator bar.
        }

        protected override void Dispose(bool disposing) {
            if (this.m_Disposed) return;
            this.m_Disposed = true;
            try {
                if (disposing) {
                    this.current_deck_traversal_listener_.Dispose();
                }
            } finally {
                base.Dispose(disposing);
            }
        }

        public class CreateBlankWhiteboardDeckMenuItem : MenuItem {
            private readonly PresenterModel m_Model;

            public CreateBlankWhiteboardDeckMenuItem(PresenterModel model) {
                this.m_Model = model;

                this.Text = Strings.CreateBlankWhiteboardDeck;
                this.Shortcut = Shortcut.CtrlW;
                this.ShowShortcut = true;
            }

            protected override void OnClick(EventArgs e) {
                base.OnClick(e);

                // Change the name of whiteboard according to the number of it
                string wbname = "WhiteBoard "+ (++UW.ClassroomPresenter.Viewer.ViewerForm.white_board_num).ToString();
                // NOTE: This code is duplicated in DeckNavigationToolBarButtons.WhiteboardToolBarButton and DeckMessage.UpdateContext
                DeckModel deck = new DeckModel(Guid.NewGuid(), DeckDisposition.Whiteboard, wbname);
                using (Synchronizer.Lock(deck.SyncRoot)) {
                    SlideModel slide = new SlideModel(Guid.NewGuid(), new LocalId(), SlideDisposition.Empty, UW.ClassroomPresenter.Viewer.ViewerForm.DEFAULT_SLIDE_BOUNDS);

                    deck.InsertSlide(slide);

                    using (Synchronizer.Lock(deck.TableOfContents.SyncRoot)) {
                        TableOfContentsModel.Entry entry = new TableOfContentsModel.Entry(Guid.NewGuid(), deck.TableOfContents, slide);
                        deck.TableOfContents.Entries.Add(entry);
                    }
                }

                DeckTraversalModel traversal = new SlideDeckTraversalModel(Guid.NewGuid(), deck);

                using (this.m_Model.Workspace.Lock()) {
                    if ((~this.m_Model.Workspace.CurrentPresentation) != null) {
                        using (Synchronizer.Lock((~this.m_Model.Workspace.CurrentPresentation).SyncRoot)) {
                            (~this.m_Model.Workspace.CurrentPresentation).DeckTraversals.Add(traversal);
                        }
                    } else {
                        this.m_Model.Workspace.DeckTraversals.Add(traversal);
                    }
                }
            }
        }

        public class SetDeckBkgColorMenuItem : MenuItem {
            private readonly PresenterModel m_Model;

            public SetDeckBkgColorMenuItem(PresenterModel model) {
                this.m_Model = model;
                this.Text = Strings.SetDeckBkgColor;
                this.Enabled = true;
            }

            protected override void OnClick(EventArgs e) {
                base.OnClick(e);
                ColorDialog cd = new ColorDialog();
                cd.AllowFullOpen = true;
                cd.AnyColor = true;
                cd.SolidColorOnly = false;

                if (cd.ShowDialog() == DialogResult.OK) {
                    using (this.m_Model.Workspace.Lock()) {
                        DeckTraversalModel traversal = this.m_Model.Workspace.CurrentDeckTraversal;
                        if (traversal != null) {
                            using (Synchronizer.Lock(traversal.SyncRoot)) {
                                DeckModel deck = traversal.Deck;
                                using (Synchronizer.Lock(deck.SyncRoot)) {
                                    deck.DeckBackgroundColor = cd.Color;
                                    deck.Dirty = true;
                                }
                            }
                            
                        }
                    }
                }
            }
        }

        public class SetSlideBkgColorMenuItem : MenuItem {
            private readonly PresenterModel m_Model;

            public SetSlideBkgColorMenuItem(PresenterModel model) {
                this.Text = Strings.SetSlideBkgColor;
                this.Enabled = true;
                this.m_Model = model;
            }

            protected override void OnClick(EventArgs e) {
                base.OnClick(e);
                ColorDialog cd = new ColorDialog();
                cd.AllowFullOpen = true;
                cd.AnyColor = true;
                cd.SolidColorOnly = false;

                if (cd.ShowDialog() == DialogResult.OK) {
                    using (this.m_Model.Workspace.Lock()) {
                        DeckTraversalModel traversal = this.m_Model.Workspace.CurrentDeckTraversal;
                        if (traversal != null) {
                            using (Synchronizer.Lock(traversal.SyncRoot)) {
                                using (Synchronizer.Lock(traversal.Current.Slide.SyncRoot)) {
                                    traversal.Current.Slide.BackgroundColor = cd.Color;
                                    using (Synchronizer.Lock(traversal.Deck.SyncRoot)) {
                                        traversal.Deck.Dirty = true;
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        public class DeckMatcherMenuItem : MenuItem {
            private PresenterModel localModel;

            public DeckMatcherMenuItem(PresenterModel model) {
                localModel = model;
                this.Text = Strings.DeckMatcher;
                this.Enabled = false;
            }

            protected override void OnClick(EventArgs e) {
                UW.ClassroomPresenter.Viewer.DeckMatcherForm.DeckMatcherForm match =
                    new UW.ClassroomPresenter.Viewer.DeckMatcherForm.DeckMatcherForm(localModel);
                match.ShowDialog();
            }
        }

        public class SetDeckBackgroundTemplateMenuItem : MenuItem {
            private PresenterModel m_Model;

            public SetDeckBackgroundTemplateMenuItem(PresenterModel model) {
                this.m_Model = model;
                this.Text = Strings.SetDeckBackgroundTemplate;
                this.Enabled = true;
            }

            protected override void OnClick(EventArgs e) {
                base.OnClick(e);
                BackgroundPropertiesForm bkgForm = new BackgroundPropertiesForm(this.m_Model);
                bkgForm.ApplyToCurrentSlideOnly = false;
                bkgForm.ShowDialog();
            }
        }

        public class SetSlideBackgroundTemplateMenuItem : MenuItem
        {
            private PresenterModel m_Model;

            public SetSlideBackgroundTemplateMenuItem(PresenterModel model)
            {
                this.m_Model = model;
                this.Text = Strings.SetSlideBackgroundTemplate;
                this.Enabled = true;
            }

            protected override void OnClick(EventArgs e)
            {
                base.OnClick(e);
                BackgroundPropertiesForm bkgForm = new BackgroundPropertiesForm(this.m_Model);
                bkgForm.ApplyToCurrentSlideOnly = true;
                bkgForm.ShowDialog();
            }
        }

    }
}
