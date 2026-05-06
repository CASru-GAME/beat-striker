using R3;
using UnityEngine;

namespace Alice {
    public interface IAppNetworkSetting {
        ReadOnlyReactiveProperty<bool> IsOnline { get; }
        string SessionName { get; }
        string CloudApiBaseUrl { get; }
        float SelectionTimeLimitSeconds { get; }
        float DuelInviteSkipCooldownSeconds { get; }
        int LocalOnlinePlayerId { get; }
        void BindMatching(IMatchingModel matchingModel);
        void SetLocalOnlinePlayerId(int playerId);
    }

    public class AppNetworkSetting : MonoBehaviour, IAppNetworkSetting {
        [SerializeField] string sessionName = "beat-striker-minimal";
        [SerializeField] string cloudApiBaseUrl = "https://beat-striker-api-1049753443537.asia-northeast1.run.app";
        [SerializeField, Min(1f)] float selectionTimeLimitSeconds = 180f;
        [SerializeField, Min(0f)] float duelInviteSkipCooldownSeconds = 60f;

        readonly ReactiveProperty<bool> isOnlineProperty = new(false);
        int localOnlinePlayerId;
        bool initialized;
        bool bindingApplied;

        public ReadOnlyReactiveProperty<bool> IsOnline => isOnlineProperty;
        public string SessionName => string.IsNullOrWhiteSpace(sessionName) ? "beat-striker-minimal" : sessionName;
        public string CloudApiBaseUrl => string.IsNullOrWhiteSpace(cloudApiBaseUrl)
            ? "https://beat-striker-api-1049753443537.asia-northeast1.run.app"
            : cloudApiBaseUrl.TrimEnd('/');
        public float SelectionTimeLimitSeconds => Mathf.Max(1f, selectionTimeLimitSeconds);
        public float DuelInviteSkipCooldownSeconds => Mathf.Max(0f, duelInviteSkipCooldownSeconds);
        public int LocalOnlinePlayerId => localOnlinePlayerId;

        void Awake() {
            InitializeDefaults();
        }

        public void InitializeDefaults() {
            if (initialized) {
                return;
            }

            isOnlineProperty.OnNext(false);
            initialized = true;
        }

        public void BindMatching(IMatchingModel matchingModel) {
            if (bindingApplied) {
                return;
            }

            bindingApplied = true;
            matchingModel.IsEstablished
                .Subscribe(value => isOnlineProperty.OnNext(value))
                .AddTo(this);
        }

        public void SetLocalOnlinePlayerId(int playerId) {
            localOnlinePlayerId = Mathf.Clamp(playerId, 0, 1);
        }
    }
}
