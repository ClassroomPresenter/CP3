using UW.ClassroomPresenter.Network.Groups;

namespace UW.ClassroomPresenter.Network.Messages {
    public interface INetworkService {
        void ForceUpdate(Group receivers);
    }
}
