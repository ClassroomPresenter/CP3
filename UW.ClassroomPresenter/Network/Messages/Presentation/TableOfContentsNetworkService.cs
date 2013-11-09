// $Id: TableOfContentsNetworkService.cs 1611 2008-05-29 02:03:36Z cmprince $

using System;
using System.Diagnostics;
using System.Threading;

using UW.ClassroomPresenter.Model;
using UW.ClassroomPresenter.Model.Presentation;
using UW.ClassroomPresenter.Network.Groups;

namespace UW.ClassroomPresenter.Network.Messages.Presentation {
    public class TableOfContentsNetworkService : INetworkService, IDisposable {
        private readonly SendingQueue m_Sender;
        private readonly PresentationModel m_Presentation;
        private readonly DeckModel m_Deck;

        private readonly EntriesCollectionHelper m_EntriesCollectionHelper;

        private bool m_Disposed;

        public TableOfContentsNetworkService(SendingQueue sender, PresentationModel presentation, DeckModel deck) {
            this.m_Sender = sender;
            this.m_Presentation = presentation;
            this.m_Deck = deck;

            this.m_EntriesCollectionHelper = new EntriesCollectionHelper(this);
        }

        #region IDisposable Members

        ~TableOfContentsNetworkService() {
            this.Dispose(false);
        }

        public void Dispose() {
            this.Dispose(true);
            System.GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing) {
            if(this.m_Disposed) return;
            if(disposing) {
                this.m_EntriesCollectionHelper.Dispose();
            }
            this.m_Disposed = true;
        }

        #endregion


        private class EntriesCollectionHelper : PropertyCollectionHelper, INetworkService {
            private readonly TableOfContentsNetworkService m_Service;

            public EntriesCollectionHelper(TableOfContentsNetworkService service) : base(service.m_Sender, service.m_Deck.TableOfContents, "Entries") {
                this.m_Service = service;

                base.Initialize();
            }

            protected override void Dispose(bool disposing) {
                // Currently, none of the tags returned by SetUpMember need to be disposed.
                base.Dispose(disposing);
            }

            protected override object SetUpMember(int index, object member) {
                TableOfContentsModel.Entry entry = ((TableOfContentsModel.Entry) member);

                Group receivers = Group.AllParticipant;
                if( (this.m_Service.m_Deck.Disposition & (DeckDisposition.StudentSubmission | DeckDisposition.QuickPoll)) != 0 ) {
                    receivers = Group.Submissions;
                }

                this.SendEntryAdded(entry, receivers);

                // Return non-null so TearDownMember can know whether we succeeded.
                return entry;
            }

            private void SendEntryAdded(TableOfContentsModel.Entry entry, Group receivers) {
                Message message, deck, slide;
                message = new PresentationInformationMessage(this.m_Service.m_Presentation);
                message.Group = receivers;
                message.InsertChild(deck = new DeckInformationMessage(this.m_Service.m_Deck));
                deck.InsertChild(slide = new SlideInformationMessage(entry.Slide));
                slide.InsertChild(new TableOfContentsEntryMessage(entry));
                this.m_Service.m_Sender.Send(message);
            }

            protected override void TearDownMember(int index, object member, object tag) {
                if(tag == null) return;

                TableOfContentsModel.Entry entry = ((TableOfContentsModel.Entry) member);

                Message message, deck, slide;
                message = new PresentationInformationMessage(this.m_Service.m_Presentation);
                message.InsertChild(deck = new DeckInformationMessage(this.m_Service.m_Deck));
                deck.InsertChild(slide = new SlideInformationMessage(entry.Slide));
                slide.InsertChild(new TableOfContentsEntryRemovedMessage(entry));

                this.m_Service.m_Sender.Send(message);
            }

            #region INetworkService Members

            public void ForceUpdate(Group receivers) {
                // Lock the DeckModel first, since that's what all the other code does.
                using (Synchronizer.Lock(this.m_Service.m_Deck.SyncRoot)) {
                    using (Synchronizer.Lock(this.Owner.SyncRoot)) {
                        foreach (TableOfContentsModel.Entry entry in this.Tags) {
                            if (entry != null) {
                                this.SendEntryAdded(entry, receivers);
                            }
                        }
                    }
                }
            }

            #endregion
        }

        #region INetworkService Members

        public void ForceUpdate(Group receivers) {
            this.m_EntriesCollectionHelper.ForceUpdate(receivers);
        }

        #endregion
    }
}