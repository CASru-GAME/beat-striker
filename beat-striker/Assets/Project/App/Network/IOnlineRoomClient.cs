using System.Threading.Tasks;
using R3;

namespace Alice {
    public record OnlineRoomRoster(bool IsConnected, OnlineRoomPlayer[] Players) {
        public static OnlineRoomRoster Disconnected => new(false, System.Array.Empty<OnlineRoomPlayer>());
    }

    public interface IOnlineRoomClient {
        ReadOnlyReactiveProperty<OnlineRoomRoster> Roster { get; }
        Task RefreshRoomAsync();
        Task<bool> InvitePlayerAsync(string sessionId);
    }
}
