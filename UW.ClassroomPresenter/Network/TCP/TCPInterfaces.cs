using System;
using System.Collections.Generic;
using System.Text;
using UW.ClassroomPresenter.Network.Chunking;
using UW.ClassroomPresenter.Network.Groups;
using UW.ClassroomPresenter.Model;
using UW.ClassroomPresenter.Network.Messages;

namespace UW.ClassroomPresenter.Network.TCP {
    public interface ITCPSender {
        void Send(Chunk chunk, Group receivers, MessageTags priority);
    }
    public interface ITCPReceiver {
        object Read();
    }
}
