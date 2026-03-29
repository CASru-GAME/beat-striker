using UnityEngine;
using Alice;

namespace Core.LargeSatan {
    
    public class JumpBackwardState : StrikerState {

        // このステートにいる間、再生されるアニメーションクリップ
        [SerializeField] private StrikerAnimationClip animationClip;
        [SerializeField] StrikerNode fallNode;
        [SerializeField] float jumpSpeed;

        // このステートに遷移した直後に呼ばれる
        public override void OnEnter(IStrikerContext context) {
            // アニメーションの再生を開始する
            context.PlayAnimation(animationClip, OnAnimationEnd);
            context.Rigidbody.linearVelocity = jumpSpeed * context.InputDirection;
        }

        // このステートにいる間、毎フレーム呼ばれる
        public override void OnUpdate(IStrikerStateContext context) {
        }

        // 他のステートに遷移する直前に呼ばれる
        public override void OnExit(IStrikerContext context) {
        }

        void OnAnimationEnd(IStrikerStateContext context){
            context.TryTransition(fallNode);
        }

    }
}
