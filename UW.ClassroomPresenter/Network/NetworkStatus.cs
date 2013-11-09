using System;
using System.Collections.Generic;
using System.Text;

namespace UW.ClassroomPresenter.Network {

    /// <summary>
    /// A class used for reporting network status from network layer to the UI.
    /// </summary>
    public class NetworkStatus {
        public ConnectionStatus ConnectionStatus;
        public ConnectionProtocolType ProtocolType;

        /// <summary>
        /// Used only for RTP
        /// </summary>
        public string ClassroomName;
        
        /// <summary>
        /// Only used for ProtocolType.TCP
        /// </summary>
        public TCPRole TCPRole;

        /// <summary>
        /// Only used for TCPRole.Server
        /// </summary>
        public int ClientCount;

        public NetworkStatus()
            : this(ConnectionStatus.Unknown, ConnectionProtocolType.Unknown, TCPRole.None, 0) { }

        public NetworkStatus(ConnectionStatus connectionStatus, ConnectionProtocolType protocolType, TCPRole tcpRole, int clientCount)
            : this(connectionStatus, protocolType, tcpRole, clientCount, "") { }

        public NetworkStatus(ConnectionStatus connectionStatus, ConnectionProtocolType protocolType, TCPRole tcpRole, int clientCount, string classroomName) {
            this.ConnectionStatus = connectionStatus;
            this.ProtocolType = protocolType;
            this.TCPRole = tcpRole;
            this.ClientCount = clientCount;
            this.ClassroomName = classroomName;
        }

        public static NetworkStatus DisconnectedNetworkStatus = new NetworkStatus(ConnectionStatus.Disconnected, ConnectionProtocolType.Unknown, TCPRole.None, 0);

        public override string ToString() {
            if (this.ConnectionStatus == ConnectionStatus.Unknown) {
                return Strings.UnknownConnectionStatus;
            }
            else if (this.ConnectionStatus == ConnectionStatus.Connected) {
                if (ProtocolType == ConnectionProtocolType.RTP) {
                    return Strings.RTPConnected + ClassroomName;
                }
                else if (ProtocolType == ConnectionProtocolType.TCP) {
                    if (TCPRole == TCPRole.Client) {
                        return Strings.TCPClientConnected;
                    }
                    else if (TCPRole == TCPRole.Server) {
                        return Strings.TCPServerConnected + "; "+ this.ClientCount.ToString() +" "+ Strings.Clients;
                    }
                }
                else if (ProtocolType == ConnectionProtocolType.CXPCapability) { 
                    return Strings.CXPCapabilityConnected;
                }
            }
            else if (this.ConnectionStatus == ConnectionStatus.TryingToConnect) {
                if ((ProtocolType == ConnectionProtocolType.TCP) &&
                    (TCPRole == TCPRole.Client))
                    return Strings.TCPClientAttemptingConnect;
            }
            
            return Strings.Disconnected;
        }

        public NetworkStatus Clone() {
            return new NetworkStatus(this.ConnectionStatus, this.ProtocolType, this.TCPRole, this.ClientCount, this.ClassroomName);
        }

        public bool StatusChanged(NetworkStatus other) {
            if ((this.ConnectionStatus != other.ConnectionStatus) ||
                (this.ProtocolType != other.ProtocolType) ||
                (this.TCPRole != other.TCPRole) ||
                (this.ClientCount != other.ClientCount) ||
                (this.ClassroomName != other.ClassroomName))
                return true;

            return false;
        }
    }

    public enum ConnectionStatus { 
        Connected,
        TryingToConnect,
        Disconnected,
        Unknown
    }

    public enum ConnectionProtocolType { 
        TCP,
        RTP,
        CXPCapability,
        Unknown,
        Other
    }

    public enum TCPRole {
        Client,
        Server,
        None
    }

}
