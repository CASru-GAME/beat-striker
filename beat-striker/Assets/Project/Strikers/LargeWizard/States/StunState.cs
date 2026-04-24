using UnityEngine;

namespace Core.LargeWizard {


    public class StunState : StrikerState {
        public override Alice.StrikerStateCategory Category => Alice.StrikerStateCategory.Unknown;

        [SerializeField] StrikerNode nextNode,cancelNode;
        [SerializeField] StrikerAnimationClip stunAnimationClip;
        [SerializeField] float stunDuration = 0.5f;
        [SerializeField] StunGroup stunGroup;

        public override void OnEnter(IStrikerContext context) {
            context.PlayAnimation(stunAnimationClip);

            if(stunGroup.IsCancelled) {
                ScheduleStateEvent(stunDuration, context => {
                    context.TryTransition(nextNode);
                });
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
        }
    }
}


