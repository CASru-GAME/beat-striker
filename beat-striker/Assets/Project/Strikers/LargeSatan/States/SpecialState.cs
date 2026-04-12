using UnityEngine;
using Alice;
using R3;
using System;

namespace Core.LargeSatan {


    public class SpecialState : StrikerState {
        public override Alice.StrikerStateCategory Category => Alice.StrikerStateCategory.Special;

        // 縺薙・繧ｹ繝・・繝医↓縺・ｋ髢薙∝・逕溘＆繧後ｋ繧｢繝九Γ繝ｼ繧ｷ繝ｧ繝ｳ繧ｯ繝ｪ繝・・
        [SerializeField] private StrikerAnimationClip animationClip;
        [SerializeField] private StrikerNode nextNode;
        [SerializeField] private string attentionTechniqueText = "SPECIAL";


        // 縺薙・繧ｹ繝・・繝医↓驕ｷ遘ｻ縺励◆逶ｴ蠕後↓蜻ｼ縺ｰ繧後ｋ
        public override void OnEnter(IStrikerContext context) {
            var techniqueText = string.IsNullOrWhiteSpace(attentionTechniqueText)
                ? "SPECIAL"
                : attentionTechniqueText;
            context.RequestAttention(new AttentionRequest(3f, techniqueText));
            // 繧｢繝九Γ繝ｼ繧ｷ繝ｧ繝ｳ縺ｮ蜀咲函繧帝幕蟋九☆繧・
            context.PlayAnimation(animationClip, context => {
                context.TryTransition(nextNode);
            });
        }

    }
}


