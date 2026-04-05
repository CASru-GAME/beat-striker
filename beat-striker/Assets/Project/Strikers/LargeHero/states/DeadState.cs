using Core.Battle;
using UnityEngine;
using Core.Striker;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Core.LargeHero {
    
    public class DeadState : StrikerState {

        // このステートにいる間、再生されるアニメーションクリップ
        [SerializeField] private StrikerAnimationClip animationClip;
        [SerializeField] private bool playSecondarySimultaneously = true;
        [SerializeField] private Animator secondaryAnimator;
        [SerializeField] private AnimationClip secondaryAnimationClip;
        [SerializeField] private float secondaryAnimationSpeed = 1f;

        private PlayableGraph secondaryPlayableGraph;

        // このステートに遷移した直後に呼ばれる
        public override void OnEnter(IStrikerContext context) {
            context.PlayAnimation(animationClip);

            if (playSecondarySimultaneously) {
                PlaySecondaryAnimation();
            }
        }

        // このステートにいる間、毎フレーム呼ばれる
        public override void OnUpdate(IStrikerStateContext context) {
        }

        // 他のステートに遷移する直前に呼ばれる
        public override void OnExit(IStrikerContext context) {
            if (secondaryPlayableGraph.IsValid()) {
                secondaryPlayableGraph.Destroy();
            }
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

        private void PlaySecondaryAnimation() {
            if (secondaryPlayableGraph.IsValid()) {
                secondaryPlayableGraph.Destroy();
            }

            secondaryPlayableGraph = PlayableGraph.Create("DeadStateSecondaryAnimation");
            secondaryPlayableGraph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);

            var output = AnimationPlayableOutput.Create(secondaryPlayableGraph, "DeadStateSecondaryOutput", secondaryAnimator);
            var clipPlayable = AnimationClipPlayable.Create(secondaryPlayableGraph, secondaryAnimationClip);
            clipPlayable.SetSpeed(secondaryAnimationSpeed);
            output.SetSourcePlayable(clipPlayable);

            secondaryPlayableGraph.Play();
        }

    }
}
