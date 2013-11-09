// $Id: SendingQueue.cs 940 2006-04-10 19:11:11Z pediddle $

using System;
using System.Collections;
using System.Diagnostics;
using System.Threading;
using System.Windows.Forms;

using UW.ClassroomPresenter.Model;
using UW.ClassroomPresenter.Network.Chunking;
using UW.ClassroomPresenter.Model.Network;

namespace UW.ClassroomPresenter.Network.Messages {
    public abstract class SendingQueue : ThreadEventQueue {
        public void Send(Message message) {
            this.Send(message, MessagePriority.Default);
        }

        public abstract void Send(Message message, MessagePriority priority);

        public abstract ClassroomModel Classroom { get; }
    }
}
