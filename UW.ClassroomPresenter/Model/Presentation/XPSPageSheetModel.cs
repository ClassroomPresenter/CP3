using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Documents;

using UW.ClassroomPresenter.Decks;

namespace UW.ClassroomPresenter.Model.Presentation
{
    [Serializable]
    public class XPSPageSheetModel : EditableSheetModel
    {
        [NonSerialized]
        private DocumentPage m_DocumentPage;
        public DocumentPage DocumentPage
        {
            set { this.m_DocumentPage = value; }
            get { return this.m_DocumentPage; }
        }

        private XPSDocumentPageWrapper m_DocumentPageWrapper;

        public XPSDocumentPageWrapper DocumentPageWrapper
        {
            set { this.m_DocumentPageWrapper = value; }
            get { return this.m_DocumentPageWrapper; }
        }

        private int m_Width;
        public int Width
        {
            get { return this.m_Width; }
            set { this.m_Width = value; }
        }

        public XPSPageSheetModel(DocumentPage page, XPSDocumentPageWrapper pageWrapper, Guid id, SheetDisposition disposition, Rectangle bounds, int height)
            : base(id, disposition,bounds,false,height)
        {
            this.m_DocumentPage = page;
            this.m_DocumentPageWrapper = pageWrapper;
            this.m_Width = bounds.Width;
        }

        public XPSPageSheetModel(XPSDocumentPageWrapper pageWrapper, Guid id, SheetDisposition disposition, Rectangle bounds, int height)
            : base(id, disposition, bounds, false, height)
        {
            this.m_DocumentPage = null;
            this.m_DocumentPageWrapper = pageWrapper;
            this.m_Width = bounds.Width;
        }

        public override object Clone()
        {
            Rectangle bounds;
            using (Synchronizer.Lock(this.SyncRoot))
            {
                bounds = this.Bounds;
            }
            XPSPageSheetModel s = new XPSPageSheetModel(this.m_DocumentPageWrapper, Guid.NewGuid(), this.Disposition, bounds, this.Height);
            return s;
        }
        public override object CloneToRemote()
        {
            Rectangle bounds;
            using (Synchronizer.Lock(this.SyncRoot))
            {
                bounds = this.Bounds;
            }
            XPSPageSheetModel s = new XPSPageSheetModel(this.DocumentPageWrapper,Guid.NewGuid(),  this.Disposition | SheetDisposition.Remote, bounds, this.Height);

            s.is_editable_ = false;

            return s;
        }
    }
}
