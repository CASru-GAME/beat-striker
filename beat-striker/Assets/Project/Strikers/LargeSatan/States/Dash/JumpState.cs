using UnityEngine;
using Alice;

namespace Core.LargeSatan {
    
    public class JumpState : StrikerState {

        // このステートにいる間、再生されるアニメーションクリップ
        [SerializeField] private StrikerAnimationClip fowardClip, backwardClip, upwardClip;
        [SerializeField] StrikerNode fallNode;
        [SerializeField] float jumpSpeed;

        // このステートに遷移した直後に呼ばれる
        public override void OnEnter(IStrikerContext context) {
            // アニメーションの再生を開始する
            StrikerAnimationClip clip;
            if(context.InputDirection.y > 0.86f) {
                clip = upwardClip;
            } else if(context.InputDirection.x < 0) {
                clip = backwardClip;
            } else {
                clip = fowardClip;
            }
            context.PlayAnimation(clip, OnAnimationEnd);
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
