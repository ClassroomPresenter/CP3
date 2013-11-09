using System;
using System.Collections.Generic;
using System.Text;
using UW.ClassroomPresenter.Network;


namespace UW.ClassroomPresenter.Model.Network {
    [Serializable]
    public class SubmissionStatusModel : PropertyPublisher, IGenericSerializable {
        
        protected static SubmissionStatusModel submission_model_;
        /// <summary>
        /// If we are a student, this is the student's unique status model Id which 
        /// helps to identify whether a confirmation message is being sent to him.
        /// If we are an instructor, this is a way to keep track of which student
        /// we are sending our confirmation to.
        /// </summary>
        private Guid id_;
        private Status submission_status_;
        private string role_;

        private SubmissionStatusModel(){
            id_ = PresenterModel.ParticipantId;
        }

        public static SubmissionStatusModel GetInstance(){
            if(submission_model_ == null){
                submission_model_ = new SubmissionStatusModel();
            }
            return submission_model_;
        }

        public enum Status : int {
            /// <summary>
            /// sending but not receiving
            /// </summary>
            NotReceived = 1,
            /// <summary>
            /// we've sent and received, so we are fine.
            /// </summary>
            Received = 2,
            Failed = 3
        }



        [Published]
        public Guid Id{
            get{return this.GetPublishedProperty("Id", ref this.id_);}
            set{this.SetPublishedProperty("Id", ref this.id_, value);}
        }



        [Published]
        public Status SubmissionStatus {
            get { return this.GetPublishedProperty("SubmissionStatus", ref this.submission_status_); }
            set { this.SetPublishedProperty("SubmissionStatus", ref this.submission_status_, value); }
        }

        public string Role {
            get { return role_; }
            set { role_ = value; }
        }

        #region IGenericSerializable
        public SerializedPacket Serialize() {
            SerializedPacket p = new SerializedPacket( this.GetClassId() );
            p.Add( SerializedPacket.SerializeGuid( this.id_ ) );
            p.Add( SerializedPacket.SerializeInt( (int)this.submission_status_ ) );
            p.Add( SerializedPacket.SerializeString( this.role_ ) );
            return p;
        }

        public SubmissionStatusModel( SerializedPacket p ) {
            SerializedPacket.VerifyPacket( p, this.GetClassId() );
            this.id_ = SerializedPacket.DeserializeGuid( p.GetPart( 0 ) );
            this.submission_status_ = (Status)SerializedPacket.DeserializeInt( p.GetPart( 1 ) );
            this.role_ = SerializedPacket.DeserializeString( p.GetPart( 2 ) );
        }

        public int GetClassId() {
            return PacketTypes.SubmissionStatusModelId;
        }

        #endregion

    }
}
