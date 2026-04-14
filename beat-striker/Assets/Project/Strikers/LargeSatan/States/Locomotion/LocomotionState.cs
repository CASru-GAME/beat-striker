using UnityEngine;
using Alice;

namespace Core.LargeSatan {


    public class LocomotionState : StrikerState {
        public override Alice.StrikerStateCategory Category => Alice.StrikerStateCategory.Idle;
        // 縺薙・繧ケ繝・・繝医↓縺・ｋ髢薙∝・逕溘＆繧後ｋ繧「繝九Γ繝シ繧キ繝ァ繝ウ繧ッ繝ェ繝・・
        [SerializeField] private StrikerAnimationClip animationClip;
        [SerializeField] StrikerNode locomotionNode;
        [SerializeField] float duration = 0.2f;


        // 縺薙・繧ケ繝・・繝医↓驕キ遘サ縺励◆逶エ蠕後↓蜻シ縺ー繧後ｋ
        public override void OnEnter(IStrikerContext context) {
            // 繧「繝九Γ繝シ繧キ繝ァ繝ウ縺ョ蜀咲函繧帝幕蟋九☆繧・
            context.PlayAnimation(animationClip);
            this.ScheduleStateEvent(duration,ctx => {
                ctx.TryTransition(locomotionNode);
            });
        }

    }
}


