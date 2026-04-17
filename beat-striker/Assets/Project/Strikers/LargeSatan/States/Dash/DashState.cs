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
        [SerializeField] float turnDurationSeconds = 0.2f;
        [SerializeField] float turnPredictionLeadSeconds = 0.2f;
        [SerializeField] float turnTriggerAheadDistance = 0.5f;
        [SerializeField] EffectPlayer effectPlayer;
        Vector3 initialVelocity;
        float elapsedTime;
        bool hasTurnedDuringDash;
        bool isTurning;
        bool canTurnDuringDash;
        bool hasRequestedTurnDuringDash;
        float turnElapsedTime;
        Quaternion turnStartRotation;
        Quaternion turnTargetRotation;
        Transform turnTransform;
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
            this.hasTurnedDuringDash = false;
            this.isTurning = false;
            this.canTurnDuringDash = enterDirectionSign > 0;
            this.hasRequestedTurnDuringDash = false;
            this.turnElapsedTime = 0f;
            this.turnTransform = context.Rigidbody.transform;
            this.turnStartRotation = turnTransform.rotation;
            this.turnTargetRotation = turnTransform.rotation;
            context.Rigidbody.linearVelocity = this.initialVelocity;

            this.ScheduleStateEvent(duration, context => {
                context.TryTransition(nextNode);
            });

            Quaternion effectRotation = turnTransform.rotation;
            if(enterDirectionSign > 0) {
                effectRotation *= Quaternion.Euler(0f, 180f, 0f);
            }

            effectPlayer.Emit(this.transform.position, effectRotation);
        }

        public override void OnUpdate(IStrikerStateContext context) {
            elapsedTime += Time.deltaTime;
            float ratio = Mathf.Max(endSpeedRatio, 0.0001f);
            float decayRate = Mathf.Log(1f / ratio) / Mathf.Max(duration, 0.0001f);
            float decay = Mathf.Exp(-decayRate * elapsedTime);
            context.Rigidbody.linearVelocity = this.initialVelocity * decay;

            var self = context.GetSelf();
            var opponent = context.GetOpponent();
            Vector3 toOpponent = opponent.Position.CurrentValue - self.Position.CurrentValue;
            float predictionSeconds = Mathf.Max(turnPredictionLeadSeconds, 0.0001f);
            Vector3 selfVelocity = context.Rigidbody.linearVelocity;
            Vector3 predictedSelfMove = PredictDashMoveWithDecay(selfVelocity, decayRate, predictionSeconds);
            Vector3 predictedToOpponent = toOpponent - predictedSelfMove;
            float dashAxisSign = Mathf.Abs(initialVelocity.x) > Mathf.Epsilon ? Mathf.Sign(initialVelocity.x) : 1f;
            float predictedOpponentOnDashAxis = predictedToOpponent.x * dashAxisSign;

            if(canTurnDuringDash && !hasRequestedTurnDuringDash && predictedOpponentOnDashAxis < -turnTriggerAheadDistance) {
                this.hasRequestedTurnDuringDash = true;
                this.isTurning = true;
                this.turnElapsedTime = 0f;
                this.turnStartRotation = turnTransform.rotation;
                this.turnTargetRotation = GetTurnTargetRotation(dashAxisSign);
                context.PlayAnimation(backwardClip);
            }

            if(isTurning) {
                float normalizedTurnTime = turnElapsedTime / Mathf.Max(turnDurationSeconds, 0.0001f);
                turnTransform.rotation = Quaternion.Slerp(turnStartRotation, turnTargetRotation, normalizedTurnTime);
                turnElapsedTime += Time.deltaTime;

                if(turnElapsedTime >= turnDurationSeconds) {
                    turnTransform.rotation = turnTargetRotation;
                    this.isTurning = false;
                    this.hasTurnedDuringDash = true;
                }
            }
        }

        public override void OnExit(IStrikerContext context) {
            if(isTurning) {
                turnTransform.rotation = turnTargetRotation;
                this.isTurning = false;
                this.hasTurnedDuringDash = true;
            }
        }

        Vector3 PredictDashMoveWithDecay(Vector3 currentVelocity, float decayRate, float predictionSeconds) {
            if(decayRate <= Mathf.Epsilon) {
                return currentVelocity * predictionSeconds;
            }

            float moveScale = (1f - Mathf.Exp(-decayRate * predictionSeconds)) / decayRate;
            return currentVelocity * moveScale;
        }

        Quaternion GetTurnTargetRotation(float dashAxisSign) {
            Vector3 targetForward = -dashAxisSign * Vector3.right;
            return Quaternion.LookRotation(targetForward, Vector3.up);
        }

    }
}


