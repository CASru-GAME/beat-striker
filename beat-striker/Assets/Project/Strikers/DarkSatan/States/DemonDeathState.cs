using UnityEngine;

namespace Core.DarkSatan {
    public sealed class DemonDeathState : StrikerState {
        public override Alice.StrikerStateCategory Category => Alice.StrikerStateCategory.Unknown;
        [SerializeField] StrikerAnimationClip animationClip;

        public override void OnEnter(IStrikerContext context) {
            StopHorizontalMotion(context);
            context.PlayAnimation(animationClip);
        }

        public override void OnUpdate(IStrikerStateContext context) => StopHorizontalMotion(context);

        static void StopHorizontalMotion(IStrikerContext context) {
            var velocity = context.Rigidbody.linearVelocity;
            context.Rigidbody.linearVelocity = new Vector3(0f, velocity.y, 0f);
        }
    }
}
