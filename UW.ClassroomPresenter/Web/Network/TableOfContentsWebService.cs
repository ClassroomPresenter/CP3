#if WEBSERVER
using System;
using System.Collections.Generic;
using System.Text;

using UW.ClassroomPresenter.Model;
using UW.ClassroomPresenter.Model.Presentation;
using UW.ClassroomPresenter.Network.Messages;

namespace UW.ClassroomPresenter.Web.Network {
    /// <summary>
    /// Model listener to act when there are changes to the table of contents
    /// </summary>
    public class TableOfContentsWebService : IDisposable {
        #region Members

        /// <summary>
        /// The queue to use for events
        /// </summary>
        private readonly SendingQueue m_Sender;
        /// <summary>
        /// The presentation model to track
        /// </summary>
        private readonly PresentationModel m_Presentation;
        /// <summary>
        /// The deck model to track
        /// </summary>
        private readonly DeckModel m_Deck;

        /// <summary>
        /// Helper class for keeping track of changes to entries
        /// </summary>
        private readonly EntriesCollectionHelper m_EntriesCollectionHelper;

        /// <summary>
        /// Keeps track of whether the object is disposed
        /// </summary>
        private bool m_Disposed;

        #endregion

        #region Constructors

        /// <summary>
        /// Build this model listener
        /// </summary>
        /// <param name="sender">The queue to use to send events</param>
        /// <param name="presentation">The presentation to listen to</param>
        /// <param name="deck">The deck to listen to</param>
        public TableOfContentsWebService(SendingQueue sender, PresentationModel presentation, DeckModel deck) {
            this.m_Sender = sender;
            this.m_Presentation = presentation;
            this.m_Deck = deck;

            this.m_EntriesCollectionHelper = new EntriesCollectionHelper(this);
        }

        #endregion

        #region IDisposable Members

        /// <summary>
        /// Finalizer
        /// </summary>
        ~TableOfContentsWebService()
        {
            this.Dispose(false);
        }

        /// <summary>
        /// Public dispose method
        /// </summary>
        public void Dispose()
        {
            this.Dispose(true);
            System.GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Protected dispose method
        /// </summary>
        /// <param name="disposing">True when user initiated</param>
        protected virtual void Dispose(bool disposing)
        {
            if (this.m_Disposed) return;
            if (disposing)
            {
                this.m_EntriesCollectionHelper.Dispose();
            }
            this.m_Disposed = true;
        }

        #endregion

        #region Entries Collection

        /// <summary>
        /// Private class for keeping track of when entries are added and removed from the
        /// table of contents
        /// </summary>
        private class EntriesCollectionHelper : PropertyCollectionHelper {
            private readonly TableOfContentsWebService m_Service;

            public EntriesCollectionHelper(TableOfContentsWebService service)
                : base(service.m_Sender, service.m_Deck.TableOfContents, "Entries")
            {
                this.m_Service = service;

                base.Initialize();
            }

            protected override void Dispose(bool disposing)
            {
                // Currently, none of the tags returned by SetUpMember need to be disposed.
                base.Dispose(disposing);
            }

            protected override object SetUpMember(int index, object member)
            {
                TableOfContentsModel.Entry entry = ((TableOfContentsModel.Entry)member);

                // Return non-null so TearDownMember can know whether we succeeded.
                return entry;
            }

            protected override void TearDownMember(int index, object member, object tag)
            {
                if (tag == null) return;

                TableOfContentsModel.Entry entry = ((TableOfContentsModel.Entry)member);
            }
        }

        #endregion
    }
}
#endif