using Core.Battle;
using UnityEngine;
using Core.Striker;

namespace Core.LargeWizard {



    public class Attack3State : StrikerState {
        public override Alice.StrikerStateCategory Category => Alice.StrikerStateCategory.Attack;

        // このステートにいる間、再生されるアニメーションクリップ
        [SerializeField] private StrikerAnimationClip animationClip;

         [SerializeField] StrikerNode nextNode;

        [Header("Rock attack settings")]
        [SerializeField] GameObject rockPrefab;             // 頭上に生成する岩のプレハブ
        [SerializeField] AudioClip audioClip;             // 岩生成時の音
        [SerializeField] float fireTime = 0.3f;           // 岩を生成するタイミング（秒）
        [SerializeField] float spawnHeight = 5f;           // 相手の頭上からの生成高さ

        // このステートに遷移した直後に呼ばれる
        public override void OnEnter(IStrikerContext context) {
            // アニメーションの再生を開始する
            context.PlayAnimation(animationClip, OnAnimationEnd);

            ScheduleStateEvent(fireTime, ctx => {
                // 相手のStrikerHubを見つけて上空に岩を生成する
                var opponent = FindOpponent(ctx.Rigidbody.transform);
                if (opponent == null) return;

                var spawnPos = opponent.transform.position + Vector3.up * spawnHeight;
                var rock = Instantiate(rockPrefab, spawnPos, Quaternion.identity);
                var rockBehavior = rock.GetComponent<Rock>();
                rockBehavior.SetAttackerPosition(ctx.Rigidbody.transform.position);
                rockBehavior.SetAttackerRoot(ctx.Rigidbody.transform.root);
                rockBehavior.SetOwnerStrikerHub(ctx.Rigidbody.GetComponent<StrikerHub>());
                audioClip.PlayAtApp(rockPrefab.transform.position);
            });
        }


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
