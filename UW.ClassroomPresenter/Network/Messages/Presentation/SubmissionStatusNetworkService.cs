using System;
using System.Collections.Generic;
using System.Text;
using UW.ClassroomPresenter.Model.Network;
using UW.ClassroomPresenter.Model;
using UW.ClassroomPresenter.Network.Groups;

namespace UW.ClassroomPresenter.Network.Messages.Presentation {
    [Serializable]
    public class SubmissionStatusNetworkService :INetworkService, IDisposable {
        private SubmissionStatusModel submission_status_model_;

        private SendingQueue sender_;

        public SubmissionStatusNetworkService(SendingQueue sender, RoleModel role) {
            submission_status_model_ = SubmissionStatusModel.GetInstance();

            if (role is StudentModel) {
                submission_status_model_.Role = "student";
                submission_status_model_.Changed["SubmissionStatus"].Add(new PropertyEventHandler(this.StudentSendStatus));
            } else if(role is InstructorModel){
                submission_status_model_.Role = "instructor";
                submission_status_model_.Changed["Id"].Add(new PropertyEventHandler(this.InstructorSendStatus));
            }
            
            sender_ = sender;   
        }

        /// <summary>
        /// Message from student to instructor
        /// </summary>
        /// <param name="o"></param>
        /// <param name="args"></param>
        private void StudentSendStatus(object o, PropertyEventArgs args) {
            sender_.Post(delegate() {
                this.SendStatusHelper(Group.AllInstructor);
            });
        }

        /// <summary>
        /// Message from instructor to student.
        /// </summary>
        /// <param name="o"></param>
        /// <param name="args"></param>
        private void InstructorSendStatus(object o, PropertyEventArgs args) {
            PropertyChangeEventArgs pcea = (PropertyChangeEventArgs)args;
            //The Id is actually the student Participant ID.
            Guid StudentId = (Guid)pcea.NewValue;
            if (!StudentId.Equals(Guid.Empty)) {
                Group studentGroup = new SingletonGroup(new ParticipantModel(StudentId));
                sender_.Post(delegate() {
                    this.SendStatusHelper(studentGroup);
                });
            }
        }

        /// <summary>
        /// Sends the status of a student submission out to a teacher or student.
        /// </summary>
        /// <param name="group"></param>
        private void SendStatusHelper(Group group) {
            Message message;
            using (Synchronizer.Lock(submission_status_model_.SyncRoot)) {
                if (submission_status_model_.SubmissionStatus == SubmissionStatusModel.Status.Received) {
                    return;
                }
            }
            message = new SubmissionStatusMessage(submission_status_model_);
            message.Group = group;
            sender_.Send(message);
        }

        #region IDisposable Members

        public void Dispose() {
            submission_status_model_.Changed["SubmissionStatus"].Remove(new PropertyEventHandler(this.StudentSendStatus));
        }

        #endregion

        #region INetworkService Members

        public void ForceUpdate(UW.ClassroomPresenter.Network.Groups.Group receivers) {
            sender_.Post(delegate() {
                this.SendStatusHelper(receivers);
            });
        }

        #endregion
    }
}
