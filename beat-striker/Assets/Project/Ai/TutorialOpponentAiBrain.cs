using UnityEngine;

namespace Alice {
    public sealed class TutorialOpponentAiBrain : AiBrain {
        [SerializeField, Min(0f)] float returnDistance = 10f;
        [SerializeField, Min(0f)] float arrivalDistance = 3.5f;

        float spawnX;
        bool isReturning;

        // The brain is instantiated under the player's spawn anchor at deployment.
        void Awake() => spawnX = transform.parent.position.x;

        protected override AiAction OnGoodWindow(AiObservation observation) {
            if (observation.Self is not IStrikerTutorialSource self || observation.Self.HitPoint.CurrentValue <= 0f) {
                return AiAction.None;
            }

            var offset = spawnX - observation.Self.Position.CurrentValue.x;
            var distance = Mathf.Abs(offset);
            isReturning = isReturning ? distance > arrivalDistance : distance >= returnDistance;
            if (!isReturning || !self.IsTutorialGrounded) return AiAction.None;

            // West recovers from stun without attacking. Dash after locomotion resumes.
            if (self.ObservedState is Core.DarkSatan.StunState { ObservationAutoRecovers: false }) {
                return new AiAction(Vector2.zero, GamePadButton.West);
            }

            return self.ObservedState?.Category == StrikerStateCategory.Idle
                ? new AiAction(new Vector2(Mathf.Sign(offset), 0f), GamePadButton.West)
                : AiAction.None;
        }
    }
}
