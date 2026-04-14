using UnityEngine;
using Alice;
using System;

namespace Core.LargeSatan {



    public class DashState : StrikerState {
        public override Alice.StrikerStateCategory Category => Alice.StrikerStateCategory.Dash;

        [SerializeField] private StrikerAnimationClip fowardClip, backwardClip;
        [SerializeField] StrikerNode nextNode;
        [SerializeField] float[] consecutiveDashSpeeds = { 20f, 24f, 28f };
        [SerializeField] float reentryWindowSeconds = 0.7f;
        [SerializeField] float duration = 0.5f;
        [SerializeField] float endSpeedRatio = 0.01f;
        [SerializeField] float stopDistanceToOpponent = 1.0f;
        [SerializeField] EffectPlayer effectPlayer;
        Vector3 initialVelocity;
        float elapsedTime;
        bool stoppedByOpponentDistance;
        bool stopBehindOpponent;
        float lastEnterTime = float.NegativeInfinity;
        int lastEnterDirectionSign;
        int consecutiveEnterCount;

        public override void OnEnter(IStrikerContext context) {
            int requestedInputX = Math.Sign(context.LocalInputDirection.x);
            int enterDirectionSign = requestedInputX < 0 ? -1 : 1;
            int movementInputX = Math.Sign(context.InputDirection.x);
            int movementDirectionSign = movementInputX < 0 ? -1 : 1;
            float elapsedSinceLastEnter = Time.time - lastEnterTime;
            if(elapsedSinceLastEnter <= reentryWindowSeconds && enterDirectionSign == lastEnterDirectionSign && enterDirectionSign == 1) {
                consecutiveEnterCount++;
            } else {
                consecutiveEnterCount = 0;
            }
            lastEnterTime = Time.time;
            lastEnterDirectionSign = enterDirectionSign;

            StrikerAnimationClip clip;
            if(enterDirectionSign < 0) {
                clip = backwardClip;
            } else {
                clip = fowardClip;
            }
            context.PlayAnimation(clip);
            int speedIndex = Mathf.Min(consecutiveEnterCount, consecutiveDashSpeeds.Length - 1);
            float dashSpeed = consecutiveDashSpeeds[speedIndex];
            this.initialVelocity = dashSpeed * movementDirectionSign * Vector2.right;
            this.elapsedTime = 0f;
            this.stoppedByOpponentDistance = false;
            this.stopBehindOpponent = enterDirectionSign > 0;
            context.Rigidbody.linearVelocity = this.initialVelocity;

            this.ScheduleStateEvent(duration, context => {
                context.TryTransition(nextNode);
            });

            Quaternion effectRotation = transform.rotation;
            if(enterDirectionSign > 0) {
                effectRotation *= Quaternion.Euler(0f, 180f, 0f);
            }

            effectPlayer.Emit(this.transform.position, effectRotation);
        }

        public override void OnUpdate(IStrikerStateContext context) {
            if(stoppedByOpponentDistance) {
                context.Rigidbody.linearVelocity = Vector3.zero;
                return;
            }

            elapsedTime += Time.deltaTime;
            float ratio = Mathf.Max(endSpeedRatio, 0.0001f);
            float decayRate = Mathf.Log(1f / ratio) / Mathf.Max(duration, 0.0001f);
            float decay = Mathf.Exp(-decayRate * elapsedTime);
            context.Rigidbody.linearVelocity = this.initialVelocity * decay;

            var self = context.GetSelf();
            var opponent = context.GetOpponent();
            Vector3 toOpponent = opponent.Position.CurrentValue - self.Position.CurrentValue;

            if(this.stopBehindOpponent) {
                Vector3 frameMove = context.Rigidbody.linearVelocity * Time.deltaTime;
                Vector3 dashDirection = this.initialVelocity.x < 0f ? Vector3.left : Vector3.right;
                float projectedDistanceToOpponent = Vector3.Dot(toOpponent, dashDirection);
                float projectedFrameMove = Vector3.Dot(frameMove, dashDirection);
                float stopThreshold = -stopDistanceToOpponent;
                bool willEnterStopDistance = WillEnterStopDistanceThisFrame(projectedDistanceToOpponent, projectedFrameMove, stopThreshold);

                if(projectedDistanceToOpponent <= stopThreshold || willEnterStopDistance) {
                    this.stoppedByOpponentDistance = true;
                    context.Rigidbody.linearVelocity = Vector3.zero;
                }
            } else {
                float sqrDistance = toOpponent.sqrMagnitude;
                float sqrStopDistance = stopDistanceToOpponent * stopDistanceToOpponent;
                float towardOpponent = Vector3.Dot(context.Rigidbody.linearVelocity, toOpponent);
                Vector3 frameMove = context.Rigidbody.linearVelocity * Time.deltaTime;
                bool willEnterStopDistance = WillEnterStopDistanceThisFrame(self.Position.CurrentValue, opponent.Position.CurrentValue, frameMove, stopDistanceToOpponent);

                if(towardOpponent > 0f && (sqrDistance <= sqrStopDistance || willEnterStopDistance)) {
                    this.stoppedByOpponentDistance = true;
                    context.Rigidbody.linearVelocity = Vector3.zero;
                }
            }
        }

        bool WillEnterStopDistanceThisFrame(float projectedDistanceToOpponent, float projectedFrameMove, float stopThreshold) {
            if(projectedFrameMove <= Mathf.Epsilon) {
                return false;
            }

            return projectedDistanceToOpponent - projectedFrameMove <= stopThreshold;
        }

        bool WillEnterStopDistanceThisFrame(Vector3 selfPosition, Vector3 opponentPosition, Vector3 frameMove, float stopDistance) {
            float moveSqrMagnitude = frameMove.sqrMagnitude;
            if(moveSqrMagnitude <= Mathf.Epsilon) {
                return false;
            }

            Vector3 offset = selfPosition - opponentPosition;
            float a = moveSqrMagnitude;
            float b = 2f * Vector3.Dot(offset, frameMove);
            float c = offset.sqrMagnitude - stopDistance * stopDistance;
            float discriminant = b * b - 4f * a * c;
            if(discriminant < 0f) {
                return false;
            }

            float sqrtDiscriminant = Mathf.Sqrt(discriminant);
            float inverse2A = 1f / (2f * a);
            float t1 = (-b - sqrtDiscriminant) * inverse2A;
            float t2 = (-b + sqrtDiscriminant) * inverse2A;
            return (0f <= t1 && t1 <= 1f) || (0f <= t2 && t2 <= 1f);
        }

        public override void OnExit(IStrikerContext context) {
        }

    }
}


