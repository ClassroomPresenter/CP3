// $Id: RoleModel.cs 1043 2006-07-19 22:48:12Z julenka $

using System;

namespace UW.ClassroomPresenter.Model.Network {
    [Serializable]
    public abstract class RoleModel : PropertyPublisher {
        private readonly Guid m_Id;

        public RoleModel(Guid id) {
            this.m_Id = id;
        }

        public Guid Id {
            get { return this.m_Id; }
        }
    }
}
