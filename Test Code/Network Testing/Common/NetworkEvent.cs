// $Id: NetworkEvent.cs 774 2005-09-21 20:22:33Z pediddle $

using System;
using UW.ClassroomPresenter.Network.Messages;
using UW.ClassroomPresenter.Network.Chunking;
using UW.ClassroomPresenter.Network.RTP;

namespace UW.ClassroomPresenter.Test.Network.Common {
    /// <summary>
    /// Summary description for NetworkEvent.
    /// </summary>
    [Serializable]
    public abstract class NetworkEvent {
        
        public readonly long timeIndex;
        public readonly string source;
        public object extension;
        
        public NetworkEvent(long timeIndex, string source) {
            this.timeIndex = timeIndex;
            this.source = source;
        }
    }

    [Serializable]
    public class NetworkChunkEvent : NetworkEvent {
        public readonly Chunk chunk;

        public NetworkChunkEvent(Chunk chunk, long timeIndex, string source) : base(timeIndex, source) {
            this.chunk = chunk;
        }
    }

    [Serializable]
    public class NetworkMessageEvent : NetworkEvent {
        public readonly Message message;

        public NetworkMessageEvent(Message msg, long timeIndex, string source) : base(timeIndex, source) {
            this.message = msg;
        }
    }

    [Serializable]
    public class NetworkNACKMessageEvent : NetworkEvent {
        public readonly RtpNackMessage message;

        public NetworkNACKMessageEvent(RtpNackMessage msg, long timeIndex, string source) : base(timeIndex, source) {
            this.message = msg;
        }
    }
}
