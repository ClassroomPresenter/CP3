// $Id: ReceiveContext.cs 677 2005-08-31 23:49:22Z pediddle $

using System;
using System.Collections;
using System.Diagnostics;

using UW.ClassroomPresenter.Model;
using UW.ClassroomPresenter.Model.Network;

namespace UW.ClassroomPresenter.Network.Messages {

    /// <summary>
    /// The context in which a received message is to be processed.
    /// </summary>
    /// <remarks>
    /// An instance of <see cref="ReceiveMessage"/> is passed to <see cref="Message.UpdateTarget"/>
    /// to enable the message to access the <see cref="PresenterModel">model</see>,
    /// <see cref="ClassroomModel">classroom</see> from which the message was received,
    /// and the <see cref="ParticipantModel">participant</see> who sent the message.
    /// </remarks>
    public class ReceiveContext {
        /// <summary>
        /// The application's complete model.
        /// </summary>
        public readonly PresenterModel Model;

        /// <summary>
        /// The classroom from which the message was received.
        /// </summary>
        public readonly ClassroomModel Classroom;

        /// <summary>
        /// The participant who sent the message.
        /// </summary>
        public readonly ParticipantModel Participant;

        internal ReceiveContext(PresenterModel model, ClassroomModel classroom, ParticipantModel participant) {
            this.Model = model;
            this.Classroom = classroom;
            this.Participant = participant;
        }

        private readonly ArrayList m_Persistent = new ArrayList();

        public void Persist(object value) {
            this.m_Persistent.Add(value);
        }

        public void Unpersist(object value) {
            this.m_Persistent.Remove(value);
        }
    }
}
