// $Id: NetworkModel.cs 1363 2007-05-15 18:27:43Z fred $

using System;
using UW.ClassroomPresenter.Network.Messages;
using System.Diagnostics;
using UW.ClassroomPresenter.Network;
using System.Collections.Generic;
using System.Collections;

namespace UW.ClassroomPresenter.Model.Network {
    [Serializable]
    public class NetworkModel : PropertyPublisher {
        private readonly ProtocolCollection m_Protocols;

        // Published properties:
        private ParticipantModel m_Association;
        private NetworkStatus m_NetworkStatus;
        private Hashtable m_NetworkStatusProviders;

        public NetworkModel() {
            this.m_Protocols = new ProtocolCollection(this, "Protocols");
            m_NetworkStatus = NetworkStatus.DisconnectedNetworkStatus;
            m_NetworkStatusProviders = new Hashtable();
        }

        /// <summary>
        /// The current networking code should register and unregister when it starts and disposes.  
        /// The intent is that this will allow UI elements to pick up global network state 
        /// from a corresponding property aggregated and published here in NetworkModel. We normally should have only one 
        /// PropertyPublisher registered at a time, but we will keep track of multiple, and 
        /// report an indeterminate state if more than one is registered.
        /// </summary>
        /// <param name="publisher">A PropertyPublisher with a NetworkStatus property</param>
        /// <param name="register">true to register, false to unregister</param>
        public void RegisterNetworkStatusProvider(PropertyPublisher publisher, bool register, NetworkStatus initialStatus) {
            using (Synchronizer.Lock(this.m_NetworkStatusProviders)) {
                if (register) {
                    if (m_NetworkStatusProviders.ContainsKey(publisher))
                        throw (new ArgumentException("A Network Status Provider can only be registered once."));
                    publisher.Changed["NetworkStatus"].Add(new PropertyEventHandler(OnNetworkStatusChanged));
                    NetworkStatus newStatus = initialStatus.Clone();
                    m_NetworkStatusProviders.Add(publisher, newStatus);
                    if (m_NetworkStatusProviders.Count > 1) {
                        //Multiple providers --> unknown global status
                        newStatus = new NetworkStatus();
                    }
                    if (m_NetworkStatus.StatusChanged(newStatus)) {
                        using (Synchronizer.Lock(this.SyncRoot)) {
                            this.SetPublishedProperty("NetworkStatus", ref m_NetworkStatus, newStatus);
                        }
                    }
                }
                else {
                    if (!m_NetworkStatusProviders.ContainsKey(publisher))
                        throw (new ArgumentException("A Network Status Provider can't be unregistered until it has been registered."));
                    m_NetworkStatusProviders.Remove(publisher);
                    publisher.Changed["NetworkStatus"].Remove(new PropertyEventHandler(OnNetworkStatusChanged));
                    if (m_NetworkStatusProviders.Count == 1) {
                        foreach (NetworkStatus ns in m_NetworkStatusProviders.Values) {
                            if (m_NetworkStatus.StatusChanged(ns)) {
                                using (Synchronizer.Lock(this.SyncRoot)) {
                                    this.SetPublishedProperty("NetworkStatus", ref m_NetworkStatus, ns);
                                }
                            }
                            break;
                        }
                    }
                    if (m_NetworkStatusProviders.Count == 0) { 
                        if (m_NetworkStatus.StatusChanged(NetworkStatus.DisconnectedNetworkStatus)) {
                            using (Synchronizer.Lock(this.SyncRoot)) {
                                this.SetPublishedProperty("NetworkStatus", ref m_NetworkStatus, NetworkStatus.DisconnectedNetworkStatus);
                            }
                        }
                    }
                }
            }
        }

        private void OnNetworkStatusChanged(object sender, PropertyEventArgs args) {
            using (Synchronizer.Lock(this.m_NetworkStatusProviders)) {
                NetworkStatus newStatus = ((NetworkStatus)((PropertyChangeEventArgs)args).NewValue).Clone();
                m_NetworkStatusProviders[sender] = newStatus;
                if (m_NetworkStatusProviders.Count != 1) {
                    newStatus = new NetworkStatus();
                }
                if (m_NetworkStatus.StatusChanged(newStatus)) {
                    using (Synchronizer.Lock(this.SyncRoot)) {
                        this.SetPublishedProperty("NetworkStatus", ref this.m_NetworkStatus, newStatus);
                    }
                }
            }
        }

        [Published]
        public NetworkStatus NetworkStatus { 
            get { return this.GetPublishedProperty("NetworkStatus", ref this.m_NetworkStatus); }
        }

        [Published] public ParticipantModel Association {
            get { return this.GetPublishedProperty("Association", ref this.m_Association); }
            set { this.SetPublishedProperty("Association", ref this.m_Association, value); }
        }


        #region Protocols

        [Published] public ProtocolCollection Protocols {
            get { return this.m_Protocols; }
        }

        public class ProtocolCollection : PropertyCollectionBase {
            internal ProtocolCollection(PropertyPublisher owner, string property) : base(owner, property) {
            }

            public ProtocolModel this[int index] {
                get { return ((ProtocolModel) List[index]); }
                set { List[index] = value; }
            }

            public int Add(ProtocolModel value) {
                return List.Add(value);
            }

            public int IndexOf(ProtocolModel value) {
                return List.IndexOf(value);
            }

            public void Insert(int index, ProtocolModel value) {
                List.Insert(index, value);
            }

            public void Remove(ProtocolModel value) {
                List.Remove(value);
            }

            public bool Contains(ProtocolModel value) {
                return List.Contains(value);
            }

            protected override void OnValidate(Object value) {
                if(!typeof(ProtocolModel).IsInstanceOfType(value))
                    throw new ArgumentException("Value must be of type ProtocolModel.", "value");
            }
        }

        #endregion Protocols



    }
}
