using UnityEngine;
using Alice;

namespace Core.LargeSatan {



    public class ChargeState : StrikerState {
        public override Alice.StrikerStateCategory Category => Alice.StrikerStateCategory.Charge;

        [SerializeField] private StrikerAnimationClip animationClip;
        [SerializeField] StrikerNode nextNode, emitNode;
        [SerializeField] float speed = 1f;
        Vector3 initialSpeed;

        public override void OnEnter(IStrikerContext context) {
            initialSpeed = speed * context.InputDirection.x * Vector3.right;

            context.PlayAnimation(animationClip, context => {
                context.TryTransition(nextNode);
            });
        }

        public override void OnUpdate(IStrikerStateContext context) {
            context.Rigidbody.linearVelocity = initialSpeed;
        }

        public override void OnExit(IStrikerContext context) {
            context.Rigidbody.linearVelocity = Vector3.zero;
        }

        public override void OnAttackRequested(IStrikerStateContext context) {
            context.PreventGroup();
            context.TryTransition(emitNode);
        }

    }
}


