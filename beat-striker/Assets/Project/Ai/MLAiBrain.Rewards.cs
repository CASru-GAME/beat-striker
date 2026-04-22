using System.Collections.Generic;
using UnityEngine;

namespace Alice {
    public partial class MLAiBrain {
        void ResetRuntimeState() {
            previousSelfHp = null;
            previousOpponentHp = null;
            hasPreviousPositions = false;
            previousDistance = null;
            movementAverageWindow.Clear();
            damageHitAverageWindow.Clear();
            aiChargeCount = 0;
            beatsSinceLastCharge = chargeAutoResetAfterBeats;

            for (var i = 0; i < BEAT_STACK_COUNT; i++) {
                selfMoveDirectionLocalHistory[i] = Vector2.zero;
                opponentMoveDirectionLocalHistory[i] = Vector2.zero;
                selfMoveMagnitudeHistory[i] = 0f;
                opponentMoveMagnitudeHistory[i] = 0f;
                selfStateTransitionHistory[i] = StateTransitionFlags.None;
                opponentStateTransitionHistory[i] = StateTransitionFlags.None;
                selfDamagedHistory[i] = false;
                opponentDamagedHistory[i] = false;
            }

            previousSelfObservedStateCategory = null;
            previousOpponentObservedStateCategory = null;
            selfEnteredDashSinceLastBeat = false;
            selfEnteredAttackSinceLastBeat = false;
            selfEnteredChargeSinceLastBeat = false;
            selfEnteredGuardSinceLastBeat = false;
            selfEnteredElseSinceLastBeat = false;
            opponentEnteredDashSinceLastBeat = false;
            opponentEnteredAttackSinceLastBeat = false;
            opponentEnteredChargeSinceLastBeat = false;
            opponentEnteredGuardSinceLastBeat = false;
            opponentEnteredElseSinceLastBeat = false;

            decisionAgent.ResetDecisionState();
        }

        void EvaluateAndReward(AiObservation observation, AiAction action) {
            var self = observation.Self;
            var opponent = observation.Opponent;

            decisionAgent.AddStepReward(stepPenalty);

            var dealtDamage = 0f;
            var receivedDamage = 0f;
            if (previousSelfHp.HasValue && previousOpponentHp.HasValue) {
                dealtDamage = Mathf.Max(0f, previousOpponentHp.Value - opponent.HitPoint.CurrentValue) / hpRewardScale;
                receivedDamage = Mathf.Max(0f, previousSelfHp.Value - self.HitPoint.CurrentValue) / hpRewardScale;
            }

            var dealtDamageHit = opponentDamagedHistory[0];
            var receivedDamageHit = selfDamagedHistory[0];
            PushBooleanWindow(damageHitAverageWindow, dealtDamageHit, damageHitAverageWindowBeats);

            var currentDamageHitAverage = CalculateTrueRate(damageHitAverageWindow);

            if (dealtDamageHit) {
                decisionAgent.AddStepReward(dealtDamageFixedReward);
                decisionAgent.AddStepReward(dealtDamage * dealtDamageRewardScale);
                decisionAgent.AddStepReward(currentDamageHitAverage * dealtDamageHitRateRewardScale);
            }

            if (receivedDamageHit) {
                decisionAgent.AddStepReward(-receivedDamage * receivedDamagePenaltyScale);
            }

            if (selfStateTransitionHistory[0].EnteredDash) {
                decisionAgent.AddStepReward(enteredDashFixedReward);
            }

            if (selfStateTransitionHistory[0].EnteredAttack) {
                decisionAgent.AddStepReward(enteredAttackFixedPenalty);
            }

            var selfDamagedRecent2 = selfDamagedHistory[0] || selfDamagedHistory[1];
            if (opponentStateTransitionHistory[1].EnteredAttack && !selfDamagedRecent2) {
                decisionAgent.AddStepReward(punishAvoidedReward);
            }

            var opponentDamagedRecent2 = opponentDamagedHistory[0] || opponentDamagedHistory[1];
            if (selfStateTransitionHistory[1].EnteredAttack && !opponentDamagedRecent2) {
                decisionAgent.AddStepReward(attackNoDamage2BeatPenalty);
            }

            if (selfStateTransitionHistory[0].EnteredGuard && !HasOpponentEnteredAttackRecently()) {
                decisionAgent.AddStepReward(unnecessaryGuardPenalty);
            }

            if (IsRecentWindowFilledAndAllFalse(damageHitAverageWindow, 3) && currentDamageHitAverage <= damageHitAverageThreshold) {
                decisionAgent.AddStepReward(noDamageLast3BeatsPenalty);
            }

            EvaluateDistanceControlReward(observation);
            EvaluateMovementActivityReward();
            EvaluateChargeReward(action.Button, selfStateTransitionHistory[0].EnteredAttack);

            previousSelfHp = self.HitPoint.CurrentValue;
            previousOpponentHp = opponent.HitPoint.CurrentValue;
        }

