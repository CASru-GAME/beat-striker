using Core.Battle;
using UnityEngine;
using Core.Striker;

namespace Core.LargeWizard {


    public class Attack2State : StrikerState {
        public override Alice.StrikerStateCategory Category => Alice.StrikerStateCategory.Attack;

        // 縺薙・繧ｹ繝・・繝医↓縺・ｋ髢薙∝・逕溘＆繧後ｋ繧｢繝九Γ繝ｼ繧ｷ繝ｧ繝ｳ繧ｯ繝ｪ繝・・
        [SerializeField] private StrikerAnimationClip animationClip;
        [SerializeField] StrikerNode nextNode;

        [Header("Ice attack settings")]
        [SerializeField] GameObject icePrefab;             // 蝨ｰ髱｢縺九ｉ逕溘∴繧区ｰｷ縺ｮ繝励Ξ繝上ヶ
        [SerializeField] AudioClip audioClip1;             // 豌ｷ逕滓・譎ゅ・髻ｳ
        [SerializeField] AudioClip audioClip2;             // 豌ｷ縺梧判謦・愛螳壹ｒ逋ｺ逕溘＆縺帙ｋ縺ｨ縺阪・髻ｳ
        [SerializeField] LayerMask groundMask;             // 蝨ｰ髱｢繝ｬ繧､繝､繝ｼ
        [SerializeField] float fireTime = 0.3f;           // 豌ｷ繧堤函謌舌☆繧九ち繧､繝溘Φ繧ｰ・育ｧ抵ｼ・
        [SerializeField] float groundRayStartHeight = 20f; // 蝨ｰ髱｢謗｢邏｢繝ｬ繧､縺ｮ髢句ｧ矩ｫ倥＆
        [SerializeField] float groundRayDistance = 50f;    // 蝨ｰ髱｢謗｢邏｢繝ｬ繧､縺ｮ髟ｷ縺・

        // 縺薙・繧ｹ繝・・繝医↓驕ｷ遘ｻ縺励◆逶ｴ蠕後↓蜻ｼ縺ｰ繧後ｋ
        public override void OnEnter(IStrikerContext context) {
            // 繧｢繝九Γ繝ｼ繧ｷ繝ｧ繝ｳ縺ｮ蜀咲函繧帝幕蟋九☆繧・
            context.PlayAnimation(animationClip, OnAnimationEnd);

            ScheduleStateEvent(fireTime, ctx => {
                // 逶ｸ謇九・StrikerHub繧定ｦ九▽縺代※雜ｳ蜈・↓豌ｷ繧堤函謌舌☆繧・
                var opponent = FindOpponent(ctx.Rigidbody.transform);
                if (opponent == null) return;

                var opponentPos = opponent.transform.position;
                var spawnPos = opponentPos;
                spawnPos.y = 315f;

                var ice = Instantiate(icePrefab, spawnPos, Quaternion.identity);
                var iceBehavior = ice.GetComponent<Ice>();
                iceBehavior.SetAttackerPosition(ctx.Rigidbody.transform.position);
                iceBehavior.SetAttackerRoot(ctx.Rigidbody.transform.root);

                AudioSource.PlayClipAtPoint(audioClip1, spawnPos);
                AudioSource.PlayClipAtPoint(audioClip2, spawnPos);
            });
        }

        /// <summary>
        /// 繧ｷ繝ｼ繝ｳ蜀・・ StrikerHub 縺九ｉ閾ｪ蛻・ｻ･螟厄ｼ茨ｼ晉嶌謇具ｼ峨ｒ霑斐☆
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

        // 縺薙・繧ｹ繝・・繝医↓縺・ｋ髢薙∵ｯ弱ヵ繝ｬ繝ｼ繝蜻ｼ縺ｰ繧後ｋ
        public override void OnUpdate(IStrikerStateContext context) {
        }

        // 莉悶・繧ｹ繝・・繝医↓驕ｷ遘ｻ縺吶ｋ逶ｴ蜑阪↓蜻ｼ縺ｰ繧後ｋ
        public override void OnExit(IStrikerContext context)
        {
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
}


