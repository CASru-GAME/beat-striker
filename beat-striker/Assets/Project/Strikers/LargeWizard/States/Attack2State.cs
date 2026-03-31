using Core.Battle;
using UnityEngine;
using Core.Striker;

namespace Core.LargeWizard {
    
    public class Attack2State : StrikerState {

        // このステートにいる間、再生されるアニメーションクリップ
        [SerializeField] private StrikerAnimationClip animationClip;
        [SerializeField] StrikerNode nextNode;

        [Header("Ice attack settings")]
        [SerializeField] GameObject icePrefab;             // 地面から生える氷のプレハブ
        [SerializeField] AudioClip audioClip1;             // 氷生成時の音
        [SerializeField] AudioClip audioClip2;             // 氷が攻撃判定を発生させるときの音
        [SerializeField] LayerMask groundMask;             // 地面レイヤー
        [SerializeField] float fireTime = 0.3f;           // 氷を生成するタイミング（秒）
        [SerializeField] float groundRayStartHeight = 20f; // 地面探索レイの開始高さ
        [SerializeField] float groundRayDistance = 50f;    // 地面探索レイの長さ

        // このステートに遷移した直後に呼ばれる
        public override void OnEnter(IStrikerContext context) {
            // アニメーションの再生を開始する
            context.PlayAnimation(animationClip, OnAnimationEnd);

            ScheduleStateEvent(fireTime, ctx => {
                // 相手のStrikerHubを見つけて足元に氷を生成する
                var opponent = FindOpponent(ctx.Rigidbody.transform);
                if (opponent == null) return;

                var opponentPos = opponent.transform.position;
                var spawnPos = opponentPos;
                if (Physics.Raycast(opponentPos + Vector3.up * groundRayStartHeight,
                                    Vector3.down, out var rhit, groundRayDistance, groundMask,
                                    QueryTriggerInteraction.Ignore)) {
                    spawnPos = rhit.point;
                }

                var ice = Instantiate(icePrefab, spawnPos, Quaternion.identity);
                var iceBehavior = ice.GetComponent<Ice>();
                iceBehavior.SetAttackerPosition(ctx.Rigidbody.transform.position);
                iceBehavior.SetAttackerRoot(ctx.Rigidbody.transform.root);

                AudioSource.PlayClipAtPoint(audioClip1, icePrefab.transform.position);
                AudioSource.PlayClipAtPoint(audioClip2, icePrefab.transform.position);
            });
        }

        /// <summary>
        /// シーン内の StrikerHub から自分以外（＝相手）を返す
        /// </summary>
        private static StrikerHub FindOpponent(Transform self) {
            var hubs = FindObjectsByType<StrikerHub>(FindObjectsSortMode.None);
            foreach (var hub in hubs) {
                if (hub.transform.root != self.root) return hub;
            }
            return null;
        }

        private void OnAnimationEnd(IStrikerStateContext context) {
            context.TryTransition(nextNode);
        }

        // このステートにいる間、毎フレーム呼ばれる
        public override void OnUpdate(IStrikerStateContext context) {
        }

        // 他のステートに遷移する直前に呼ばれる
        public override void OnExit(IStrikerContext context)
        {
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
