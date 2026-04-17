using Core.Battle;
using UnityEngine;
using Core.Striker;

namespace Core.LargeHero {
    
    public class BeamState : StrikerState {
        public override Alice.StrikerStateCategory Category => Alice.StrikerStateCategory.Attack;


        [SerializeField] private StrikerAnimationClip animationClip;
        [SerializeField] StrikerNode nextNode;

        [SerializeField] GameObject beamPrefab;
        [SerializeField] Transform firePosition;
        [SerializeField] float fireTime = 0.3f;
        [SerializeField] AudioClip audioClip;
        [SerializeField] AudioClip missAudioClip;

        public override void OnEnter(IStrikerContext context) {
            Debug.Log("BeamState: OnEnter");
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

        public override void OnUpdate(IStrikerStateContext context) {
        }

        public override void OnExit(IStrikerContext context) {
            Debug.Log("BeamState: OnExit");
        }

        // 攻撃コマンドが押された時に呼ばれる
        public override void OnAttackRequested(IStrikerStateContext context) {
        }

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

        public override void OnMiss(IStrikerStateContext context) {
        }

    }
}
