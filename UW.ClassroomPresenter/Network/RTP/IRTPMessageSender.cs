using System;
using System.Collections.Generic;
using System.Text;

namespace UW.ClassroomPresenter.Network.RTP {
    public interface IRTPMessageSender {
        void SendNack(RtpNackMessage nack);
    }
}
