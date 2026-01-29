using Core.Battle;
using UnityEngine;
using Core.Striker;
using UnityEditor.Experimental.GraphView;

namespace Core.LargeSatan {
    
    public class WalkState : StrikerState {

        // このステートにいる間、再生されるアニメーションクリップ
        [SerializeField] private StrikerAnimationClip animationClip;
        [SerializeField] float walkSpeed;

        // このステートに遷移した直後に呼ばれる
        public override void OnEnter(IStrikerContext context) {
            // アニメーションの再生を開始する
            context.PlayAnimation(animationClip);
        }

        // このステートにいる間、毎フレーム呼ばれる
        public override void OnUpdate(IStrikerStateContext context) {
            var v = context.Rigidbody.linearVelocity;
            v.x = context.InputDirection.x * walkSpeed;
            context.Rigidbody.linearVelocity = v;
        }

    }
}
