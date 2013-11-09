// $Id: IRemoteId.cs 975 2006-06-28 01:02:59Z pediddle $

using System;

namespace UW.ClassroomPresenter.Model {
    public interface IRemoteId {
        Guid Id { get; }
    }
}
