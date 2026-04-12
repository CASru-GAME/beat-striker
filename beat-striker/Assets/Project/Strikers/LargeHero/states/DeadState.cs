using Core.Battle;
using UnityEngine;
using Core.Striker;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Core.LargeHero {


    public class DeadState : StrikerState {
        public override Alice.StrikerStateCategory Category => Alice.StrikerStateCategory.Unknown;

        // 縺薙・繧ｹ繝・・繝医↓縺・ｋ髢薙∝・逕溘＆繧後ｋ繧｢繝九Γ繝ｼ繧ｷ繝ｧ繝ｳ繧ｯ繝ｪ繝・・
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

        // 縺薙・繧ｹ繝・・繝医↓驕ｷ遘ｻ縺励◆逶ｴ蠕後↓蜻ｼ縺ｰ繧後ｋ
        public override void OnEnter(IStrikerContext context) {
            context.PlayAnimation(animationClip);

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
        }

        // 縺薙・繧ｹ繝・・繝医↓縺・ｋ髢薙∵ｯ弱ヵ繝ｬ繝ｼ繝蜻ｼ縺ｰ繧後ｋ
        public override void OnUpdate(IStrikerStateContext context) {
            if (playSecondarySimultaneously && lockSecondaryWorldPosition) {
                secondaryAnimator.transform.position = secondaryLockedWorldPosition;
            }

            if (playSecondarySimultaneously && lockSecondaryWorldScale) {
                secondaryAnimator.transform.localScale = secondaryLockedWorldScale;
            }
        }

        // 莉悶・繧ｹ繝・・繝医↓驕ｷ遘ｻ縺吶ｋ逶ｴ蜑阪↓蜻ｼ縺ｰ繧後ｋ
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


