using Core.Battle;
using UnityEngine;
using Core.Striker;

namespace Core.LargeWizard {

    public class DeadState : StrikerState {
        public override Alice.StrikerStateCategory Category => Alice.StrikerStateCategory.Unknown;

        // このステートにいる間、再生されるアニメーションクリップ
        [SerializeField] private StrikerAnimationClip animationClip;

        // このステートに遷移した直後に呼ばれる
        public override void OnEnter(IStrikerContext context) {
            DestroyOwnedGuardObjects(context);
            context.Rigidbody.GetComponentInParent<EnergyStorage>()?.ClearChargeEffect();

            // アニメーションの再生を開始する
            context.PlayAnimation(animationClip);
        }

        // このステートにいる間、毎フレーム呼ばれる
        public override void OnUpdate(IStrikerStateContext context) {
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

        void DestroyOwnedGuardObjects(IStrikerContext context) {
            var ownerStrikerHub = context.Rigidbody.GetComponent<StrikerHub>();
            var guards = FindObjectsByType<Guard>(FindObjectsSortMode.None);

            for (var i = 0; i < guards.Length; i++) {
                var guard = guards[i];
                if (!guard.IsOwnedBy(ownerStrikerHub)) {
                    continue;
                }

                Destroy(guard.gameObject);
            }
        }

    }
}
