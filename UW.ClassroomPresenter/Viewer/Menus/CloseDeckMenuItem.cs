// $Id: CloseDeckMenuItem.cs 1622 2008-06-26 00:31:16Z lamphare $

using System;
using System.Threading;
using System.Windows.Forms;

using UW.ClassroomPresenter.Decks;

using UW.ClassroomPresenter.Model;
using UW.ClassroomPresenter.Model.Workspace;
using UW.ClassroomPresenter.Model.Presentation;

namespace UW.ClassroomPresenter.Viewer.Menus {
    public class CloseDeckMenuItem : MenuItem {
        private readonly ControlEventQueue m_EventQueue;
        private readonly IDisposable m_CurrentDeckTraversalChangedDispatcher;

        private readonly PresenterModel m_Model;
        private DeckTraversalModel m_CurrentDeckTraversal;
        private DeckMarshalService m_Marshal;

        public CloseDeckMenuItem(ControlEventQueue dispatcher, PresenterModel model, DeckMarshalService marshal) {
            this.m_EventQueue = dispatcher;
            this.m_Model = model;
            this.Text = Strings.CloseDeck;
            this.m_Marshal = marshal;

            this.m_CurrentDeckTraversalChangedDispatcher = 
                this.m_Model.Workspace.CurrentDeckTraversal.ListenAndInitialize(dispatcher,
                delegate(Property<DeckTraversalModel>.EventArgs args) {
                    this.CurrentDeckTraversal = args.New;
                });
        }

        protected override void Dispose(bool disposing) {
            if (disposing) {
                this.m_CurrentDeckTraversalChangedDispatcher.Dispose();
            }
            base.Dispose(disposing);
        }

        protected override void OnClick(EventArgs e) {
            base.OnClick(e);
            if (!this.Enabled) return;

            using (Synchronizer.Lock(this.m_Model.Workspace.CurrentDeckTraversal.SyncRoot)) {
                if (this.m_Model.Workspace.CurrentDeckTraversal.Value != null) {
                    using (Synchronizer.Lock(this.m_Model.Workspace.CurrentDeckTraversal.Value.Deck.SyncRoot)) {
                        if (this.m_Model.Workspace.CurrentDeckTraversal.Value.Deck.Dirty) {
                            SaveOnCloseDeckDialog sd = new SaveOnCloseDeckDialog(this.m_Model, this.m_Marshal, e);
                            sd.ShowDialog();
                            if (sd.Cancel)
                                return;
                        }
                    }
                }
            }

            using (this.m_Model.Workspace.Lock()) {
                int index = this.m_Model.Workspace.DeckTraversals.IndexOf(this.m_CurrentDeckTraversal);
                if (index >= 0) {
                    this.m_Model.Workspace.CurrentDeckTraversal.Value =
                        (index + 1 < this.m_Model.Workspace.DeckTraversals.Count)
                        ? this.m_Model.Workspace.DeckTraversals[index + 1]
                        : (index > 0)
                            ? this.m_Model.Workspace.DeckTraversals[index - 1]
                            : null;
                    this.m_Model.Workspace.DeckTraversals.Remove(this.m_CurrentDeckTraversal);

                    using (Synchronizer.Lock(this.m_Model.Participant.SyncRoot)) {
                        using (Synchronizer.Lock(this.m_Model.Participant.Role.SyncRoot)) {
                            if (this.m_Model.Participant.Role is Model.Network.InstructorModel)
                                ((Model.Network.InstructorModel)(this.m_Model.Participant.Role)).CurrentDeckTraversal = this.m_Model.Workspace.CurrentDeckTraversal;
                        }
                    }

                    using( Synchronizer.Lock( this.m_Model.Workspace.CurrentPresentation.Value.SyncRoot ) ) {
                        this.m_CurrentDeckTraversal.Deck.current_subs = false;
                        this.m_CurrentDeckTraversal.Deck.current_poll = false;
                    }
                }
            }
        }

        protected DeckTraversalModel CurrentDeckTraversal {
            get { return this.m_CurrentDeckTraversal; }
            set {
                this.m_CurrentDeckTraversal = value;

                this.Enabled = (this.m_CurrentDeckTraversal != null
                    && (this.m_CurrentDeckTraversal.Deck.Disposition & DeckDisposition.Remote) == 0);
            }
        }
    }
}
