

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
        private IGamePadInputModel sharedModel;

        public GamePadModel(GamePadConfig config) {
            this.id = config.id;
            this.onThreshold = config.onThreshold;
            this.offThreshold = config.offThreshold;
            directionDown = false;
        }

        public void Initialize(IGamePadInputModel sharedModel) {
            this.sharedModel = sharedModel;
        }

        public GamePadId Id => id;

        public Vector2 GetDirection() => direction;

        public void HandleDirection(Vector2 v) {
            float mag = v.magnitude;
            bool nextDown = directionDown ? (mag >= offThreshold) : (mag >= onThreshold);
            direction = nextDown ? v.normalized : Vector2.zero;

            sharedModel?.FireDirectionChanged(id, direction);

            if (nextDown != directionDown) {
                directionDown = nextDown;
                sharedModel?.FireInputed(id, GamePadButton.Direction, nextDown ? GamePadAction.Down : GamePadAction.Up);
            }
        }

        public void HandleButton(GamePadButton button, GamePadAction action) {
             sharedModel?.FireInputed(id, button, action);
        }

        public void OnEnable() {
             sharedModel?.FireJoined(id);
        }

        public void OnDisable() {
             sharedModel?.FireLeft(id);
        }
        
        // Removed ApplyDirection as it is now internal part of HandleDirection logic (or removed entirely if unused)
        public DirectionResult ApplyDirection(Vector2 v) {
             throw new NotImplementedException("Use HandleDirection instead.");
        }
    }

    [Serializable]
    public struct GamePadConfig {
        public GamePadId id;
        public float onThreshold;
        public float offThreshold;
    }
}
