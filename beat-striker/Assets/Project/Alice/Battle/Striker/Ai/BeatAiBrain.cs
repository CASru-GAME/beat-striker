using System;
using UnityEngine;

namespace Alice {
    public class BeatAiBrain : AiBrain {
        [SerializeField] float keepDistance = 1.2f;
        [SerializeField] float keepDistanceTolerance = 0.4f;
        [SerializeField] float attackDistance = 1.5f;
        [SerializeField] float longJumpDistance = 4f;
        [SerializeField] float jumpDirectionY = 1f;

        int lastObservedOpponentPlayerId = -1;

        protected override AiAction OnGoodWindow(AiObservation observation) {
            var self = observation.Self;
            var opponent = observation.Opponent;

            if (lastObservedOpponentPlayerId != opponent.PlayerId) {
                lastObservedOpponentPlayerId = opponent.PlayerId;
            }

            var offset = opponent.Position - self.Position;
            var offset2D = new Vector2(offset.x, offset.y);
            var distance = offset2D.magnitude;
            var moveDirection = ComputeSpacingDirection(offset2D, distance);

            if (ShouldJumpAgainstConsecutiveAttack(opponent)) {
                var evadeDirection = ComputeJumpAwayDirection(self, opponent);
                return new AiAction(evadeDirection, GamePadButton.South);
            }

            if (distance >= longJumpDistance) {
                var horizontal = Mathf.Sign(offset2D.x);
                if (horizontal == 0f) {
                    horizontal = 1f;
                }
                var jumpDir = new Vector2(horizontal, jumpDirectionY).normalized;
                return new AiAction(jumpDir, GamePadButton.South);
            }

            if (distance <= attackDistance) {
                return new AiAction(moveDirection, GamePadButton.East);
            }

            return new AiAction(moveDirection, null);
        }

        protected override void OnAiEnabled() {
            lastObservedOpponentPlayerId = -1;
        }

        protected override void OnAiDisabled() {
            lastObservedOpponentPlayerId = -1;
        }

        Vector2 ComputeSpacingDirection(Vector2 offset2D, float distance) {
            if (distance <= 0.0001f) {
                return Vector2.zero;
            }

            if (distance < keepDistance - keepDistanceTolerance) {
                return (-offset2D).normalized;
            }

            if (distance > keepDistance + keepDistanceTolerance) {
                return offset2D.normalized;
            }

            return Vector2.zero;
        }

        bool ShouldJumpAgainstConsecutiveAttack(IReadOnlyBattleEntity opponent) {
            // Command-history based heuristic removed — never jump based on command history.
            return false;
        }

        Vector2 ComputeJumpAwayDirection(IReadOnlyBattleEntity self, IReadOnlyBattleEntity opponent) {
            var horizontal = Mathf.Sign(self.Position.x - opponent.Position.x);
            if (horizontal == 0f) {
                horizontal = 1f;
            }

            return new Vector2(horizontal, jumpDirectionY).normalized;
        }
    }
}