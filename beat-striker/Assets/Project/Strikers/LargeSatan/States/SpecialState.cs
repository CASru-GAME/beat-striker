using UnityEngine;
using Alice;
using R3;
using System;

namespace Core.LargeSatan {
    
    public class SpecialState : StrikerState {

        // このステートにいる間、再生されるアニメーションクリップ
        [SerializeField] private StrikerAnimationClip animationClip;
        [SerializeField] private StrikerNode nextNode;


        // このステートに遷移した直後に呼ばれる
        public override void OnEnter(IStrikerContext context) {
            context.RequestAttention(new AttentionRequest(3));
            // アニメーションの再生を開始する
            context.PlayAnimation(animationClip, context => {
                context.TryTransition(nextNode);
            });
        }

    }
}
