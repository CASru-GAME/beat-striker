using UnityEngine;
using Alice;

namespace Core.LargeSatan {
    
    public class IdleState : StrikerState {
        // このステートにいる間、再生されるアニメーションクリップ
        [SerializeField] private StrikerAnimationClip animationClip;
        [SerializeField] StrikerNode locomotionNode;


        // このステートに遷移した直後に呼ばれる
        public override void OnEnter(IStrikerContext context) {
            // アニメーションの再生を開始する
            context.PlayAnimation(animationClip);
        }

        public override void OnUpdate(IStrikerStateContext context) {
            context.TryTransition(locomotionNode);
        }   

    }
}
