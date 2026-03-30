using UnityEngine;
using Alice;

namespace Core.LargeSatan {
    
    public class LocomotionState : StrikerState {
        // このステートにいる間、再生されるアニメーションクリップ
        [SerializeField] private StrikerAnimationClip animationClip;
        [SerializeField] StrikerNode locomotionNode;
        [SerializeField] float duration = 0.2f;


        // このステートに遷移した直後に呼ばれる
        public override void OnEnter(IStrikerContext context) {
            // アニメーションの再生を開始する
            context.PlayAnimation(animationClip);
            this.ScheduleStateEvent(duration,ctx => {
                ctx.TryTransition(locomotionNode);
            });
        }

    }
}
