using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Alice {
    public partial class MLAiBrain {
        internal void WriteObservations(VectorSensor sensor, AiObservation observation) {
            var self = observation.Self;
            var opponent = observation.Opponent;
            var offset = opponent.Position.CurrentValue - self.Position.CurrentValue;
            var offset2D = new Vector2(offset.x, offset.y);
            var distance = offset2D.magnitude;

            sensor.AddObservation(NormalizeSigned(distance, distanceObservationScale));

            var selfToOpponentLocal = ToLocalDirection(offset2D, self.LookDirection.CurrentValue);
            sensor.AddObservation(selfToOpponentLocal.x);
            sensor.AddObservation(selfToOpponentLocal.y);

            for (var i = 0; i < BEAT_STACK_COUNT; i++) {
                sensor.AddObservation(selfMoveDirectionLocalHistory[i].x);
                sensor.AddObservation(selfMoveDirectionLocalHistory[i].y);
            }

            for (var i = 0; i < BEAT_STACK_COUNT; i++) {
                sensor.AddObservation(NormalizeSigned(selfMoveMagnitudeHistory[i], beatMoveMagnitudeObservationScale));
            }

            for (var i = 0; i < BEAT_STACK_COUNT; i++) {
                sensor.AddObservation(opponentMoveDirectionLocalHistory[i].x);
                sensor.AddObservation(opponentMoveDirectionLocalHistory[i].y);
            }

            for (var i = 0; i < BEAT_STACK_COUNT; i++) {
                sensor.AddObservation(NormalizeSigned(opponentMoveMagnitudeHistory[i], beatMoveMagnitudeObservationScale));
            }

            for (var i = 0; i < BEAT_STACK_COUNT; i++) {
                WriteStateTransitionFlags(sensor, selfStateTransitionHistory[i]);
            }

            for (var i = 0; i < BEAT_STACK_COUNT; i++) {
                WriteStateTransitionFlags(sensor, opponentStateTransitionHistory[i]);
            }

            for (var i = 0; i < BEAT_STACK_COUNT; i++) {
                sensor.AddObservation(selfDamagedHistory[i] ? 1f : 0f);
            }

            for (var i = 0; i < BEAT_STACK_COUNT; i++) {
                sensor.AddObservation(opponentDamagedHistory[i] ? 1f : 0f);
            }

            WriteStrikerOneHot(sensor, opponent.Striker.CurrentValue);
            sensor.AddObservation(Mathf.Clamp01((float)aiChargeCount / chargeCountObservationCap));
        }

        internal void WriteZeroObservations(VectorSensor sensor, int count) {
            for (var i = 0; i < count; i++) {
                sensor.AddObservation(0f);
            }
        }

        internal AiAction DecodeAction(AiObservation observation, ActionBuffers actions) {
            var buttonAction = actions.DiscreteActions[0];
            var moveDirectionAction = actions.DiscreteActions[1];

            var moveDirection = DecodeMoveDirection(observation.Self, moveDirectionAction);
            var button = DecodeButton(buttonAction);
            return new AiAction(moveDirection, button);
        }

        internal void WriteHeuristic(ActionBuffers actionsOut) {
            var discreteActions = actionsOut.DiscreteActions;
            discreteActions[0] = 0;
            discreteActions[1] = 0;
        }



        static Vector2 ToLocalDirection(Vector2 worldDirection, Vector3 lookDirection) {
            if (worldDirection.sqrMagnitude <= 0.000001f) {
                return Vector2.zero;
            }

            var normalizedWorld = worldDirection.normalized;
            var forward = GetForward2D(lookDirection);
            var up = new Vector2(-forward.y, forward.x);

            var local = new Vector2(
                Vector2.Dot(normalizedWorld, forward),
                Vector2.Dot(normalizedWorld, up)
            );

            if (local.sqrMagnitude <= 0.000001f) {
                return Vector2.zero;
            }

            return local.normalized;
        }

        static Vector2 GetForward2D(Vector3 lookDirection) {
            var forward = new Vector2(lookDirection.x, lookDirection.y);
            if (forward.sqrMagnitude > 0.000001f) {
                return forward.normalized;
            }

            return Vector2.right;
        }

        Vector2 DecodeMoveDirection(IObservableStriker self, int moveAction) {
            if (moveAction <= 0) {
                return Vector2.zero;
            }

            var localDirection = moveAction switch {
                1 => Vector2.right,
                2 => new Vector2(0.70710677f, 0.70710677f),
                3 => Vector2.up,
                4 => new Vector2(-0.70710677f, 0.70710677f),
                5 => Vector2.left,
                6 => new Vector2(0.70710677f, -0.70710677f),
                7 => Vector2.down,
                8 => new Vector2(-0.70710677f, -0.70710677f),
                _ => Vector2.zero,
            };

            if (localDirection.sqrMagnitude <= 0.000001f) {
                return Vector2.zero;
            }

            var forward = GetForward2D(self.LookDirection.CurrentValue);
            var up = new Vector2(-forward.y, forward.x);
            var worldDirection = forward * localDirection.x + up * localDirection.y;
            return worldDirection.sqrMagnitude > 0.000001f ? worldDirection.normalized : Vector2.zero;
        }



        static GamePadButton? DecodeButton(int buttonAction) {
            return buttonAction switch {
                0 => GamePadButton.East,
                1 => GamePadButton.South,
                2 => GamePadButton.West,
                3 => GamePadButton.North,
                _ => GamePadButton.East,
            };
        }

        static float NormalizeSigned(float value, float scale) {
            if (scale <= 0f) {
                return 0f;
            }

            return Mathf.Clamp(value / scale, -1f, 1f);
        }

        static void WriteStrikerOneHot(VectorSensor sensor, Striker striker) {
            var index = (int)striker;
            for (var i = 0; i < STRIKER_TYPE_COUNT; i++) {
                sensor.AddObservation(i == index ? 1f : 0f);
            }
        }
    }
}