


using System.Collections.Generic;
using System.Linq;
using Core.EventBus;
using Core.GamePad;
using UnityEngine;

namespace Core.App.Player {

    /// <summary>
    /// ゲームパッドが参加したときにプレイヤーIDと紐づけ、離脱したときにプレイヤーIDの関連付けを破棄する
    /// プレイヤIDは紐づけのない番号の中で最小のものを割り当てる
    /// 1つのGamePadIdに対し複数のプレイヤIDが紐づくことがある(同じゲームパッドが退出せずに何回も参加した場合)
    /// </summary>
    public class PlayerGamePadBinder : MonoBehaviour {

        [InfoBox("ゲームパッド参加時にゲームパッドとプレイヤーを紐づけ、離脱時に紐づけを解除する。")]
        private Dictionary<int, GamePadId> playerIdToGamePadId;

        void Awake() {
            playerIdToGamePadId = new Dictionary<int, GamePadId>();
            Bus.Subscribe<GamePadJoinedMessage>(OnGamePadJoined);
            Bus.Subscribe<GamePadLeftMessage>(OnGamePadLeft);
        }

        void OnDestroy() {
            Bus.Unsubscribe<GamePadJoinedMessage>(OnGamePadJoined);
            Bus.Unsubscribe<GamePadLeftMessage>(OnGamePadLeft);
        }

        void OnGamePadJoined(GamePadJoinedMessage msg) {

            int playerId = GetNextPlayerId();
            playerIdToGamePadId[playerId] = msg.gamePadId;

            Bus.Publish(new PlayerGamePadBindMessage(
                    new PlayerId(playerId),
                    msg.gamePadId
            ));
        }

        void OnGamePadLeft(GamePadLeftMessage msg) {
            var matchingPlayerIds = playerIdToGamePadId.Where(kvp => kvp.Value.value == msg.gamePadId.value).Select(kvp => kvp.Key).ToList();
            foreach (var playerId in matchingPlayerIds) {
                playerIdToGamePadId.Remove(playerId);
                Bus.Publish(new PlayerGamePadUnbindMessage(new PlayerId(playerId)));
            }
        }

        private int GetNextPlayerId() {
            int id = 0;
            while (playerIdToGamePadId.ContainsKey(id)) {
                id++;
            }
            return id;
        }
    }
}