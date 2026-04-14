using UnityEngine;
using Alice;

namespace Core.LargeSatan {



    public class JumpState : StrikerState {
        public override Alice.StrikerStateCategory Category => Alice.StrikerStateCategory.Dash;

        [SerializeField] private StrikerAnimationClip fowardClip, backwardClip, upwardClip;
        [SerializeField] StrikerNode fallNode;
        [SerializeField] float jumpSpeed;
        [SerializeField] float upwardThreshold = 0.96f;
        [SerializeField] float duration = 0.5f;
        [SerializeField] float endSpeedRatio = 0.01f;
        [SerializeField] EffectPlayer effectPlayer;
        Vector3 initialVelocity;
        float elapsedTime;
        bool previousUseGravity;

        public override void OnEnter(IStrikerContext context) {

            var direction = context.InputDirection == Vector2.zero ? Vector2.up : context.InputDirection;
            var requestedDirection = context.LocalInputDirection == Vector2.zero ? Vector2.up : context.LocalInputDirection;
            StrikerAnimationClip clip;
            if(requestedDirection.y > upwardThreshold) {
                clip = upwardClip;
            } else if(requestedDirection.x < 0) {
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

            this.ScheduleStateEvent(duration, context => {
                context.TryTransition(fallNode);
            });

            Quaternion effectRotation = transform.rotation;
            if(context.LocalInputDirection.x >= 0) {
                effectRotation *= Quaternion.Euler(0f, 180f, 0f);
            }

            effectPlayer.Emit(effectPlayer.transform.position, effectRotation);
        }

        public override void OnUpdate(IStrikerStateContext context) {
            elapsedTime += Time.deltaTime;
            float ratio = Mathf.Max(endSpeedRatio, 0.0001f);
            float decayRate = Mathf.Log(1f / ratio) / Mathf.Max(duration, 0.0001f);
            float decay = Mathf.Exp(-decayRate * elapsedTime);
            context.Rigidbody.linearVelocity = this.initialVelocity * decay;
        }

        public override void OnExit(IStrikerContext context) {
            context.Rigidbody.useGravity = this.previousUseGravity;
        }
    }
}


