using Core.Battle;
using UnityEngine;
using Core.Striker;

namespace Core.LargeWizard {


    public class IntroState : StrikerState {
        public override Alice.StrikerStateCategory Category => Alice.StrikerStateCategory.Unknown;

        // 縺薙・繧ｹ繝・・繝医↓縺・ｋ髢薙∝・逕溘＆繧後ｋ繧｢繝九Γ繝ｼ繧ｷ繝ｧ繝ｳ繧ｯ繝ｪ繝・・
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

        // 縺薙・繧ｹ繝・・繝医↓驕ｷ遘ｻ縺励◆逶ｴ蠕後↓蜻ｼ縺ｰ繧後ｋ
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

        // 縺薙・繧ｹ繝・・繝医↓縺・ｋ髢薙∵ｯ弱ヵ繝ｬ繝ｼ繝蜻ｼ縺ｰ繧後ｋ
        public override void OnUpdate(IStrikerStateContext context) {
        }

        // 莉悶・繧ｹ繝・・繝医↓驕ｷ遘ｻ縺吶ｋ逶ｴ蜑阪↓蜻ｼ縺ｰ繧後ｋ
        public override void OnExit(IStrikerContext context) {
        }

        // 謾ｻ謦・さ繝槭Φ繝峨′謚ｼ縺輔ｌ縺滓凾縺ｫ蜻ｼ縺ｰ繧後ｋ
        public override void OnAttackRequested(IStrikerStateContext context) {
        }

        // 繝√Ε繝ｼ繧ｸ繧ｳ繝槭Φ繝峨′謚ｼ縺輔ｌ縺滓凾縺ｫ蜻ｼ縺ｰ繧後ｋ
        public override void OnChargeRequested(IStrikerStateContext context) {
        }

        // 繝繝・す繝･繧ｳ繝槭Φ繝峨′謚ｼ縺輔ｌ縺滓凾縺ｫ蜻ｼ縺ｰ繧後ｋ
        public override void OnDashRequested(IStrikerStateContext context) {
        }

        // 繧ｬ繝ｼ繝峨さ繝槭Φ繝峨′謚ｼ縺輔ｌ縺滓凾縺ｫ蜻ｼ縺ｰ繧後ｋ
        public override void OnGuardRequested(IStrikerStateContext context) {
        }

        // 謾ｻ謦・ｒ蜿励￠縺滓凾縺ｫ蜻ｼ縺ｰ繧後ｋ
        public override void OnHit(IStrikerStateContext context, HitStatus status) {
        }

        // 繝溘せ縺励◆譎ゅ↓蜻ｼ縺ｰ繧後ｋ
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


