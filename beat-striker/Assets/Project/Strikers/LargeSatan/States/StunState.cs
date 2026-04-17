using UnityEngine;
using Alice;

namespace Core.LargeSatan {



    public class StunState : StrikerState {
        public override Alice.StrikerStateCategory Category => Alice.StrikerStateCategory.Unknown;

        [SerializeField] StrikerNode nextNode,cancelNode;
        [SerializeField] ParticleSystem stunEffect;
        [SerializeField] float stunDuration = 0.5f;
        [SerializeField] StunGroup stunGroup;

        public override void OnEnter(IStrikerContext context) {
            var stunInverseDirection = -context.Rigidbody.linearVelocity.normalized;
            stunGroup.PlayAnimation(context, stunInverseDirection);
            stunEffect.Play();

            if(stunGroup.IsCancelled) {
                ScheduleStateEvent(stunDuration, context => {
                    context.TryTransition(nextNode);
                });
                return;
            }
        }

        public override void OnAttackRequested(IStrikerStateContext hub) {
            if(stunGroup.IsCancelled) return;
            hub.PreventGroup();
            hub.TryTransition(cancelNode);
        }
        public override void OnChargeRequested(IStrikerStateContext hub) {
            if(stunGroup.IsCancelled) return;
            hub.PreventGroup();
            hub.TryTransition(cancelNode);
        }
        public override void OnGuardRequested(IStrikerStateContext hub) {
            if(stunGroup.IsCancelled) return;
            hub.PreventGroup();
            hub.TryTransition(cancelNode);
        }
        public override void OnDashRequested(IStrikerStateContext hub) {
            if(stunGroup.IsCancelled) return;
            hub.PreventGroup();
            hub.TryTransition(cancelNode);
        }

        public override void OnExit(IStrikerContext hub) {
            stunEffect.Stop();
        }
    }
}


