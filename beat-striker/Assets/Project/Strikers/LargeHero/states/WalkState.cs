using Core.Battle;
using UnityEngine;
using Core.Striker;

namespace Core.LargeHero {
    
    public class WalkState : StrikerState {

        // このステートにいる間、再生されるアニメーションクリップ
        [SerializeField] private StrikerAnimationClip animationClip;
        [SerializeField] StrikerNode locomotionNode;
        [SerializeField] float walkSpeed;
        [SerializeField] StrikerNode dashNode;
        [SerializeField] StrikerNode attackNode;
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

        public override void OnAttackRequested(IStrikerStateContext context) {
            context.TryTransition(attackNode);
        }

  }
}
