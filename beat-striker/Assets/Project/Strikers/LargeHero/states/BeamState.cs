using Core.Battle;
using UnityEngine;
using Core.Striker;

namespace Core.LargeHero {


    public class BeamState : StrikerState {
        public override Alice.StrikerStateCategory Category => Alice.StrikerStateCategory.Attack;

        // 縺薙・繧ｹ繝・・繝医↓縺・ｋ髢薙∝・逕溘＆繧後ｋ繧｢繝九Γ繝ｼ繧ｷ繝ｧ繝ｳ繧ｯ繝ｪ繝・・
        [SerializeField] private StrikerAnimationClip animationClip;
        [SerializeField] StrikerNode nextNode;

        [SerializeField] GameObject beamPrefab;
        [SerializeField] Transform firePosition;
        [SerializeField] float fireTime = 0.3f;
        [SerializeField] AudioClip audioClip;
        [SerializeField] AudioClip missAudioClip;

        // 縺薙・繧ｹ繝・・繝医↓驕ｷ遘ｻ縺励◆逶ｴ蠕後↓蜻ｼ縺ｰ繧後ｋ
        public override void OnEnter(IStrikerContext context) {
            Debug.Log("BeamState: OnEnter");
            // 繧｢繝九Γ繝ｼ繧ｷ繝ｧ繝ｳ縺ｮ蜀咲函繧帝幕蟋九☆繧・
            context.PlayAnimation(animationClip, context => {
                context.TryTransition(nextNode);
            });

            ScheduleStateEvent(fireTime, context => {
                var swingAudioClip = missAudioClip ? missAudioClip : audioClip;
                var particleInstance =
                Instantiate(beamPrefab, firePosition.position, GetBeamRotation(context));
                AudioSource.PlayClipAtPoint(swingAudioClip, firePosition.position);
            });
        }

        Quaternion GetBeamRotation(IStrikerStateContext context) {
            var opponentPosition = context.GetOpponent().CenterPosition.CurrentValue;
            var firePositionWorld = firePosition.position;
            var directionToOpponent = opponentPosition - firePositionWorld;
            directionToOpponent.y = 0f;

            if (directionToOpponent.sqrMagnitude <= 0.0001f) {
                return firePosition.rotation;
            }

            return Quaternion.LookRotation(directionToOpponent.normalized, Vector3.up);
        }

        // 縺薙・繧ｹ繝・・繝医↓縺・ｋ髢薙∵ｯ弱ヵ繝ｬ繝ｼ繝蜻ｼ縺ｰ繧後ｋ
        public override void OnUpdate(IStrikerStateContext context) {
        }

        // 莉悶・繧ｹ繝・・繝医↓驕ｷ遘ｻ縺吶ｋ逶ｴ蜑阪↓蜻ｼ縺ｰ繧後ｋ
        public override void OnExit(IStrikerContext context) {
            Debug.Log("BeamState: OnExit");
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


