using System;
using System.Drawing;
using System.Windows.Documents;

using UW.ClassroomPresenter.Decks;
using UW.ClassroomPresenter.Model.Presentation;

namespace UW.ClassroomPresenter.Network.Messages.Presentation
{
    [Serializable]
    public class XPSPageSheetMessage : SheetMessage
    {
        /// <summary>
        /// DocumentPage wrapper
        /// </summary>
        private XPSDocumentPageWrapper m_DocumentPageWrapper;
        public XPSDocumentPageWrapper DocumentPageWrapper
        {
            get { return m_DocumentPageWrapper; }
        }

        public int Width;

        public XPSPageSheetMessage(XPSPageSheetModel sheet, SheetCollection collection)
            : base(sheet, collection)
        {
            if (sheet.DocumentPageWrapper!=null)
            {
                using (Synchronizer.Lock(sheet.SyncRoot))
                {
                    this.m_DocumentPageWrapper = sheet.DocumentPageWrapper;
                    this.Height = sheet.Height;
                    this.Width = sheet.Width;
                }
            }
        }

        protected override bool UpdateTarget(ReceiveContext context)
        {
            DeckModel deck = (this.Parent != null && this.Parent.Parent != null) ? this.Parent.Parent.Target as DeckModel : null;

            XPSPageSheetModel sheet = this.Target as XPSPageSheetModel;
            if (sheet == null)
            {
                if (this.m_DocumentPageWrapper != null)
                {
                    this.Target = sheet = new XPSPageSheetModel(this.m_DocumentPageWrapper, (Guid)this.TargetId, this.Disposition | SheetDisposition.Remote, new Rectangle(0, 0, this.Width, this.Height), this.Height);
                }
            }

            base.UpdateTarget(context);

            return true;

        }
    }
}
