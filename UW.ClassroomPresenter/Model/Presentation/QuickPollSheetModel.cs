using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Collections.Generic;
using System.Text;

namespace UW.ClassroomPresenter.Model.Presentation {
    [Serializable]
    public class QuickPollSheetModel : SheetModel 
    {
        #region Private Members

        /// <summary>
        /// The quickpoll model whos results this sheet displays
        /// </summary>
        private QuickPollModel m_QuickPoll;

        private Guid m_QuickPollId = Guid.Empty;

        #endregion

        public QuickPollModel QuickPoll {
            get { return this.m_QuickPoll; }
        }

        public Guid QuickPollId {
            get { return this.m_QuickPollId; }
            set { this.m_QuickPollId = value; }
        }

        #region Constructors

        /// <summary>
        /// Constructs a default quickpollsheetmodel which has been scaled to fit the current transform
        /// </summary>
        /// <param name="id"></param>
        /// <param name="model"></param>
        public QuickPollSheetModel( Guid id, QuickPollModel model ): base( id, 0 ) {
            this.m_QuickPoll = model;
        }

        /// <summary>
        /// Clone a QuickPollSheetModel
        /// </summary>
        /// <returns></returns>
        public QuickPollSheetModel Clone() {
            Rectangle bounds;
            using(Synchronizer.Lock(this.SyncRoot)){
                bounds = this.Bounds;
            }
            return new QuickPollSheetModel( Guid.NewGuid(), this.m_QuickPoll );
        }

        #endregion


    }
}