        public override void EndRoundEpisode() {
            decisionAgent.EndEpisode();
            ResetRuntimeState();
        }

        void EvaluateDistanceControlReward(AiObservation observation) {
            var self = observation.Self;
            var opponent = observation.Opponent;
            var distance = Vector2.Distance(
                new Vector2(self.Position.CurrentValue.x, self.Position.CurrentValue.y),
                new Vector2(opponent.Position.CurrentValue.x, opponent.Position.CurrentValue.y)
            );

            if (previousDistance.HasValue && previousDistance.Value <= minPreferredDistance) {
                if (distance < previousDistance.Value) {
                    decisionAgent.AddStepReward(tooCloseApproachPenalty);
                } else if (distance > previousDistance.Value) {
                    decisionAgent.AddStepReward(Mathf.Min(Mathf.Abs(tooCloseApproachPenalty), tooCloseRetreatReward));
                }
            }

            previousDistance = distance;
        }

        void EvaluateMovementActivityReward() {
            var currentStepMovement = selfMoveMagnitudeHistory[0];
            var averageMovement = CalculateAverage(movementAverageWindow);

            if (averageMovement > movementAverageThreshold) {
                return;
            }

            if (currentStepMovement < averageMovement) {
                decisionAgent.AddStepReward(movementBelowAveragePenalty);
                return;
            }

            if (currentStepMovement < movementAverageThreshold * 0.1f) {
                return;
            }

            decisionAgent.AddStepReward(Mathf.Min(Mathf.Abs(movementBelowAveragePenalty), movementAboveAverageReward));
        }

        void EvaluateChargeReward(GamePadButton? button, bool selfEnteredAttackThisBeat) {
            if (selfEnteredAttackThisBeat) {
                aiChargeCount = 0;
                beatsSinceLastCharge = chargeAutoResetAfterBeats;
            }

            if (button == GamePadButton.West) {
                if (aiChargeCount >= chargeOveruseThreshold) {
                    decisionAgent.AddStepReward(chargeOverusePenalty);
                }

                aiChargeCount++;
                beatsSinceLastCharge = 0;
                return;
            }

            beatsSinceLastCharge++;
            if (beatsSinceLastCharge >= chargeAutoResetAfterBeats) {
                aiChargeCount = 0;
            }
        }

        static void PushBooleanWindow(Queue<bool> window, bool value, int maxCount) {
            window.Enqueue(value);

            while (window.Count > maxCount) {
                window.Dequeue();
            }
        }

        static bool IsWindowFilledAndAllFalse(Queue<bool> window, int requiredCount) {
            if (window.Count < requiredCount) {
                return false;
            }

            foreach (var value in window) {
                if (value) {
                    return false;
                }
            }

            return true;
        }

        static bool IsRecentWindowFilledAndAllFalse(Queue<bool> window, int requiredCount) {
            if (window.Count < requiredCount) {
                return false;
            }

            var skipCount = window.Count - requiredCount;
            var index = 0;
            foreach (var value in window) {
                if (index >= skipCount && value) {
                    return false;
                }

                index++;
            }

            return true;
        }

        static float CalculateTrueRate(Queue<bool> window) {
            if (window.Count == 0) {
                return 0f;
            }

            var trueCount = 0;
            foreach (var value in window) {
                if (value) {
                    trueCount++;
                }
            }

            return (float)trueCount / window.Count;
        }

        static float CalculateAverage(Queue<float> window) {
            if (window.Count == 0) {
                return 0f;
            }

            var total = 0f;
            foreach (var value in window) {
                total += value;
            }

            return total / window.Count;
        }

        bool HasOpponentEnteredAttackRecently() {
            return opponentStateTransitionHistory[0].EnteredAttack || 
                   opponentStateTransitionHistory[1].EnteredAttack;
        }
    }
}