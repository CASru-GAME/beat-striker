using Core.Battle;
using UnityEngine;
using Core.Striker;

namespace Core.LargeSatan {
    
    public class FallState : StrikerState {

        // このステートにいる間、再生されるアニメーションクリップ
        [SerializeField] private StrikerAnimationClip animationClip;
        [SerializeField] GroundChecker groundChecker;
        [SerializeField] StrikerNode landNode;

        // このステートに遷移した直後に呼ばれる
        public override void OnEnter(IStrikerContext context) {
            // アニメーションの再生を開始する
            context.PlayAnimation(animationClip);
        }

        // このステートにいる間、毎フレーム呼ばれる
        public override void OnUpdate(IStrikerStateContext context) {
            if (groundChecker.IsGrounded) {
                context.TryTransition(landNode);
            }
        }

        // 他のステートに遷移する直前に呼ばれる
        public override void OnExit(IStrikerContext context) {
        }

        // 攻撃コマンドが押された時に呼ばれる
        public override void OnAttackRequested(IStrikerStateContext context) {
        }

        // チャージコマンドが押された時に呼ばれる
        public override void OnChargeRequested(IStrikerStateContext context) {
        }

        // ダッシュコマンドが押された時に呼ばれる
        public override void OnDashRequested(IStrikerStateContext context) {
        }

        // ガードコマンドが押された時に呼ばれる
        public override void OnGuardRequested(IStrikerStateContext context) {
        }

        // 攻撃を受けた時に呼ばれる
        public override void OnHit(IStrikerStateContext context, HitStatus status) {
        }

        // ミスした時に呼ばれる
        public override void OnMiss(IStrikerStateContext context) {
        }

    }
}
