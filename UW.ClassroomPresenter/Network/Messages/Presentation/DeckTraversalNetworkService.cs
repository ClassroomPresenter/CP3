// $Id: DeckTraversalNetworkService.cs 1880 2009-06-08 19:46:27Z jing $

using System;
using System.Collections;
using System.Diagnostics;
using System.Drawing;
using System.Threading;

using UW.ClassroomPresenter.Model;
using UW.ClassroomPresenter.Model.Presentation;
using UW.ClassroomPresenter.Network.Groups;

namespace UW.ClassroomPresenter.Network.Messages.Presentation {
    public class DeckTraversalNetworkService : INetworkService, IDisposable {
        private readonly SendingQueue m_Sender;
        private readonly EventQueue.PropertyEventDispatcher m_CurrentChangedDispatcher;

        private readonly PresentationModel m_Presentation;
        private readonly DeckTraversalModel m_DeckTraversal;

        private readonly DeckNetworkService m_DeckNetworkService;

        private bool m_Disposed;

        public DeckTraversalNetworkService(SendingQueue sender, PresentationModel presentation, DeckTraversalModel traversal) {
            this.m_Sender = sender;
            this.m_Presentation = presentation;
            this.m_DeckTraversal = traversal;

            this.m_DeckNetworkService = new DeckNetworkService(this.m_Sender, this.m_Presentation, this.m_DeckTraversal.Deck);

            this.m_CurrentChangedDispatcher = new EventQueue.PropertyEventDispatcher(this.m_Sender, new PropertyEventHandler(this.HandleCurrentChanged));
            this.m_DeckTraversal.Changed["Current"].Add(this.m_CurrentChangedDispatcher.Dispatcher);
        }

        #region IDisposable Members

        ~DeckTraversalNetworkService() {
            this.Dispose(false);
        }

        public void Dispose() {
            this.Dispose(true);
            System.GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing) {
            if(this.m_Disposed) return;
            if(disposing) {
                this.m_DeckTraversal.Changed["Current"].Remove(this.m_CurrentChangedDispatcher.Dispatcher);
                this.m_DeckNetworkService.Dispose();
            }
            this.m_Disposed = true;
        }

        #endregion

        public DeckTraversalModel DeckTraversal {
            get { return this.m_DeckTraversal; }
        }

        private void HandleCurrentChanged(object sender, PropertyEventArgs args) {
            Group receivers = Group.AllParticipant;
            if ((this.m_DeckTraversal.Deck.Disposition & (DeckDisposition.StudentSubmission | DeckDisposition.QuickPoll)) != 0) {
                receivers = Group.Submissions;
            }
            this.SendCurrentChanged(receivers);
        }

        private void SendCurrentChanged(Group receivers) {
            Message message, deck;
            message = new PresentationInformationMessage(this.m_Presentation);
            message.Group = receivers;
            message.InsertChild(deck = new DeckInformationMessage(this.m_DeckTraversal.Deck));
            deck.InsertChild(DeckTraversalMessage.ForDeckTraversal(this.m_DeckTraversal));
            
#if DEBUG
            // Add logging of slide change events
            string pres_name = "";
            using( Synchronizer.Lock( this.m_Presentation.SyncRoot ) )
                pres_name = this.m_Presentation.HumanName;

            string deck_name = "";
            using( Synchronizer.Lock( this.m_DeckTraversal.Deck.SyncRoot ) )
                deck_name = this.m_DeckTraversal.Deck.HumanName;

            int slide_index = 0;
            Guid slide_guid = Guid.Empty;
            using( Synchronizer.Lock( this.m_DeckTraversal.SyncRoot ) ) {
                if( this.m_DeckTraversal.Current != null )
                    using( Synchronizer.Lock( this.m_DeckTraversal.Current.SyncRoot ) ) {
                        slide_index = this.m_DeckTraversal.Current.IndexInParent;
                        using (Synchronizer.Lock(this.m_DeckTraversal.Current.Slide.SyncRoot)) {
                            slide_guid = this.m_DeckTraversal.Current.Slide.Id;
                        }
                    }
            }

            Debug.WriteLine( string.Format("SLIDE CHANGE ({0}): Pres -- {1}, Deck -- {2}, Slide -- {3}, Guid -- {4}", 
                System.DateTime.Now.Ticks, pres_name, deck_name, slide_index, slide_guid ) );
#endif

            message.Tags = new MessageTags();
            message.Tags.BridgePriority = MessagePriority.Higher;

            this.m_Sender.Send(message);
        }

        #region INetworkService Members

        public void ForceUpdate(Group receivers) {
            this.SendCurrentChanged(receivers);
            this.m_DeckNetworkService.ForceUpdate(receivers);
        }

        #endregion
    }
}
