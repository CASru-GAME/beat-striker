using Core.Battle;
using UnityEngine;
using Core.Striker;

namespace Core.LargeWizard {


    public class Attack2State : StrikerState {
        public override Alice.StrikerStateCategory Category => Alice.StrikerStateCategory.Attack;

        // 縺薙・繧ケ繝・・繝医↓縺・ｋ髢薙∝・逕溘＆繧後ｋ繧「繝九Γ繝シ繧キ繝ァ繝ウ繧ッ繝ェ繝・・
        [SerializeField] private StrikerAnimationClip animationClip;
        [SerializeField] StrikerNode nextNode;

        [Header("Ice attack settings")]
        [SerializeField] GameObject icePrefab;             // 蝨ー髱「縺九ｉ逕溘∴繧区ーキ縺ョ繝励Ξ繝上ヶ
        [SerializeField] AudioClip audioClip1;             // 豌キ逕滓・譎ゅ・髻ウ
        [SerializeField] AudioClip audioClip2;             // 豌キ縺梧判謦・愛螳壹ｒ逋コ逕溘＆縺帙ｋ縺ィ縺阪・髻ウ
        [SerializeField] LayerMask groundMask;             // 蝨ー髱「繝ャ繧、繝、繝シ
        [SerializeField] float fireTime = 0.3f;           // 豌キ繧堤函謌舌☆繧九ち繧、繝溘Φ繧ー・育ァ抵シ・
        [SerializeField] float groundRayStartHeight = 20f; // 蝨ー髱「謗「邏「繝ャ繧、縺ョ髢句ァ矩ォ倥＆
        [SerializeField] float groundRayDistance = 50f;    // 蝨ー髱「謗「邏「繝ャ繧、縺ョ髟キ縺・

        // 縺薙・繧ケ繝・・繝医↓驕キ遘サ縺励◆逶エ蠕後↓蜻シ縺ー繧後ｋ
        public override void OnEnter(IStrikerContext context) {
            // 繧「繝九Γ繝シ繧キ繝ァ繝ウ縺ョ蜀咲函繧帝幕蟋九☆繧・
            context.PlayAnimation(animationClip, OnAnimationEnd);

            ScheduleStateEvent(fireTime, ctx => {
                // 逶ク謇九・StrikerHub繧定ヲ九▽縺代※雜ウ蜈・↓豌キ繧堤函謌舌☆繧・
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
        /// 繧キ繝シ繝ウ蜀・・ StrikerHub 縺九ｉ閾ェ蛻・サ・螟厄シ茨シ晉嶌謇具シ峨ｒ霑斐☆
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

        // 縺薙・繧ケ繝・・繝医↓縺・ｋ髢薙∵ッ弱ヵ繝ャ繝シ繝蜻シ縺ー繧後ｋ
        public override void OnUpdate(IStrikerStateContext context) {
        }

        // 莉悶・繧ケ繝・・繝医↓驕キ遘サ縺吶ｋ逶エ蜑阪↓蜻シ縺ー繧後ｋ
        public override void OnExit(IStrikerContext context)
        {
        }

        // 謾サ謦・さ繝槭Φ繝峨′謚シ縺輔ｌ縺滓凾縺ォ蜻シ縺ー繧後ｋ
        public override void OnAttackRequested(IStrikerStateContext context) {
        }

        // 繝√Ε繝シ繧ク繧ウ繝槭Φ繝峨′謚シ縺輔ｌ縺滓凾縺ォ蜻シ縺ー繧後ｋ
        public override void OnChargeRequested(IStrikerStateContext context) {
        }

        // 繝繝・す繝・繧ウ繝槭Φ繝峨′謚シ縺輔ｌ縺滓凾縺ォ蜻シ縺ー繧後ｋ
        public override void OnDashRequested(IStrikerStateContext context) {
        }

        // 繧ャ繝シ繝峨さ繝槭Φ繝峨′謚シ縺輔ｌ縺滓凾縺ォ蜻シ縺ー繧後ｋ
        public override void OnGuardRequested(IStrikerStateContext context) {
        }

        // 謾サ謦・ｒ蜿励¢縺滓凾縺ォ蜻シ縺ー繧後ｋ
        public override void OnHit(IStrikerStateContext context, HitStatus status) {
        }

        // 繝溘せ縺励◆譎ゅ↓蜻シ縺ー繧後ｋ
        public override void OnMiss(IStrikerStateContext context) {
        }

    }
}


