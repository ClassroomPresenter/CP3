
using System;
using System.Drawing;
using UW.ClassroomPresenter.Viewer.Text;
using System.Collections.Generic;
using System.Drawing.Drawing2D;
using UW.ClassroomPresenter.Model.Stylus;

namespace UW.ClassroomPresenter.Model.Presentation {
    [Serializable]
    ///This class is for sheets of objects that 
    ///can be edited in the UI like text and images.
    ///Objects that extend this class have an is_editable
    ///property which indicates whether users can edit the data
    ///on the sheet.
    public abstract class EditableSheetModel : SheetModel, ICloneable {


        #region Published Properties

        /// <summary>
        /// determines whether or not we are allowed to edit the text
        /// </summary>
        protected bool is_editable_;


        #endregion

        #region Constructors
        /// <summary>
        /// constructs a default textsheet at the origin, which is by default editable
        /// </summary>
        /// <param name="id"></param>
        /// <param name="bounds"></param>
        public EditableSheetModel(Guid id, bool editable, int height)
            : base(id, height) {
            is_editable_ = editable;
        }

        public EditableSheetModel(Guid id, SheetDisposition disposition, Rectangle bounds, bool editable, int height) : base(id, disposition, bounds, height) {
            is_editable_ = editable;
        } 
        #endregion

        #region Interfaces
        // Subclasses must support cloning
        public abstract object Clone();

        /// <summary>
        /// Create a cloned object with disposition remote
        /// </summary>
        /// <returns></returns>
        public abstract object CloneToRemote();

        #endregion


        #region Publishing
        /// <summary>
        /// sets whether this sheet is editable or not
        /// This is not published because the .IsEditable will
        /// never affect the UI, and this makes it cleaner to access
        /// </summary>
        public bool IsEditable {
            get { return is_editable_; }
            set { is_editable_ = value; }
        }

        #endregion
    }
}

