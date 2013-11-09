using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using System.Net.NetworkInformation;
using System.Diagnostics;

namespace UW.ClassroomPresenter.Network.Broadcast {
    /// <summary>
    /// The message used to advertise TCP Server (instructor node).
    /// </summary>
    [Serializable]
    public class BroadcastMessage : IGenericSerializable {
        public IPEndPoint[] EndPoints;
        public string HumanName;
        public Guid SenderID;
        public string PresentationName;
        public bool ShowIP;

        public BroadcastMessage(IPEndPoint ep, String humanName, Guid senderID, String presentationName, bool showIP) {
            if ((ep.Address == IPAddress.Any) || (ep.Address == IPAddress.IPv6Any)) {
                EndPoints = getAllEndPoints(ep.Port);
            }
            else {
                EndPoints = new IPEndPoint[1];
                EndPoints[0] = ep;
            }
            HumanName = humanName;
            SenderID = senderID;
            PresentationName = presentationName;
            ShowIP = showIP;
        }

        /// <summary>
        /// Get all the local addresses which are UP and which are not loopback or tunnel adapters.
        /// </summary>
        /// <param name="port"></param>
        /// <returns></returns>
        private IPEndPoint[] getAllEndPoints(int port) { 
            NetworkInterface[] nics = NetworkInterface.GetAllNetworkInterfaces();
            List<IPEndPoint> epList = new List<IPEndPoint>();

            if (nics == null || nics.Length < 1) {
                Trace.WriteLine("No network interfaces found.", this.GetType().ToString());
                return null;
            }

            foreach (NetworkInterface adapter in nics) {              
                if (adapter.NetworkInterfaceType == NetworkInterfaceType.Loopback) {
                    continue;
                }

                if (adapter.NetworkInterfaceType == NetworkInterfaceType.Tunnel) {
                    continue;
                }

                if (adapter.OperationalStatus != OperationalStatus.Up) {
                    continue;
                }

                IPInterfaceProperties properties = adapter.GetIPProperties();
                
                UnicastIPAddressInformationCollection uniCast = properties.UnicastAddresses;
                if (uniCast != null) {
                    foreach (UnicastIPAddressInformation uni in uniCast) {                     
                        epList.Add(new IPEndPoint(uni.Address, port));
                    }
                }
            }

            return epList.ToArray();
        }

        #region IGenericSerializable

        public SerializedPacket Serialize() {
            SerializedPacket p = new SerializedPacket( this.GetClassId() );
            p.Add( SerializedPacket.SerializeBool( this.EndPoints != null ) );
            if( this.EndPoints != null ) {
                p.Add( SerializedPacket.SerializeInt( this.EndPoints.Length ) );
                foreach( IPEndPoint ep in this.EndPoints ) {
                    p.Add( SerializedPacket.SerializeIPEndPoint( ep ) );
                }
            }
            p.Add( SerializedPacket.SerializeString( this.HumanName ) );
            p.Add( SerializedPacket.SerializeGuid( this.SenderID ) );
            p.Add( SerializedPacket.SerializeString( this.PresentationName ) );
            p.Add( SerializedPacket.SerializeBool( this.ShowIP ) );
            return p;
        }

        public BroadcastMessage( SerializedPacket p ) {
            SerializedPacket.VerifyPacket( p, this.GetClassId() );
            this.EndPoints = null;
            if( SerializedPacket.DeserializeBool( p.GetNextPart() ) ) {
                this.EndPoints = new IPEndPoint[SerializedPacket.DeserializeInt( p.GetNextPart() )];
                for( int i = 0; i < this.EndPoints.Length; i++ ) {
                    this.EndPoints[i] = SerializedPacket.DeserializeIPEndPoint( p.GetNextPart() );
                }
            }
            this.HumanName = SerializedPacket.DeserializeString( p.GetNextPart() );
            this.SenderID = SerializedPacket.DeserializeGuid( p.GetNextPart() );
            this.PresentationName = SerializedPacket.DeserializeString( p.GetNextPart() );
            this.ShowIP = SerializedPacket.DeserializeBool( p.GetNextPart() );

/*
//CMPRINCE DEBUGGING
            string IPs = "";
            foreach( IPEndPoint ep in this.EndPoints ) {
                IPs += ep.Address.ToString() + " ";
            }
            System.Diagnostics.Debug.Write( "RECVD: BroadcastListener: " + 
                IPs + 
                this.HumanName + " " + 
                this.SenderID.ToString() + " " +
                this.PresentationName + 
                System.Environment.NewLine
                );
*/
        }

        public int GetClassId() {
            return PacketTypes.BroadcastMessageId;
        }

        #endregion
    }
}
