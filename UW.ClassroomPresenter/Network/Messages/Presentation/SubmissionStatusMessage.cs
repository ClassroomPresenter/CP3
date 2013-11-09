using System;
using System.Collections.Generic;
using System.Text;
using UW.ClassroomPresenter.Model.Network;

namespace UW.ClassroomPresenter.Network.Messages.Presentation {
    [Serializable]
    public class SubmissionStatusMessage : Message {
        private SubmissionStatusModel sender_;

        /// <summary>
        /// constructor for creating a confirmation message from the instructor.
        /// Here Id is the id of the student this message is for.
        /// </summary>
        /// <param name="status"></param>
        /// <param name="id"></param>
        public SubmissionStatusMessage(SubmissionStatusModel submission_model)
            : base(Guid.NewGuid()) {
            ///the model that send this.
            sender_ = submission_model;
        }

        protected override bool UpdateTarget(ReceiveContext context) {

            if (SubmissionStatusModel.GetInstance().Role == "student"
                && sender_.Role == "instructor") {
                Guid id1;
                Guid id2;
                using(Synchronizer.Lock(SubmissionStatusModel.GetInstance().SyncRoot)){
                    id1 = SubmissionStatusModel.GetInstance().Id;
                }
                using (Synchronizer.Lock(sender_.SyncRoot)) {
                    id2 = sender_.Id;
                }
                if (id1 == id2) {
                    using (Synchronizer.Lock(SubmissionStatusModel.GetInstance().SyncRoot)) {
                        SubmissionStatusModel.GetInstance().SubmissionStatus = SubmissionStatusModel.Status.Received;
                    }
                }
            } else if (SubmissionStatusModel.GetInstance().Role == "instructor" 
                && sender_.Role == "student") {
                Guid id1;
                using (Synchronizer.Lock(sender_.SyncRoot)) {
                    id1 = sender_.Id;
                }
                using (Synchronizer.Lock(SubmissionStatusModel.GetInstance().SyncRoot)) {
                    ///we need to toggle the Id in order for the listener to recognize the event.
                    SubmissionStatusModel.GetInstance().Id = Guid.Empty;
                    SubmissionStatusModel.GetInstance().Id = id1;
                }
            }
            return true;
        }

        #region IGenericSerializable

        public override SerializedPacket Serialize() {
            SerializedPacket p = base.Serialize();
            p.Add( (this.sender_ != null) ? 
                this.sender_.Serialize() : SerializedPacket.NullPacket( PacketTypes.SubmissionStatusModelId ) );
            return p;
        }

        public SubmissionStatusMessage( Message parent, SerializedPacket p ) : base( parent, p ) {
            this.sender_ = (!SerializedPacket.IsNullPacket( p.PeekNextPart() )) ?
                new SubmissionStatusModel( p.PeekNextPart() ) : null; p.GetNextPart();
        }

        public override int GetClassId() {
            return PacketTypes.SubmissionStatusMessageId;
        }

        #endregion
    }
}
