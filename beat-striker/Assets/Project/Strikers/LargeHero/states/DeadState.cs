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
        [SerializeField] private bool lockSecondaryWorldPosition = true;
        [SerializeField] private bool lockSecondaryWorldScale = true;

        private PlayableGraph secondaryPlayableGraph;
        private Transform secondaryOriginalParent;
        private Vector3 secondaryOriginalLocalPosition;
        private Quaternion secondaryOriginalLocalRotation;
        private Vector3 secondaryOriginalLocalScale;
        private Vector3 secondaryLockedWorldPosition;
        private Vector3 secondaryLockedWorldScale;

        // このステートに遷移した直後に呼ばれる
        public override void OnEnter(IStrikerContext context) {
            if (playSecondarySimultaneously) {
                secondaryOriginalParent = secondaryAnimator.transform.parent;
                secondaryOriginalLocalPosition = secondaryAnimator.transform.localPosition;
                secondaryOriginalLocalRotation = secondaryAnimator.transform.localRotation;
                secondaryOriginalLocalScale = secondaryAnimator.transform.localScale;
                secondaryAnimator.transform.SetParent(null, true);
                secondaryLockedWorldPosition = secondaryAnimator.transform.position;
                secondaryLockedWorldScale = secondaryAnimator.transform.localScale;
                PlaySecondaryAnimation();
            }

            context.PlayAnimation(animationClip);
        }

        // このステートにいる間、毎フレーム呼ばれる
        public override void OnUpdate(IStrikerStateContext context) {
            if (playSecondarySimultaneously && lockSecondaryWorldPosition) {
                secondaryAnimator.transform.position = secondaryLockedWorldPosition;
            }

            if (playSecondarySimultaneously && lockSecondaryWorldScale) {
                secondaryAnimator.transform.localScale = secondaryLockedWorldScale;
            }
        }

        // 他のステートに遷移する直前に呼ばれる
        public override void OnExit(IStrikerContext context) {
            if (secondaryPlayableGraph.IsValid()) {
                secondaryPlayableGraph.Destroy();
            }

            if (playSecondarySimultaneously) {
                secondaryAnimator.transform.SetParent(secondaryOriginalParent, false);
                secondaryAnimator.transform.localPosition = secondaryOriginalLocalPosition;
                secondaryAnimator.transform.localRotation = secondaryOriginalLocalRotation;
                secondaryAnimator.transform.localScale = secondaryOriginalLocalScale;
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
