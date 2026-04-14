using Core.Battle;
using UnityEngine;
using Core.Striker;

namespace Core.LargeWizard {


    public class Attack3State : StrikerState {
        public override Alice.StrikerStateCategory Category => Alice.StrikerStateCategory.Attack;

        // 縺薙・繧ケ繝・・繝医↓縺・ｋ髢薙∝・逕溘＆繧後ｋ繧「繝九Γ繝シ繧キ繝ァ繝ウ繧ッ繝ェ繝・・
        [SerializeField] private StrikerAnimationClip animationClip;

         [SerializeField] StrikerNode nextNode;

        [Header("Rock attack settings")]
        [SerializeField] GameObject rockPrefab;             // 鬆ュ荳翫↓逕滓・縺吶ｋ蟯ゥ縺ョ繝励Ξ繝上ヶ
        [SerializeField] AudioClip audioClip;             // 蟯ゥ逕滓・譎ゅ・髻ウ
        [SerializeField] float fireTime = 0.3f;           // 蟯ゥ繧堤函謌舌☆繧九ち繧、繝溘Φ繧ー・育ァ抵シ・
        [SerializeField] float spawnHeight = 5f;           // 逶ク謇九・鬆ュ荳翫°繧峨・逕滓・鬮倥＆

        // 縺薙・繧ケ繝・・繝医↓驕キ遘サ縺励◆逶エ蠕後↓蜻シ縺ー繧後ｋ
        public override void OnEnter(IStrikerContext context) {
            // 繧「繝九Γ繝シ繧キ繝ァ繝ウ縺ョ蜀咲函繧帝幕蟋九☆繧・
            context.PlayAnimation(animationClip, OnAnimationEnd);

            ScheduleStateEvent(fireTime, ctx => {
                // 逶ク謇九・StrikerHub繧定ヲ九▽縺代※荳顔ゥコ縺ォ蟯ゥ繧堤函謌舌☆繧・
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


        // 莉悶・繧ケ繝・・繝医↓驕キ遘サ縺吶ｋ逶エ蜑阪↓蜻シ縺ー繧後ｋ
        public override void OnExit(IStrikerContext context) {
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


