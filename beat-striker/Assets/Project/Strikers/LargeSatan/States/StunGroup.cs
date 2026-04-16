using UnityEngine;
using Alice;
using System;

namespace Core.LargeSatan {

    public class StunGroup : StrikerGroup {
        [SerializeField] private StrikerAnimationClip normalAnimationClip, forwardAnimationClip, backwardAnimationClip;
        StrikerAnimationClip lastAnimationClip = null;
        bool isCancelled = false;

        public bool IsCancelled => isCancelled;

        public void CancelStun() {
            isCancelled = true;
        }

        public StrikerAnimationClip GetStunCancelAnimation() {
            if (lastAnimationClip == forwardAnimationClip) {
                return backwardAnimationClip;
            }
            else {
                return forwardAnimationClip;
            }
        }

        public void PlayAnimation(IStrikerContext context, Vector3 stunInverseDirection) {
            var lookDirection = context.GetSelf().LookDirection.CurrentValue;
            var cos = Vector3.Dot(lookDirection, stunInverseDirection);

            var animationClip = cos > 0f ? forwardAnimationClip : backwardAnimationClip;
            if (lastAnimationClip == animationClip) {
                animationClip = normalAnimationClip;
            }
            lastAnimationClip = animationClip;

            context.PlayAnimation(animationClip);
        }

        // このグループに入った時に呼ばれる（前のステートがこのグループに所属していなかった場合）
        public override void OnEnter(IStrikerContext context) {
            isCancelled = false;
            lastAnimationClip = null;
        }

        // このグループから出る時に呼ばれる（次のステートがこのグループに所属していない場合）
        public override void OnExit(IStrikerContext context) {
            isCancelled = false;
            lastAnimationClip = null;
        }

    }
}
