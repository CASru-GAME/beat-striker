using UnityEngine;
using Alice;

namespace Core.LargeSatan {

    public class StunCancelState : StrikerState {
        public override Alice.StrikerStateCategory Category => Alice.StrikerStateCategory.Unknown;
        [SerializeField] StrikerNode nextNode;
        [SerializeField] ParticleSystem cancelEffect;
        [SerializeField] float duration = 0.5f;
        [SerializeField] StunGroup stunGroup;

        public override void OnEnter(IStrikerContext context) {
            context.PlayAnimation(stunGroup.GetStunCancelAnimation());
            cancelEffect.Play();

            stunGroup.CancelStun();

            ScheduleStateEvent(duration, context => {
                context.TryTransition(nextNode);
            });
        }
    }
}


