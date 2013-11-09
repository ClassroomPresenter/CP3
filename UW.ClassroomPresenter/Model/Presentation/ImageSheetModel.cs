// $Id: ImageSheetModel.cs 1767 2008-09-14 22:33:39Z anderson $

using System;
using System.Drawing;
using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Threading;


namespace UW.ClassroomPresenter.Model.Presentation {
    [Serializable]
    ///represents data for images loaded by student or instructor
    public class ImageSheetModel : EditableSheetModel {

        private readonly DeckModel m_Deck;
        public DeckModel Deck {
            [DebuggerStepThrough]
            get { return this.m_Deck; }
        }

        private readonly ByteArray m_MD5;
        public ByteArray MD5 {
            [DebuggerStepThrough]
            get { return this.m_MD5; }
        }

        private bool visible_;
        public bool Visible {
            get { return visible_; }
            set { visible_ = value; }
        }

        public ImageSheetModel(DeckModel deck, Guid id, SheetDisposition disp, Rectangle bounds, ByteArray md5, int height) : base(id, disp, bounds, false, height) {
            this.m_MD5 = md5;
            this.m_Deck = deck;
            this.visible_ = true;
        }


        #region ImageIt Images (Julia)
        /// <summary>
        /// the image we have.
        /// </summary>
        private Image image_;

        public ImageSheetModel (Guid id, Image image, bool is_editable, Point p, Size s, int height): this (id, image, SheetDisposition.All, is_editable, p, s, height){
        }
        
        public ImageSheetModel(Guid id, Image image, SheetDisposition disp, bool is_editable, Point p, Size s, int height)
            : base(id, disp, new Rectangle(p, s), is_editable, height) {
            image_ = image;
        }

        /// <summary>
        /// FIXME: image should be published, or
        /// Image should only be set once
        /// </summary>
        [Published]
        public Image Image {
            get { return image_; }
            set { image_ = value; }
        }

        public override object Clone() {
            Rectangle bounds;
            using (Synchronizer.Lock(this.SyncRoot)) {
                bounds = this.Bounds;
            }
            ImageSheetModel s = new ImageSheetModel(Guid.NewGuid(), this.image_, this.Disposition, is_editable_, bounds.Location, bounds.Size, this.Height);

            // Todo: it's not clear why the constructors don't handle visability 
            s.Visible = this.Visible;
            return s;
        }

        public override object CloneToRemote() {
            Rectangle bounds;
            using (Synchronizer.Lock(this.SyncRoot)) {
                bounds = this.Bounds;
            }
            ImageSheetModel s = new ImageSheetModel(Guid.NewGuid(), this.image_, this.Disposition | SheetDisposition.Remote, is_editable_, bounds.Location, bounds.Size, this.Height);

            // Todo: it's not clear why the constructors don't handle visability 
            s.Visible = this.Visible;
            s.is_editable_ = false;

            return s;
        }

        #endregion

        
    }
}
