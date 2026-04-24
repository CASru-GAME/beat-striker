using Core.Battle;
using UnityEngine;
using Core.Striker;

namespace Core.LargeWizard {



    public class IdleState : StrikerState {
        public override Alice.StrikerStateCategory Category => Alice.StrikerStateCategory.Idle;
        [SerializeField] private StrikerAnimationClip animationClip;
        [SerializeField] StrikerNode locomotionNode;

        // 縺薙・繧ケ繝・・繝医↓縺・ｋ髢薙∝・逕溘＆繧後ｋ繧「繝九Γ繝シ繧キ繝ァ繝ウ繧ッ繝ェ繝・・
       
        // 縺薙・繧ケ繝・・繝医↓驕キ遘サ縺励◆逶エ蠕後↓蜻シ縺ー繧後ｋ
        public override void OnEnter(IStrikerContext context) {
            // 繧「繝九Γ繝シ繧キ繝ァ繝ウ縺ョ蜀咲函繧帝幕蟋九☆繧・
            context.PlayAnimation(animationClip);
        }

        public override void OnUpdate(IStrikerStateContext context) {
            context.TryTransition(locomotionNode);
        }

    }
}


