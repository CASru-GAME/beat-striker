using UnityEngine;
using Alice;

namespace Core.LargeWizard {

    public class StunCancelState : StrikerState {
        public override Alice.StrikerStateCategory Category => Alice.StrikerStateCategory.Unknown;
        [SerializeField] StrikerAnimationClip stunCancelAnimationClip;
        [SerializeField] StrikerNode nextNode;
        [SerializeField] float duration = 0.5f;
        [SerializeField] StunGroup stunGroup;

        public override void OnEnter(IStrikerContext context) {
            context.PlayAnimation(stunCancelAnimationClip);

            stunGroup.CancelStun();

            ScheduleStateEvent(duration, context => {
                context.TryTransition(nextNode);
            });
        }
    }
}


