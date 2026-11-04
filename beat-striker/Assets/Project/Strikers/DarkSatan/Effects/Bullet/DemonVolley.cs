using Alice;
using UnityEngine;

namespace Core.DarkSatan {
    public sealed class DemonVolley : MonoBehaviour {
        [SerializeField] Transform[] bullets;
        [SerializeField] DemonNoteBulletVisual[] bulletVisuals;
        [SerializeField] ParticleSystem explosionPrefab;
        [SerializeField] AudioClip summonSound;
        [SerializeField] AudioClip launchSound;
        [SerializeField] AudioClip impactSound;
        [SerializeField] float impactSoundInterval = 0.08f;
        [SerializeField] float cameraImpact = 9f;
        [SerializeField, Min(0f)] float spreadRadius = 3f;
        [SerializeField, Min(0.01f)] float spreadSeconds = 0.25f;
        [SerializeField, Min(0f)] float spreadExpansionSpeed = 0.05f;
        [SerializeField] float speed = 50f;
        [SerializeField] float flightSeconds = 1f;
        [SerializeField] float hitRadius = 0.2f;
        [SerializeField] float damage = 3f;
        [SerializeField] float knockbackSpeed = 12f;
        [SerializeField] float explosionLifetime = 2f;

        IStrikerContext context;
        DemonChargeAttackState owner;
        Vector3 origin;
        Vector3[] directions;
        bool[] active;
        float[] launchDelays;
        float elapsed;
        bool fired;
        public float SpreadSeconds => spreadSeconds;
        public int BulletCount => bullets.Length;
        public bool IsActive(int index) => active[index];
        public Vector3 BulletPosition(int index) => bullets[index].position;

        public void Begin(IStrikerContext strikerContext, DemonChargeAttackState attackOwner) {
            context = strikerContext;
            owner = attackOwner;
            origin = transform.position;
            summonSound.PlayAtApp(origin);
            directions = new Vector3[bullets.Length];
            active = new bool[bullets.Length];
            launchDelays = new float[bullets.Length];
            var target = context.GetOpponent().CenterPosition.CurrentValue;
            for (int i = 0; i < bullets.Length; i++) {
                var spreadPosition = GetSpreadPosition(i, spreadRadius);
                if (IsBlockedByStage(spreadPosition)) {
                    bullets[i].gameObject.SetActive(false);
                    continue;
                }
                active[i] = true;
                bulletVisuals[i].SelectRandom();
                var toTarget = target - spreadPosition;
                directions[i] = toTarget.sqrMagnitude > 0.000001f
                    ? toTarget.normalized : context.Rigidbody.transform.forward;
                bullets[i].rotation = Quaternion.LookRotation(directions[i]);
            }
        }

        public void PrepareLaunch() {
            transform.SetPositionAndRotation(origin, Quaternion.identity);
            SetSpreadPositions(GetWaitingRadius());
        }

        public void SetLaunchDelay(int index, float delay) => launchDelays[index] = delay;

        public void Fire(bool playSound) {
            if (playSound) launchSound.PlayAtApp(origin);
            elapsed = 0f;
            fired = true;
        }

        void SetSpreadPositions(float spread) {
            for (int i = 0; i < bullets.Length; i++) {
                if (!active[i]) continue;
                MoveAndDetect(i, GetSpreadPosition(i, spread));
            }
        }

        float GetWaitingRadius() => spreadRadius * Mathf.Clamp01(elapsed / spreadSeconds)
            + spreadExpansionSpeed * Mathf.Max(0f, elapsed - spreadSeconds);

        Vector3 GetSpreadPosition(int index, float spread) {
            float angle = 2f * Mathf.PI * index / bullets.Length;
            return origin + spread * new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
        }

        bool IsBlockedByStage(Vector3 destination) {
            int stageMask = LayerMask.GetMask("Wall");
            if (Physics.CheckSphere(origin, hitRadius, stageMask, QueryTriggerInteraction.Ignore)
                || Physics.CheckSphere(destination, hitRadius, stageMask, QueryTriggerInteraction.Ignore)) return true;
            var delta = destination - origin;
            return delta.sqrMagnitude > 0f && Physics.SphereCast(
                origin, hitRadius, delta.normalized, out _, delta.magnitude, stageMask, QueryTriggerInteraction.Ignore);
        }

        bool IsTarget(Collider collider) {
            if (collider.transform.IsChildOf(context.Rigidbody.transform)) return false;
            var body = collider.attachedRigidbody;
            if (body != null && body.transform.IsChildOf(context.Rigidbody.transform)) return false;
            return true;
        }

        void MoveAndDetect(int index, Vector3 destination) {
            var position = bullets[index].position;
            var delta = destination - position;
            var direction = delta.sqrMagnitude > 0.000001f ? delta.normalized : directions[index];
            Collider target = null;
            Vector3 hitPosition = position;
            float nearest = float.PositiveInfinity;
            foreach (var collider in Physics.OverlapSphere(position, hitRadius, ~0, QueryTriggerInteraction.Collide)) {
                if (!IsTarget(collider)) continue;
                target = collider;
                hitPosition = collider.ClosestPoint(position);
                nearest = 0f;
                break;
            }
            if (nearest > 0f && delta.sqrMagnitude > 0f) {
                foreach (var hit in Physics.SphereCastAll(position, hitRadius, direction, delta.magnitude, ~0, QueryTriggerInteraction.Collide)) {
                    if (hit.distance >= nearest || !IsTarget(hit.collider)) continue;
                    target = hit.collider;
                    nearest = hit.distance;
                    hitPosition = hit.distance <= 0f ? hit.collider.ClosestPoint(position) : hit.point;
                }
            }
            if (target == null) {
                bullets[index].position = destination;
                return;
            }
            if (target.TryGetComponent<Hurtbox>(out var hurtbox)) {
                hurtbox.GiveHit(new HitStatus(damage, direction * knockbackSpeed));
            }
            owner.ReportImpact(context, hitPosition, impactSound, impactSoundInterval, cameraImpact);
            var explosion = Instantiate(explosionPrefab, hitPosition, Quaternion.identity);
            Destroy(explosion.gameObject, explosionLifetime);
            active[index] = false;
            bullets[index].gameObject.SetActive(false);
        }

        bool AnyActive() {
            foreach (var bulletActive in active) if (bulletActive) return true;
            return false;
        }

        void Update() {
            if (context == null) return;
            transform.SetPositionAndRotation(origin, Quaternion.identity);
            if (!fired) {
                elapsed += Time.deltaTime;
                SetSpreadPositions(GetWaitingRadius());
                if (!AnyActive()) { owner.ForgetVolley(this); Destroy(gameObject); }
                return;
            }

            float previousElapsed = elapsed;
            elapsed += Time.deltaTime;
            bool anyActive = false;
            for (int i = 0; i < bullets.Length; i++) {
                if (!active[i]) continue;
                if (elapsed < launchDelays[i]) {
                    MoveAndDetect(i, bullets[i].position);
                    anyActive |= active[i];
                    continue;
                }
                float stepTime = Mathf.Max(0f, Mathf.Min(elapsed, launchDelays[i] + flightSeconds) - Mathf.Max(previousElapsed, launchDelays[i]));
                var position = bullets[i].position;
                var direction = directions[i];
                var distance = speed * stepTime;
                MoveAndDetect(i, position + direction * distance);
                if (active[i] && elapsed < launchDelays[i] + flightSeconds) {
                    anyActive = true;
                } else if (active[i]) {
                    active[i] = false;
                    bullets[i].gameObject.SetActive(false);
                }
            }
            if (!anyActive) Destroy(gameObject);
        }
    }
}
