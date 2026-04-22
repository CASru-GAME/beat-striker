using System;
using System.Collections.Generic;
using R3;
using Unity.MLAgents.Sensors;
using UnityEngine;

namespace Alice {
    public partial class MLAiBrain {
        private record StateTransitionFlags(
            bool EnteredDash,
            bool EnteredAttack,
            bool EnteredCharge,
            bool EnteredGuard,
            bool EnteredElse
        ) {
            public static readonly StateTransitionFlags None = new(false, false, false, false, false);
        }

        void UpdateBeatFeatureHistory(AiObservation observation) {
            var self = observation.Self;
            var opponent = observation.Opponent;

            var selfDamagedThisBeat = false;
            var opponentDamagedThisBeat = false;
            if (previousSelfHp.HasValue && previousOpponentHp.HasValue) {
                selfDamagedThisBeat = (previousSelfHp.Value - self.HitPoint.CurrentValue) > 0.0001f;
                opponentDamagedThisBeat = (previousOpponentHp.Value - opponent.HitPoint.CurrentValue) > 0.0001f;
            }

            ShiftHistory(selfDamagedHistory, selfDamagedThisBeat);
            ShiftHistory(opponentDamagedHistory, opponentDamagedThisBeat);

            var selfCurrentPosition = self.Position.CurrentValue;
            var opponentCurrentPosition = opponent.Position.CurrentValue;

            var selfBeatDelta = hasPreviousPositions ? selfCurrentPosition - previousSelfPosition : Vector3.zero;
            var opponentBeatDelta = hasPreviousPositions ? opponentCurrentPosition - previousOpponentPosition : Vector3.zero;

            previousSelfPosition = selfCurrentPosition;
            previousOpponentPosition = opponentCurrentPosition;
            hasPreviousPositions = true;

            var selfBeatDelta2D = new Vector2(selfBeatDelta.x, selfBeatDelta.y);
            var opponentBeatDelta2D = new Vector2(opponentBeatDelta.x, opponentBeatDelta.y);

            var selfMoveMagnitude = selfBeatDelta2D.magnitude;
            var opponentMoveMagnitude = opponentBeatDelta2D.magnitude;

            var selfMoveLocalDir = ToLocalDirection(selfBeatDelta2D, self.LookDirection.CurrentValue);
            var opponentMoveLocalDir = ToLocalDirection(opponentBeatDelta2D, opponent.LookDirection.CurrentValue);

            ShiftHistory(selfMoveDirectionLocalHistory, selfMoveLocalDir);
            ShiftHistory(opponentMoveDirectionLocalHistory, opponentMoveLocalDir);
            ShiftHistory(selfMoveMagnitudeHistory, selfMoveMagnitude);
            ShiftHistory(opponentMoveMagnitudeHistory, opponentMoveMagnitude);

            PushWindow(movementAverageWindow, selfMoveMagnitude, movementAverageWindowBeats);
        }

        void EnsureStateCategorySubscriptions(AiObservation observation) {
            if (!ReferenceEquals(observedSelfStriker, observation.Self)) {
                selfStateCategorySubscription?.Dispose();
                observedSelfStriker = observation.Self;
                previousSelfObservedStateCategory = null;
                selfStateCategorySubscription = observedSelfStriker.CurrentStateCategory.Subscribe(OnSelfStateCategoryChanged);
            }

            if (!ReferenceEquals(observedOpponentStriker, observation.Opponent)) {
                opponentStateCategorySubscription?.Dispose();
                observedOpponentStriker = observation.Opponent;
                previousOpponentObservedStateCategory = null;
                opponentStateCategorySubscription = observedOpponentStriker.CurrentStateCategory.Subscribe(OnOpponentStateCategoryChanged);
            }
        }

        void DisposeStateCategorySubscriptions() {
            selfStateCategorySubscription?.Dispose();
            selfStateCategorySubscription = null;
            opponentStateCategorySubscription?.Dispose();
            opponentStateCategorySubscription = null;
            observedSelfStriker = null;
            observedOpponentStriker = null;
        }

        void OnSelfStateCategoryChanged(StrikerStateCategory currentCategory) {
            if (!previousSelfObservedStateCategory.HasValue) {
                previousSelfObservedStateCategory = currentCategory;
                return;
            }

            if (currentCategory == previousSelfObservedStateCategory.Value) {
                return;
            }

            previousSelfObservedStateCategory = currentCategory;
            MarkRecentStateEntry(
                currentCategory,
                ref selfEnteredDashSinceLastBeat,
                ref selfEnteredAttackSinceLastBeat,
                ref selfEnteredChargeSinceLastBeat,
                ref selfEnteredGuardSinceLastBeat,
                ref selfEnteredElseSinceLastBeat
            );
        }

