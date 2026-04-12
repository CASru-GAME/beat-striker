using UnityEngine;
using Alice;

namespace Core.LargeSatan {


    public class AirJumpState : StrikerState {
        public override Alice.StrikerStateCategory Category => Alice.StrikerStateCategory.Dash;

        // 縺薙・繧ケ繝・・繝医↓縺・ｋ髢薙∝・逕溘＆繧後ｋ繧「繝九Γ繝シ繧キ繝ァ繝ウ繧ッ繝ェ繝・・
        [SerializeField] private StrikerAnimationClip fowardClip, backwardClip;
        [SerializeField] StrikerNode fallNode;
        [SerializeField] float jumpSpeed;
        [SerializeField] float duration = 0.5f;
        [SerializeField] float downwardAcceleration = 30f;
        Vector3 initialVelocity;
        float elapsedTime;
        bool previousUseGravity;

        // 縺薙・繧ケ繝・・繝医↓驕キ遘サ縺励◆逶エ蠕後↓蜻シ縺ー繧後ｋ
        public override void OnEnter(IStrikerContext context) {
            var direction = context.InputDirection == Vector2.zero ? Vector2.up : context.InputDirection;
            var requestedDirection = context.LocalInputDirection == Vector2.zero ? Vector2.up : context.LocalInputDirection;
            // 繧「繝九Γ繝シ繧キ繝ァ繝ウ縺ョ蜀咲函繧帝幕蟋九☆繧・
            StrikerAnimationClip clip;
            if(requestedDirection.x < 0) {
                clip = backwardClip;
            } else {
                clip = fowardClip;
            }
            context.PlayAnimation(clip);
            this.initialVelocity = jumpSpeed * direction;
            this.elapsedTime = 0f;
            this.previousUseGravity = context.Rigidbody.useGravity;
            context.Rigidbody.useGravity = false;
            context.Rigidbody.linearVelocity = this.initialVelocity;

            this.ScheduleStateEvent(duration, context => {
                context.TryTransition(fallNode);
            });
        }

        // 縺薙・繧ケ繝・・繝医↓縺・ｋ髢薙∵ッ弱ヵ繝ャ繝シ繝蜻シ縺ー繧後ｋ
        public override void OnUpdate(IStrikerStateContext context) {
            elapsedTime += Time.deltaTime;
            Vector3 downwardVelocity = Vector3.down * (downwardAcceleration * elapsedTime);
            context.Rigidbody.linearVelocity = this.initialVelocity + downwardVelocity;
        }

        // 莉悶・繧ケ繝・・繝医↓驕キ遘サ縺吶ｋ逶エ蜑阪↓蜻シ縺ー繧後ｋ
        public override void OnExit(IStrikerContext context) {
            context.Rigidbody.useGravity = this.previousUseGravity;
        }
    }
}


