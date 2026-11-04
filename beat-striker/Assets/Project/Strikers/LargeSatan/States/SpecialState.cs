using Alice;
using UnityEngine;

namespace Core.LargeSatan {
    public sealed class SpecialState : StrikerState {
        public override Alice.StrikerStateCategory Category => Alice.StrikerStateCategory.Special;

        [SerializeField] StrikerNode nextNode;
        [SerializeField] WarriorGateVolley volleyPrefab;
        [SerializeField] StrikerAnimationClip chargeAnimation;
        [SerializeField] StrikerAnimationClip launchAnimation;
        [SerializeField, Min(0f)] float launchRecoverySeconds = 0.8f;
        [SerializeField, Min(0f)] float ascentHeight = 3f;
        [SerializeField] LayerMask groundMask;
        [SerializeField, Min(0.01f)] float ascentSeconds = 0.792f;
        [SerializeField, Min(0f)] float attentionSeconds = 3.5f;
        [SerializeField, Min(0.0001f)] float attentionZoomSmoothTime = 0.26666668f;
        [SerializeField, Min(0f)] float launchReleaseSeconds = 0.125f;
        [SerializeField] string attentionTechniqueText = "ゲート・オブ・アロー";

        WarriorGateVolley volley;
        bool previousGravity;
        bool fired;
        bool launchStarted;
        bool cameraReturned;
        int activationId;
        float elapsed;
        float ascentElapsed;
        float ascentStartY;
        float ascentTargetY;
        Vector3 centerOffset;

        public override void OnEnter(IStrikerContext context) {
            var toOpponent = context.GetOpponent().Position.CurrentValue - context.Rigidbody.position;
            if (Vector3.Dot(context.Rigidbody.transform.forward, toOpponent) < 0f) {
                context.Rigidbody.rotation *= Quaternion.Euler(0f, 180f, 0f);
            }
            ascentStartY = context.Rigidbody.position.y;
            if (!Physics.Raycast(context.Rigidbody.position + Vector3.up * 0.1f, Vector3.down,
                    out var groundHit, Mathf.Infinity, groundMask, QueryTriggerInteraction.Ignore)) {
                throw new System.InvalidOperationException("Warrior special requires ground below the striker.");
            }
            ascentTargetY = groundHit.point.y + ascentHeight;
            previousGravity = context.Rigidbody.useGravity;
            context.Rigidbody.useGravity = false;
            context.Rigidbody.linearVelocity = Vector3.zero;
            fired = false;
            launchStarted = false;
            cameraReturned = false;
            var currentActivation = ++activationId;
            elapsed = 0f;
            ascentElapsed = 0f;
            volley = null;
            centerOffset = context.GetSelf().CenterPosition.CurrentValue - context.Rigidbody.position;
            context.RequestAttention(new AttentionRequest(attentionSeconds, attentionTechniqueText, () => {
                if (activationId == currentActivation) cameraReturned = true;
            }, attentionZoomSmoothTime));
            context.PlayAnimation(chargeAnimation);
        }

        public override void OnUpdate(IStrikerStateContext context) {
            context.PreventGroup();
            context.Rigidbody.linearVelocity = Vector3.zero;
            elapsed += Time.deltaTime;
            if (!launchStarted) {
                ascentElapsed += Time.deltaTime;
                var position = context.Rigidbody.position;
                position.y = Mathf.Lerp(ascentStartY, ascentTargetY,
                    Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(ascentElapsed / ascentSeconds)));
                context.Rigidbody.position = position;
                if (volley == null) {
                    if (ascentElapsed < ascentSeconds) return;
                    volley = Instantiate(volleyPrefab, context.Rigidbody.position + centerOffset,
                        context.Rigidbody.rotation, context.Rigidbody.transform);
                    volley.Begin(context);
                    elapsed = 0f;
                }
            }
            if (!launchStarted) {
                if (elapsed < volley.ReadySeconds || !cameraReturned) return;
                launchStarted = true;
                elapsed = 0f;
                context.PlayAnimation(launchAnimation);
                return;
            }
            if (!fired && elapsed >= launchReleaseSeconds) {
                fired = true;
                volley.Fire();
            }
            if (fired && volley.AllShotsLaunched && elapsed >= launchRecoverySeconds) context.TryTransition(nextNode);
        }

        public override void OnExit(IStrikerContext context) {
            activationId++;
            // 発射済みの矢は飛行を継続し、発射前に中断された魔法陣は残さない。
            if (!fired && volley != null) Destroy(volley.gameObject);
            volley = null;
            context.Rigidbody.useGravity = previousGravity;
        }
    }
}
