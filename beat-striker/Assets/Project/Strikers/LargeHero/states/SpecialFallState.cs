using Core.Battle;
using UnityEngine;
using Core.Striker;

namespace Core.LargeHero {

    public class SpecialFallState : StrikerState {
        [SerializeField] private StrikerAnimationClip animationClip;
        [SerializeField] private GroundChecker groundChecker;
        [SerializeField] private StrikerNode landNode;
        [SerializeField] private SpecialSequenceContext specialSequenceContext;
        [SerializeField] private float fallSpeed = 15f;

        public override void OnEnter(IStrikerContext context) {
            context.PlayAnimation(animationClip);
        }

        public override void OnUpdate(IStrikerStateContext context) {
            specialSequenceContext.MoveFallTogether(context.Rigidbody, fallSpeed);

            if (groundChecker.IsGrounded) {
                context.TryTransition(landNode);
            }
        }
    }
}
