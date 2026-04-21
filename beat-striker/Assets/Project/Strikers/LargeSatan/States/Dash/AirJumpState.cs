using UnityEngine;
using Alice;

namespace Core.LargeSatan {



    public class AirJumpState : StrikerState {
        public override Alice.StrikerStateCategory Category => Alice.StrikerStateCategory.Dash;

        [SerializeField] private StrikerAnimationClip fowardClip, backwardClip;
        [SerializeField] StrikerNode fallNode;
        [SerializeField] float jumpSpeed;
        [SerializeField] float duration = 0.5f;
        [SerializeField] float downwardAcceleration = 30f;
        [SerializeField] EffectPlayer effectPlayer;
        Vector3 initialVelocity;
        float elapsedTime;
        bool previousUseGravity;

        public override void OnEnter(IStrikerContext context) {
            var direction = context.InputDirection == Vector2.zero ? Vector2.up : context.InputDirection;
            var requestedDirection = context.LocalInputDirection == Vector2.zero ? Vector2.up : context.LocalInputDirection;

            StrikerAnimationClip clip;
            if(requestedDirection.x < 0) {
                clip = backwardClip;
            } else {
                clip = fowardClip;
            }
            context.PlayAnimation(clip);
            this.initialVelocity = jumpSpeed * direction;
            this.elapsedTime = 0f;
            this.previousUseGravity = context.Rigidbody.useGravity;
            context.Rigidbody.useGravity = false;
            context.Rigidbody.linearVelocity = this.initialVelocity;

            effectPlayer.Emit(effectPlayer.transform.position, effectPlayer.transform.rotation);

            this.ScheduleStateEvent(duration, context => {
                context.TryTransition(fallNode);
            });
        }

        public override void OnUpdate(IStrikerStateContext context) {
            elapsedTime += Time.deltaTime;
            Vector3 downwardVelocity = Vector3.down * (downwardAcceleration * elapsedTime);
            context.Rigidbody.linearVelocity = this.initialVelocity + downwardVelocity;
        }

        public override void OnExit(IStrikerContext context) {
            context.Rigidbody.useGravity = this.previousUseGravity;
        }
    }
}


