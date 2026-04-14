using UnityEngine;
using Alice;
using R3;
using System;

namespace Core.LargeSatan {


    public class SpecialState : StrikerState {
        public override Alice.StrikerStateCategory Category => Alice.StrikerStateCategory.Special;

        // 縺薙・繧ケ繝・・繝医↓縺・ｋ髢薙∝・逕溘＆繧後ｋ繧「繝九Γ繝シ繧キ繝ァ繝ウ繧ッ繝ェ繝・・
        [SerializeField] private StrikerAnimationClip animationClip;
        [SerializeField] private StrikerNode nextNode;
        [SerializeField] private string attentionTechniqueText = "SPECIAL";


        // 縺薙・繧ケ繝・・繝医↓驕キ遘サ縺励◆逶エ蠕後↓蜻シ縺ー繧後ｋ
        public override void OnEnter(IStrikerContext context) {
            var techniqueText = string.IsNullOrWhiteSpace(attentionTechniqueText)
                ? "SPECIAL"
                : attentionTechniqueText;
            context.RequestAttention(new AttentionRequest(3f, techniqueText));
            // 繧「繝九Γ繝シ繧キ繝ァ繝ウ縺ョ蜀咲函繧帝幕蟋九☆繧・
            context.PlayAnimation(animationClip, context => {
                context.TryTransition(nextNode);
            });
        }

    }
}


