using System;
using R3;
using UnityEngine;

namespace Alice {
    public class BeatAiBrain : AiBrain {
        [SerializeField] float keepDistance = 1.2f;
        [SerializeField] float keepDistanceTolerance = 0.4f;
        [SerializeField] float attackDistance = 1.5f;
        [SerializeField] float longJumpDistance = 4f;
        [SerializeField] float jumpHoldDuration = 0.12f;

        IReadOnlyBattleEntity targetEnemy;
        bool aiEnabled;
        int lastHandledEnemyCommandHistoryCount;
        IDisposable selfStateNameSubscription;
        IDisposable targetStateNameSubscription;

        void Update() {
            if (!aiEnabled) {
                return;
            }

            var target = ResolveTargetEnemy();
            if (target == null) {
                EmitDirection(Vector2.zero);
                return;
            }

            var offset = target.Position - SelfStriker.Position;
            var offset2D = new Vector2(offset.x, offset.y);
            var distance = offset2D.magnitude;

            if (distance <= 0.0001f) {
                EmitDirection(Vector2.zero);
                return;
            }

            if (distance < keepDistance - keepDistanceTolerance) {
                EmitDirection((-offset2D).normalized);
                return;
            }

            if (distance > keepDistance + keepDistanceTolerance) {
                EmitDirection(offset2D.normalized);
                return;
            }

            EmitDirection(Vector2.zero);
        }

        protected override void OnGoodZoneEntered() {
            var target = ResolveTargetEnemy();
            if (target == null) {
                return;
            }

            if (ShouldJumpAgainstConsecutiveAttack(target)) {
                JumpAwayFrom(target);
                return;
            }

            var offset = target.Position - SelfStriker.Position;
            var offset2D = new Vector2(offset.x, offset.y);
            var distance = offset2D.magnitude;

            if (distance >= longJumpDistance) {
                var horizontal = Mathf.Sign(offset2D.x);
                var jumpDir = new Vector2(horizontal, 1f).normalized;
                Press(GamePadButton.South);
                EmitDirectionFor(jumpDir, jumpHoldDuration);
                return;
            }
            
            if (distance <= attackDistance) {
                Press(GamePadButton.East);
                return;
            }
        }

        protected override void OnAiEnabled() {
            aiEnabled = true;
            targetEnemy = null;
            lastHandledEnemyCommandHistoryCount = 0;

            selfStateNameSubscription?.Dispose();
            selfStateNameSubscription = SelfStriker.CurrentStateName.Subscribe(stateName => {
                Debug.Log($"[BeatAiBrain] Self State Changed: {stateName}");
            });
        }

        protected override void OnAiDisabled() {
            aiEnabled = false;
            targetEnemy = null;
            lastHandledEnemyCommandHistoryCount = 0;

            selfStateNameSubscription?.Dispose();
            selfStateNameSubscription = null;
            targetStateNameSubscription?.Dispose();
            targetStateNameSubscription = null;
        }

        IReadOnlyBattleEntity ResolveTargetEnemy() {
            if (targetEnemy != null && targetEnemy.HitPoint > 0f) {
                return targetEnemy;
            }

            foreach (var opponent in GetOpponentStrikers()) {
                if (opponent.HitPoint <= 0f) {
                    continue;
                }

                if (targetEnemy != opponent) {
                    lastHandledEnemyCommandHistoryCount = 0;
                    SubscribeTargetStateName(opponent);
                }
                targetEnemy = opponent;
                return targetEnemy;
            }

            targetStateNameSubscription?.Dispose();
            targetStateNameSubscription = null;
            targetEnemy = null;
            return null;
        }

        bool ShouldJumpAgainstConsecutiveAttack(IReadOnlyBattleEntity target) {
            var history = target.CommandHistory;
            var count = history.Count;
            if (count < 2) {
                return false;
            }

            if (count == lastHandledEnemyCommandHistoryCount) {
                return false;
            }

            var latest = history[count - 1];
            var previous = history[count - 2];
            if (latest.Button != GamePadButton.East || previous.Button != GamePadButton.East) {
                return false;
            }

            lastHandledEnemyCommandHistoryCount = count;
            return true;
        }

        void JumpAwayFrom(IReadOnlyBattleEntity target) {
            var horizontal = Mathf.Sign(SelfStriker.Position.x - target.Position.x);
            if (horizontal == 0f) {
                horizontal = 1f;
            }

            var jumpDirection = new Vector2(horizontal, 1f).normalized;
            Press(GamePadButton.South);
            EmitDirectionFor(jumpDirection, jumpHoldDuration);
        }

        void SubscribeTargetStateName(IReadOnlyBattleEntity target) {
            targetStateNameSubscription?.Dispose();
            targetStateNameSubscription = target.CurrentStateName.Subscribe(stateName => {
                Debug.Log($"[BeatAiBrain] Target State Changed (Player {target.PlayerId}): {stateName}");
            });
        }

        void OnDestroy() {
            selfStateNameSubscription?.Dispose();
            targetStateNameSubscription?.Dispose();
        }
    }
}