using Core.Battle;
using UnityEngine;
using Core.Striker;

namespace Core.LargeWizard {


    public class Attack3State : StrikerState {
        public override Alice.StrikerStateCategory Category => Alice.StrikerStateCategory.Attack;

        // 縺薙・繧ｹ繝・・繝医↓縺・ｋ髢薙∝・逕溘＆繧後ｋ繧｢繝九Γ繝ｼ繧ｷ繝ｧ繝ｳ繧ｯ繝ｪ繝・・
        [SerializeField] private StrikerAnimationClip animationClip;

         [SerializeField] StrikerNode nextNode;

        [Header("Rock attack settings")]
        [SerializeField] GameObject rockPrefab;             // 鬆ｭ荳翫↓逕滓・縺吶ｋ蟯ｩ縺ｮ繝励Ξ繝上ヶ
        [SerializeField] AudioClip audioClip;             // 蟯ｩ逕滓・譎ゅ・髻ｳ
        [SerializeField] float fireTime = 0.3f;           // 蟯ｩ繧堤函謌舌☆繧九ち繧､繝溘Φ繧ｰ・育ｧ抵ｼ・
        [SerializeField] float spawnHeight = 5f;           // 逶ｸ謇九・鬆ｭ荳翫°繧峨・逕滓・鬮倥＆

        // 縺薙・繧ｹ繝・・繝医↓驕ｷ遘ｻ縺励◆逶ｴ蠕後↓蜻ｼ縺ｰ繧後ｋ
        public override void OnEnter(IStrikerContext context) {
            // 繧｢繝九Γ繝ｼ繧ｷ繝ｧ繝ｳ縺ｮ蜀咲函繧帝幕蟋九☆繧・
            context.PlayAnimation(animationClip, OnAnimationEnd);

            ScheduleStateEvent(fireTime, ctx => {
                // 逶ｸ謇九・StrikerHub繧定ｦ九▽縺代※荳顔ｩｺ縺ｫ蟯ｩ繧堤函謌舌☆繧・
                var opponent = FindOpponent(ctx.Rigidbody.transform);
                if (opponent == null) return;

                var spawnPos = opponent.transform.position + Vector3.up * spawnHeight;
                var rock = Instantiate(rockPrefab, spawnPos, Quaternion.identity);
                var rockBehavior = rock.GetComponent<Rock>();
                rockBehavior.SetAttackerPosition(ctx.Rigidbody.transform.position);
                rockBehavior.SetAttackerRoot(ctx.Rigidbody.transform.root);
                AudioSource.PlayClipAtPoint(audioClip, rockPrefab.transform.position);
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
}


