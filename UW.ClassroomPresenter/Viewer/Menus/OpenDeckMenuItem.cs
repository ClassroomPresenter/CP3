// $Id: OpenDeckMenuItem.cs 1598 2008-04-30 00:49:50Z lining $

using System;
using System.Threading;
using System.Windows.Forms;

using UW.ClassroomPresenter.Decks;

using UW.ClassroomPresenter.Model;
using UW.ClassroomPresenter.Model.Presentation;

namespace UW.ClassroomPresenter.Viewer.Menus {
    public class OpenDeckMenuItem : MenuItem {
        private readonly PresenterModel m_Model;
        private readonly OpenDeckDialog m_OpenDeckDialog;

        public OpenDeckMenuItem(PresenterModel model, DeckMarshalService marshal) {
            this.m_Model = model;
            this.m_OpenDeckDialog = new OpenDeckDialog(model, marshal);

            this.Text = Strings.OpenDeck;
            this.Shortcut = Shortcut.CtrlO;
            this.ShowShortcut = true;
        }

        protected override void OnClick(EventArgs e) {
            base.OnClick(e);
            this.m_OpenDeckDialog.OpenDeck((IWin32Window)null);
        }
    }
}
