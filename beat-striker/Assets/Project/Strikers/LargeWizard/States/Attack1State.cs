using Core.Battle;
using UnityEngine;
using Core.Striker;

namespace Core.LargeWizard {



    public class Attack1State : StrikerState {
        public override Alice.StrikerStateCategory Category => Alice.StrikerStateCategory.Attack;

        // このステートにいる間、再生されるアニメーションクリップ
        [SerializeField] private StrikerAnimationClip animationClip;
        [SerializeField] StrikerNode nextNode;
        [SerializeField] AudioClip audioClip;

        [SerializeField] GameObject firePrefab;
        [SerializeField] Transform firePosition;
        [SerializeField] float fireTime = 0.3f;
        [Tooltip("firePrefab を削除するまでの秒数")]
        [SerializeField, Min(0f)] float firePrefabDestroyDelaySeconds = 5f;

        // このステートに遷移した直後に呼ばれる
        public override void OnEnter(IStrikerContext context) {
            // アニメーションの再生を開始する
            context.PlayAnimation(animationClip, context => {
                context.TryTransition(nextNode);
            });

            AudioSource.PlayClipAtPoint(audioClip, firePrefab.transform.position);

            ScheduleStateEvent(fireTime, context => {
                var particleInstance = Instantiate(firePrefab, firePosition.position, context.Rigidbody.rotation);
                var fire = particleInstance.GetComponent<Fire>();
                fire.Hurtbox = context.Rigidbody.GetComponentInChildren<Hurtbox>();
                fire.SetOwnerStrikerHub(context.Rigidbody.GetComponent<StrikerHub>());
                Destroy(particleInstance, firePrefabDestroyDelaySeconds);
            });
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

    }
}
