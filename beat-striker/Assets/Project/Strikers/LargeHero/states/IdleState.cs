using Core.Battle;
using UnityEngine;
using Core.Striker;

namespace Core.LargeHero {



    public class IdleState : StrikerState {
        public override Alice.StrikerStateCategory Category => Alice.StrikerStateCategory.Idle;

        [SerializeField] private StrikerAnimationClip animationClip;
        [SerializeField] StrikerNode attackNode;
        [SerializeField] StrikerNode locomotionNode;

        public override void OnEnter(IStrikerContext context) {
            context.PlayAnimation(animationClip);
        }

        public override void OnUpdate(IStrikerStateContext context) {
            context.TryTransition(locomotionNode);
        }

    }
}


