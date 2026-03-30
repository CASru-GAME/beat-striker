using UnityEngine;
using Alice;
using System;

namespace Core.LargeSatan {

    public class DiveState : StrikerState {

        // このステートにいる間、再生されるアニメーションクリップ
        [SerializeField] private StrikerAnimationClip clip;
        [SerializeField] StrikerNode fallNode;
        [SerializeField] float speed = 30f;
        [SerializeField] float duration = 0.5f;
        [SerializeField] float endSpeedRatio = 0.1f;
        [SerializeField] float stopDistanceToGround = 0.7f;
        float groundRayDistance;
        LayerMask groundMask = ~0;
        Vector3 initialVelocity;
        float elapsedTime;
        bool previousUseGravity;

        // このステートに遷移した直後に呼ばれる
        public override void OnEnter(IStrikerContext context) {
            groundRayDistance = stopDistanceToGround * 2f;
            var direction = context.InputDirection == Vector2.zero ? Vector2.up : context.InputDirection;
            // アニメーションの再生を開始する
            context.PlayAnimation(clip);
            this.initialVelocity = speed * direction;
            this.elapsedTime = 0f;
            this.previousUseGravity = context.Rigidbody.useGravity;
            context.Rigidbody.useGravity = false;
            Vector3 enterVelocity = this.initialVelocity;
            if (ShouldStopBeforeGround(context.GetSelf().Position, enterVelocity)) {
                enterVelocity = Vector3.zero;
            }
            context.Rigidbody.linearVelocity = enterVelocity;

            this.ScheduleStateEvent(duration, context => {
                context.TryTransition(fallNode);
            });
        }

        // このステートにいる間、毎フレーム呼ばれる
        public override void OnUpdate(IStrikerStateContext context) {
            elapsedTime += Time.deltaTime;
            float ratio = Mathf.Max(endSpeedRatio, 0.0001f);
            float decayRate = Mathf.Log(1f / ratio) / Mathf.Max(duration, 0.0001f);
            float decay = Mathf.Exp(-decayRate * elapsedTime);
            Vector3 velocity = this.initialVelocity * decay;
            if (ShouldStopBeforeGround(context.GetSelf().Position, velocity)) {
                context.Rigidbody.linearVelocity = Vector3.zero;
                context.TryTransition(fallNode);
                return;
            }

            context.Rigidbody.linearVelocity = velocity;
        }

        // 他のステートに遷移する直前に呼ばれる
        public override void OnExit(IStrikerContext context) {
            context.Rigidbody.useGravity = this.previousUseGravity;
        }

        bool ShouldStopBeforeGround(Vector3 rayOrigin, Vector3 velocity) {
            if (velocity.y >= 0f) {
                return false;
            }

            if (!Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, groundRayDistance, groundMask, QueryTriggerInteraction.Ignore)) {
                return false;
            }

            float downMoveThisFrame = -velocity.y * Time.deltaTime;
            float stopThreshold = stopDistanceToGround + downMoveThisFrame;
            return hit.distance <= stopThreshold;
        }
    }
}
