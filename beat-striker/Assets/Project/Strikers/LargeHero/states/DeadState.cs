using Core.Battle;
using UnityEngine;
using Core.Striker;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Core.LargeHero {


    public class DeadState : StrikerState {
        public override Alice.StrikerStateCategory Category => Alice.StrikerStateCategory.Unknown;

        // 縺薙・繧ケ繝・・繝医↓縺・ｋ髢薙∝・逕溘＆繧後ｋ繧「繝九Γ繝シ繧キ繝ァ繝ウ繧ッ繝ェ繝・・
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

        // 縺薙・繧ケ繝・・繝医↓驕キ遘サ縺励◆逶エ蠕後↓蜻シ縺ー繧後ｋ
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

        // 縺薙・繧ケ繝・・繝医↓縺・ｋ髢薙∵ッ弱ヵ繝ャ繝シ繝蜻シ縺ー繧後ｋ
        public override void OnUpdate(IStrikerStateContext context) {
            if (playSecondarySimultaneously && lockSecondaryWorldPosition) {
                secondaryAnimator.transform.position = secondaryLockedWorldPosition;
            }

            if (playSecondarySimultaneously && lockSecondaryWorldScale) {
                secondaryAnimator.transform.localScale = secondaryLockedWorldScale;
            }
        }

        // 莉悶・繧ケ繝・・繝医↓驕キ遘サ縺吶ｋ逶エ蜑阪↓蜻シ縺ー繧後ｋ
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