        void OnOpponentStateCategoryChanged(StrikerStateCategory currentCategory) {
            if (!previousOpponentObservedStateCategory.HasValue) {
                previousOpponentObservedStateCategory = currentCategory;
                return;
            }

            if (currentCategory == previousOpponentObservedStateCategory.Value) {
                return;
            }

            previousOpponentObservedStateCategory = currentCategory;
            MarkRecentStateEntry(
                currentCategory,
                ref opponentEnteredDashSinceLastBeat,
                ref opponentEnteredAttackSinceLastBeat,
                ref opponentEnteredChargeSinceLastBeat,
                ref opponentEnteredGuardSinceLastBeat,
                ref opponentEnteredElseSinceLastBeat
            );
        }

        void UpdateStateTransitionWindows() {
            var selfTransition = new StateTransitionFlags(
                ConsumeFlag(ref selfEnteredDashSinceLastBeat),
                ConsumeFlag(ref selfEnteredAttackSinceLastBeat),
                ConsumeFlag(ref selfEnteredChargeSinceLastBeat),
                ConsumeFlag(ref selfEnteredGuardSinceLastBeat),
                ConsumeFlag(ref selfEnteredElseSinceLastBeat)
            );
            var opponentTransition = new StateTransitionFlags(
                ConsumeFlag(ref opponentEnteredDashSinceLastBeat),
                ConsumeFlag(ref opponentEnteredAttackSinceLastBeat),
                ConsumeFlag(ref opponentEnteredChargeSinceLastBeat),
                ConsumeFlag(ref opponentEnteredGuardSinceLastBeat),
                ConsumeFlag(ref opponentEnteredElseSinceLastBeat)
            );

            ShiftHistory(selfStateTransitionHistory, selfTransition);
            ShiftHistory(opponentStateTransitionHistory, opponentTransition);
        }

        static void MarkRecentStateEntry(
            StrikerStateCategory currentCategory,
            ref bool dashEntered,
            ref bool attackEntered,
            ref bool chargeEntered,
            ref bool guardEntered,
            ref bool elseEntered
        ) {
            if (currentCategory == StrikerStateCategory.Dash) {
                dashEntered = true;
                return;
            }

            if (currentCategory == StrikerStateCategory.Attack) {
                attackEntered = true;
                return;
            }

            if (currentCategory == StrikerStateCategory.Charge) {
                chargeEntered = true;
                return;
            }

            if (currentCategory == StrikerStateCategory.Guard) {
                guardEntered = true;
                return;
            }

            elseEntered = true;
        }

        static bool ConsumeFlag(ref bool flag) {
            var consumed = flag;
            flag = false;
            return consumed;
        }

        static void ShiftHistory(Vector2[] history, Vector2 latest) {
            for (var i = history.Length - 1; i > 0; i--) {
                history[i] = history[i - 1];
            }

            history[0] = latest;
        }

        static void ShiftHistory(float[] history, float latest) {
            for (var i = history.Length - 1; i > 0; i--) {
                history[i] = history[i - 1];
            }

            history[0] = latest;
        }

        static void ShiftHistory(StateTransitionFlags[] history, StateTransitionFlags latest) {
            for (var i = history.Length - 1; i > 0; i--) {
                history[i] = history[i - 1];
            }

            history[0] = latest;
        }

        static void ShiftHistory(bool[] history, bool latest) {
            for (var i = history.Length - 1; i > 0; i--) {
                history[i] = history[i - 1];
            }

            history[0] = latest;
        }

        static void PushWindow(System.Collections.Generic.Queue<float> window, float value, int maxCount) {
            window.Enqueue(value);

            while (window.Count > maxCount) {
                window.Dequeue();
            }
        }

        static void WriteStateTransitionFlags(VectorSensor sensor, StateTransitionFlags flags) {
            sensor.AddObservation(flags.EnteredDash ? 1f : 0f);
            sensor.AddObservation(flags.EnteredAttack ? 1f : 0f);
            sensor.AddObservation(flags.EnteredCharge ? 1f : 0f);
            sensor.AddObservation(flags.EnteredGuard ? 1f : 0f);
        }
    }
}