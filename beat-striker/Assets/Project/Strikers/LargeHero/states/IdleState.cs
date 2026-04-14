using Core.Battle;
using UnityEngine;
using Core.Striker;

namespace Core.LargeHero {


    public class IdleState : StrikerState {
        public override Alice.StrikerStateCategory Category => Alice.StrikerStateCategory.Idle;

        // 縺薙・繧ケ繝・・繝医↓縺・ｋ髢薙∝・逕溘＆繧後ｋ繧「繝九Γ繝シ繧キ繝ァ繝ウ繧ッ繝ェ繝・・
        [SerializeField] private StrikerAnimationClip animationClip;
        [SerializeField] StrikerNode attackNode;

        public override void OnEnter(IStrikerContext context) {
            // 繧「繝九Γ繝シ繧キ繝ァ繝ウ縺ョ蜀咲函繧帝幕蟋九☆繧・
            context.PlayAnimation(animationClip);
        }

    }
}


