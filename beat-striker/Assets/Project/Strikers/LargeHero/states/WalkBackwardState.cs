using Core.Battle;
using UnityEngine;
using Core.Striker;

namespace Core.LargeHero {


    public class WalkBackwardState : StrikerState {
        public override Alice.StrikerStateCategory Category => Alice.StrikerStateCategory.Idle;

        [SerializeField] private StrikerAnimationClip animationClip;
        [SerializeField] StrikerNode locomotionNode;
        [SerializeField] float walkSpeed;
            [SerializeField] StrikerNode dashNode;
            [SerializeField] StrikerNode attackNode;

        public override void OnEnter(IStrikerContext context) {
            context.PlayAnimation(animationClip);
        }

        public override void OnUpdate(IStrikerStateContext context) {
            var v = context.Rigidbody.linearVelocity;
            v.x = context.InputDirection.x * walkSpeed;
            context.Rigidbody.linearVelocity = v;
        }

    }
}


