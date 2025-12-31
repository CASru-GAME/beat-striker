

using System;
using Core.GamePad.Types;
using UnityEngine;

namespace Core.GamePad.Models {

    public class GamePadModel : IGamePadModel {
        private readonly GamePadId id;
        private readonly float onThreshold;
        private readonly float offThreshold;

        private bool directionDown;
        private Vector2 direction = Vector2.zero;

        public GamePadModel(GamePadConfig config) {
            this.id = config.id;
            this.onThreshold = config.onThreshold;
            this.offThreshold = config.offThreshold;
            directionDown = false;
        }

        public GamePadId Id => id;

        public Vector2 GetDirection() => direction;

        public DirectionResult ApplyDirection(Vector2 v) {
            float mag = v.magnitude;
            bool nextDown = directionDown ? (mag >= offThreshold) : (mag >= onThreshold);
            direction = nextDown ? v.normalized : Vector2.zero;

            if (nextDown != directionDown) {
                directionDown = nextDown;
                return new DirectionResult(true, nextDown);
            }
            return new DirectionResult(false, directionDown);
        }
    }

    [Serializable]
    public struct GamePadConfig {
        public GamePadId id;
        public float onThreshold;
        public float offThreshold;
    }
}
