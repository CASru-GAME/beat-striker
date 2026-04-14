using Core.Battle;
using UnityEngine;
using Core.Striker;

namespace Core.LargeHero {


    public class IdleState : StrikerState {
        public override Alice.StrikerStateCategory Category => Alice.StrikerStateCategory.Idle;

        [SerializeField] private StrikerAnimationClip animationClip;
        [SerializeField] StrikerNode attackNode;

        public override void OnEnter(IStrikerContext context) {
            context.PlayAnimation(animationClip);
        }

    }
}


