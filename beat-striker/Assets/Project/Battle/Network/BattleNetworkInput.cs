using Fusion;
using UnityEngine;

namespace Alice {
    public struct BattleNetworkInput : INetworkInput {
        public int BeatIndex;
        public int ComboCount;
        public GamePadButton Button;
        public Vector2 Direction;
        public byte HasCommand;
    }
}
