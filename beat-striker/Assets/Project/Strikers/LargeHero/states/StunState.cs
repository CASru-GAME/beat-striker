using Core.Battle;
using UnityEngine;
using Core.Striker;

namespace Core.LargeHero {
    
    public class StunState : StrikerState {

        // このステートにいる間、再生されるアニメーションクリップ
        [SerializeField] private StrikerAnimationClip animationClip;
        [SerializeField] StrikerNode nextNode;
        // このステートに遷移した直後に呼ばれる
        public override void OnEnter(IStrikerContext context) {
            Debug.Log("StunStateに遷移");
            // アニメーションの再生を開始する
            context.PlayAnimation(animationClip, OnAnimationEnd);
        }
        void OnAnimationEnd(IStrikerStateContext context)
        {}
        // このステートにいる間、毎フレーム呼ばれる
        public override void OnUpdate(IStrikerStateContext context) {
        }

        // 他のステートに遷移する直前に呼ばれる
        public override void OnExit(IStrikerContext context) {
        }

        // 攻撃コマンドが押された時に呼ばれる
        public override void OnAttackRequested(IStrikerStateContext context) {
            context. TryTransition(nextNode);
        }

        // チャージコマンドが押された時に呼ばれる
        public override void OnChargeRequested(IStrikerStateContext context) {
            context. TryTransition(nextNode);

        }

        // ダッシュコマンドが押された時に呼ばれる
        public override void OnDashRequested(IStrikerStateContext context) {
            context. TryTransition(nextNode);
        }

        // ガードコマンドが押された時に呼ばれる
        public override void OnGuardRequested(IStrikerStateContext context) {
            context. TryTransition(nextNode);

        }

        // 攻撃を受けた時に呼ばれる
        public override void OnHit(IStrikerStateContext context, HitStatus status) {
            context.ApplyDamage(status.Damage);
        }

        // ミスした時に呼ばれる
        public override void OnMiss(IStrikerStateContext context) {
        }

    }
}
