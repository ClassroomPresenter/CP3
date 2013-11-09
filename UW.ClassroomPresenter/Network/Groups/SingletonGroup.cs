using System;
using System.Collections.Generic;
using System.Text;
using UW.ClassroomPresenter.Model.Network;

namespace UW.ClassroomPresenter.Network.Groups {
    [Serializable]
    public class SingletonGroup : Group {
        public SingletonGroup(ParticipantModel member)
            : base(member.Guid, GetHumanName(member)) {
        }

        /// <summary>
        /// Utility function to perform synchronizer lock before calling the super-constructor.
        /// </summary>
        private static string GetHumanName(ParticipantModel member) {
            using (Synchronizer.Lock(member.SyncRoot)) {
                return member.HumanName;
            }
        }
    }
}
