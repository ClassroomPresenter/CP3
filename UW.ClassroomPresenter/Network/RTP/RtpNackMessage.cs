// $Id: RtpNackMessage.cs 860 2006-02-04 06:07:23Z pediddle $

using System;

using UW.ClassroomPresenter.Network.Messages.Network;
using UW.ClassroomPresenter.Network.Chunking;

namespace UW.ClassroomPresenter.Network.RTP {
    [Serializable]
    public class RtpNackMessage {
        public readonly uint SenderSSRC;
        public readonly DisjointSet MissingSequences;

        public RtpNackMessage(uint senderSSRC, DisjointSet missingSequences) {
            this.SenderSSRC = senderSSRC;
            this.MissingSequences = missingSequences;
        }
    }
}
