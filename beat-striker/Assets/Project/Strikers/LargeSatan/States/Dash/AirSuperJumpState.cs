using UnityEngine;
using Alice;

namespace Core.LargeSatan {
    
    public class AirSuperJumpState : StrikerState {

        // このステートにいる間、再生されるアニメーションクリップ
        [SerializeField] private StrikerAnimationClip clip;
        [SerializeField] StrikerNode fallNode;
        [SerializeField] float jumpSpeed;
        [SerializeField] float duration = 0.5f;
        [SerializeField] float downwardAcceleration = 30f;
        Vector3 initialVelocity;
        float elapsedTime;
        bool previousUseGravity;

        // このステートに遷移した直後に呼ばれる
        public override void OnEnter(IStrikerContext context) {
            var direction = context.InputDirection == Vector2.zero ? Vector2.up : context.InputDirection;
            // アニメーションの再生を開始する
            context.PlayAnimation(clip);
            this.initialVelocity = jumpSpeed * direction;
            this.elapsedTime = 0f;
            this.previousUseGravity = context.Rigidbody.useGravity;
            context.Rigidbody.useGravity = false;
            context.Rigidbody.linearVelocity = this.initialVelocity;

            this.ScheduleStateEvent(duration, context => {
                context.TryTransition(fallNode);
            });
        }

        // このステートにいる間、毎フレーム呼ばれる
        public override void OnUpdate(IStrikerStateContext context) {
            elapsedTime += Time.deltaTime;
            Vector3 downwardVelocity = Vector3.down * (downwardAcceleration * elapsedTime);
            context.Rigidbody.linearVelocity = this.initialVelocity + downwardVelocity;
        }

        // 他のステートに遷移する直前に呼ばれる
        public override void OnExit(IStrikerContext context) {
            context.Rigidbody.useGravity = this.previousUseGravity;
        }
    }
}
