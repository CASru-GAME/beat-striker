using UnityEngine;

namespace Core.LargeHero {
    public class SpecialApproachState : StrikerState {
        public override Alice.StrikerStateCategory Category => Alice.StrikerStateCategory.Special;

        [SerializeField] StrikerAnimationClip animationClip;
        [SerializeField] StrikerNode launchState;
        [SerializeField] StrikerNode groundCancelNode;
        [SerializeField] StrikerNode airCancelNode;
        [SerializeField] GroundChecker groundChecker;
        [SerializeField, Min(0f)] float activationDistance = 1f;
        [SerializeField, Min(0f)] float approachSpeed = 10f;
        [SerializeField, Min(0f)] float maxApproachDuration = 0.5f;

        IStrikerContext activeContext;
        float elapsedTime;
        float originalLinearDamping;
        bool originalUseGravity;
        bool reachedOpponent;
        bool timedOut;
        public float ObservationActivationDistance => activationDistance;
        public float ObservationSpeed => approachSpeed;
        public float ObservationDuration => maxApproachDuration;

        public bool IsOpponentInRange(IStrikerContext context) {
            var opponent = context.GetOpponent();
            return opponent.PlayerId.CurrentValue != context.GetSelf().PlayerId.CurrentValue
                && Vector3.Distance(context.Rigidbody.position, opponent.Position.CurrentValue) <= activationDistance;
        }

        public override void OnEnter(IStrikerContext context) {
            elapsedTime = 0f;
            reachedOpponent = false;
            timedOut = false;
            originalUseGravity = context.Rigidbody.useGravity;
            originalLinearDamping = context.Rigidbody.linearDamping;
            context.Rigidbody.useGravity = false;
            context.Rigidbody.linearDamping = 0f;
            context.Rigidbody.linearVelocity = Vector3.zero;
            context.PlayAnimation(animationClip);
            activeContext = context;
        }

        void FixedUpdate() {
            if (activeContext == null || reachedOpponent || timedOut) {
                return;
            }

            var body = activeContext.Rigidbody;
            if (IsOpponentInRange(activeContext)) {
                reachedOpponent = true;
                body.linearVelocity = Vector3.zero;
                return;
            }

            if (elapsedTime >= maxApproachDuration) {
                timedOut = true;
                body.linearVelocity = Vector3.zero;
                return;
            }

            var toOpponent = activeContext.GetOpponent().Position.CurrentValue - body.position;
            toOpponent.z = 0f;
            if (toOpponent.x != 0f) {
                body.rotation = Quaternion.LookRotation(toOpponent.x > 0f ? Vector3.right : Vector3.left);
            }

            // 最後の物理フレームも制限時間内の移動量に収める。
            var stepDuration = Mathf.Min(Time.fixedDeltaTime, maxApproachDuration - elapsedTime);
            body.linearVelocity = toOpponent.normalized * (approachSpeed * stepDuration / Time.fixedDeltaTime);
            elapsedTime = Mathf.Min(elapsedTime + stepDuration, maxApproachDuration);
        }

        public override void OnUpdate(IStrikerStateContext context) {
            if (reachedOpponent || (!timedOut && IsOpponentInRange(context))) {
                context.TryTransition(launchState);
                return;
            }

            if (timedOut || elapsedTime >= maxApproachDuration) {
                context.TryTransition(groundChecker.IsGrounded ? groundCancelNode : airCancelNode);
            }
        }

        public override void OnHit(IStrikerStateContext context, HitStatus status) {
            context.PreventGroup();
        }

        public override void OnExit(IStrikerContext context) {
            activeContext = null;
            context.Rigidbody.linearVelocity = Vector3.zero;
            context.Rigidbody.useGravity = originalUseGravity;
            context.Rigidbody.linearDamping = originalLinearDamping;
        }
    }
}
