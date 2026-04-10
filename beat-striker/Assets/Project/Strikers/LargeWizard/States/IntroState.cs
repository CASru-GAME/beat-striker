using Core.Battle;
using UnityEngine;
using Core.Striker;

namespace Core.LargeWizard {
    
    public class IntroState : StrikerState {

        // このステートにいる間、再生されるアニメーションクリップ
        [SerializeField] private StrikerAnimationClip animationClip;
        [SerializeField, Min(0f)] float animationPlayDelaySeconds = 0f;
        [SerializeField] GameObject magicCirclePrefab;
        [SerializeField] Transform magicCircleSpawnPoint;
        [SerializeField] Vector3 magicCircleRotationEuler;
        [SerializeField] GameObject tuePrefab;
        [SerializeField] GameObject wizardPrefab;
        [SerializeField, Min(0f)] float magicCircleGrowDurationSeconds = 0.4f;
        [SerializeField, Min(0f)] float magicCircleDestroyDelaySeconds = 3f;
        [SerializeField, Min(0f)] float tueAndWizardSpawnDelaySeconds = 1f;

        // このステートに遷移した直後に呼ばれる
        public override void OnEnter(IStrikerContext context) {
            ScheduleStateEvent(animationPlayDelaySeconds, stateContext => {
                stateContext.PlayAnimation(animationClip);
            });

            tuePrefab.SetActive(false);
            wizardPrefab.SetActive(false);

            var magicCircleInstance = Instantiate(magicCirclePrefab, magicCircleSpawnPoint.position, Quaternion.Euler(magicCircleRotationEuler));
            var growEffect = magicCircleInstance.AddComponent<GrowFromZeroScaleEffect>();
            growEffect.Initialize(magicCircleInstance.transform.localScale, magicCircleGrowDurationSeconds);
            Destroy(magicCircleInstance, magicCircleDestroyDelaySeconds);

            ScheduleStateEvent(tueAndWizardSpawnDelaySeconds, stateContext => {
                tuePrefab.SetActive(true);
                wizardPrefab.SetActive(true);
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

    public class GrowFromZeroScaleEffect : MonoBehaviour {
        Vector3 targetScale;
        float duration;
        float elapsed;

        public void Initialize(Vector3 targetScale, float duration) {
            this.targetScale = targetScale;
            this.duration = duration;
            transform.localScale = Vector3.zero;
        }

        void Update() {
            if (duration <= 0f) {
                transform.localScale = targetScale;
                Destroy(this);
                return;
            }

            elapsed += Time.deltaTime;
            var t = Mathf.Clamp01(elapsed / duration);
            transform.localScale = Vector3.Lerp(Vector3.zero, targetScale, t);

            if (t >= 1f) {
                Destroy(this);
            }
        }
    }
}
