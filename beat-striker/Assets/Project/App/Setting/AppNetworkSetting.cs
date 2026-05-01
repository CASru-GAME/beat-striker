using R3;
using UnityEngine;

namespace Alice {
    public interface IAppNetworkSetting {
        ReadOnlyReactiveProperty<bool> IsOnline { get; }
        string SessionName { get; }
        float MatchTimeoutSeconds { get; }
        int LocalOnlinePlayerId { get; }
        void SetIsOnline(bool enabled);
        void SetLocalOnlinePlayerId(int playerId);
    }

    public class AppNetworkSetting : MonoBehaviour, IAppNetworkSetting {
        [SerializeField] bool isOnline;
        [SerializeField] string sessionName = "beat-striker-minimal";
        [SerializeField, Min(1f)] float matchTimeoutSeconds = 30f;

        readonly ReactiveProperty<bool> isOnlineProperty = new(false);
        int localOnlinePlayerId;
        bool initialized;

        public ReadOnlyReactiveProperty<bool> IsOnline => isOnlineProperty;
        public string SessionName => string.IsNullOrWhiteSpace(sessionName) ? "beat-striker-minimal" : sessionName;
        public float MatchTimeoutSeconds => Mathf.Max(1f, matchTimeoutSeconds);
        public int LocalOnlinePlayerId => localOnlinePlayerId;

        void Awake() {
            InitializeDefaults();
        }

        public void InitializeDefaults() {
            if (initialized) {
                return;
            }

            isOnlineProperty.OnNext(isOnline);
            initialized = true;
        }

        public void SetIsOnline(bool enabled) {
            isOnline = enabled;
            isOnlineProperty.OnNext(enabled);
        }

        public void SetLocalOnlinePlayerId(int playerId) {
            localOnlinePlayerId = Mathf.Clamp(playerId, 0, 1);
        }
    }
}
