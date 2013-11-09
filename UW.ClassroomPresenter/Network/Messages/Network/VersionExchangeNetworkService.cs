using System;
using System.Collections.Generic;
using System.Text;
using UW.ClassroomPresenter.Model.Network;
using UW.ClassroomPresenter.Model;
using UW.ClassroomPresenter.Network.Groups;
using System.Diagnostics;

namespace UW.ClassroomPresenter.Network.Messages.Network {
    /// <summary>
    /// Respond to changes in the VersionExchange Collection publised by the VersionExchangeModel by 
    /// sending the request and response messages.
    /// </summary>
    class VersionExchangeNetworkService : INetworkService{
        private readonly SendingQueue m_Sender;
        private readonly VersionExchangeModel m_VersionExchangeModel;
        private readonly VersionExchangeCollectionHelper m_VersionExchangeCollectionHelper;

        public VersionExchangeNetworkService(SendingQueue sender) {
            m_Sender = sender;
            m_VersionExchangeModel = PresenterModel.TheInstance.VersionExchange;
            m_VersionExchangeCollectionHelper = new VersionExchangeCollectionHelper(this, PresenterModel.TheInstance.VersionExchange);
        }

        private class VersionExchangeCollectionHelper : PropertyCollectionHelper {
            private readonly VersionExchangeNetworkService m_Service;
            private readonly VersionExchangeModel m_VeModel;
            public VersionExchangeCollectionHelper(VersionExchangeNetworkService service, VersionExchangeModel ve_model)
                : base(service.m_Sender, ve_model, "VersionExchanges") {
                this.m_Service = service;
                this.m_VeModel = ve_model;
                base.Initialize();
            }

            protected override void Dispose(bool disposing) {
                base.Dispose(disposing);
            }

            //New VersionExchange added to the collection:
            protected override object SetUpMember(int index, object member) {
                VersionExchange ve = (VersionExchange)member;
                
                //Set up an event handler to send outbound responses in all cases:
                using (Synchronizer.Lock(ve.SyncRoot)) {
                    ve.Changed["SendVersionResponse"].Add(new PropertyEventHandler(SendVersionResponseHandler));
                    if (ve.SendVersionResponse != null) {
                        Trace.WriteLine("VersionExchangeCollection.SetUpMember: VersionResponseMessage already set before event handler added: Sending response now.");
                        SendVersionResponse(ve, ve.SendVersionResponse);
                    }
                }

                //Send a request only if the VersionExchange is configured for it; Instructor nodes initiate the exchange.
                if (ve.SendRequest) {
                    VersionRequestMessage vrm = new VersionRequestMessage();
                    vrm.Group = new SingletonGroup(ve.RemoteParticipant);
                    vrm.Tags = new MessageTags();
                    vrm.Tags.BridgePriority = MessagePriority.Lowest;
                    m_Service.m_Sender.Send(vrm);
                    Trace.WriteLine("Sent VersionRequestMessage to Participant ID: " + ve.RemoteParticipant.Guid.ToString());
                }
                return null; //No tag needed
            }

            protected override void TearDownMember(int index, object member, object tag) {
                VersionExchange ve = (VersionExchange)member;
                ve.Changed["SendVersionResponse"].Remove(new PropertyEventHandler(SendVersionResponseHandler));
            }

            //Send a VersionResponesMessage
            private void SendVersionResponseHandler(object sender, PropertyEventArgs args) {
                VersionExchange ve = (VersionExchange)sender;
                PropertyChangeEventArgs pcea = (PropertyChangeEventArgs)args;
                if (pcea.NewValue != null) {
                    VersionResponseMessage vrm = (VersionResponseMessage)(pcea.NewValue);
                    SendVersionResponse(ve, vrm);
                }
            }

            private void SendVersionResponse(VersionExchange ve, VersionResponseMessage vrm) {
                vrm.Group = new SingletonGroup(ve.RemoteParticipant);
                vrm.Tags = new MessageTags();
                vrm.Tags.BridgePriority = MessagePriority.Lowest;
                m_Service.m_Sender.Send(vrm);
                Trace.WriteLine("Sent VersionResponseMessage to Participant ID: " + ve.RemoteParticipant.Guid.ToString());
            }

        }

        #region INetworkService Members

        public void ForceUpdate(UW.ClassroomPresenter.Network.Groups.Group receivers) {
            //Nothing
        }

        #endregion
    }
}
