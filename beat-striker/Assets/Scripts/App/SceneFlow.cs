

using Core.EventBus;
using UnityEngine;

namespace Core.App {

    /// <summary>
    /// シーン全体の流れを管理するコンポーネント
    /// ゲームパッドとプレイヤーの紐づけに応じてカーソルオブジェクトを生成・破棄する
    /// </summary>
    public class SceneFlow : MonoBehaviour {
        [SerializeField] private GameObject cursorPrefab;

        void Awake() {
            Bus.Subscribe<PlayerGamePadBindMessage>(OnPlayerGamePadBind);
            Bus.Subscribe<PlayerGamePadUnbindMessage>(OnPlayerGamePadUnbind);
        }

        void OnDestroy() {
            Bus.Unsubscribe<PlayerGamePadBindMessage>(OnPlayerGamePadBind);
            Bus.Unsubscribe<PlayerGamePadUnbindMessage>(OnPlayerGamePadUnbind);
        }

        private void OnPlayerGamePadBind(PlayerGamePadBindMessage msg) {
            Instantiate(cursorPrefab);
            Bus.Publish(new CursorActivatedMessage(msg.playerId, msg.gamePadId));
        }

        private void OnPlayerGamePadUnbind(PlayerGamePadUnbindMessage msg) {
            Bus.Publish(new CursorDeactivatedMessage(msg.playerId));
        }
    }
}