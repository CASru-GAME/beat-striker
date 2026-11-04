using System;
using System.Collections.Generic;
using Alice;
using UnityEngine;

namespace Core.LargeSatan {
    public sealed class WarriorGateVolley : MonoBehaviour {
        [Serializable]
        public sealed record Gate {
            public Transform Circle;
            public Transform Arrow;
            public TrailRenderer Trail;
        }

        [SerializeField] Gate[] gates;
        [SerializeField] GameObject impactPrefab;
        [SerializeField] AudioClip summonSound;
        [SerializeField] AudioClip launchSound;
        [SerializeField] AudioClip impactSound;
        [SerializeField, Min(0.01f)] float circleOpenSeconds = 0.3f;
        [SerializeField, Min(0f)] float deploymentIntervalSeconds = 0.035f;
        [SerializeField, Min(0f)] float launchDelaySeconds = 1.385f;
        [SerializeField, Min(0.01f)] float arrowEmergenceSeconds = 0.9f;
        [SerializeField, Min(0f)] float arrowEmergenceDistance = 0.35f;
        [SerializeField, Min(0f)] float maxLaunchDelay = 0.315f;
        [SerializeField, Range(0f, 89f)] float sweepHalfAngle = 15f;
        [SerializeField, Min(0.01f)] float sweepSlowAngleRange = 12f;
        [SerializeField, Range(0.01f, 1f)] float sweepMinimumSpeedRatio = 0.6f;
        [SerializeField, Min(0.01f)] float circleCloseSeconds = 0.3f;
        [SerializeField, Min(0.01f)] float speed = 38f;
        [SerializeField, Min(0.01f)] float flightSeconds = 1.6f;
        [SerializeField, Min(0f)] float embeddedSeconds = 1f;
        [SerializeField, Min(0.01f)] float hitRadius = 0.16f;
        [SerializeField, Min(0f)] float damage = 1.6848f;
        [SerializeField, Min(0f)] float knockbackSpeed = 6f;
        [SerializeField] LayerMask wallMask;
        [SerializeField, Min(0f)] float cameraImpactInterval = 0.08f;
        [SerializeField, Min(0f)] float cameraImpact = 3f;
        [SerializeField, Min(0.01f)] float impactLifetime = 2f;

        IStrikerContext context;
        Vector3 origin;
        Vector3 targetAtDeployment;
        Vector3 sweepAxis;
        Vector3 sweepCenterDirection;
        float sweepAngle;
        Quaternion rotation;
        Vector3[] circleScales;
        Vector3[] arrowScales;
        Vector3[] arrowPositions;
        sealed class Projectile {
            public Transform Arrow;
            public TrailRenderer Trail;
            public Vector3 Direction;
            public float StartTime;
            public bool Embedded;
            public float EmbeddedUntil;
            public Transform EmbeddedTarget;
            public Vector3 EmbeddedLocalPosition;
            public Quaternion EmbeddedLocalRotation;
        }
        readonly List<Projectile> projectiles = new();
        int[] shotsFired;
        float[] launchDelays;
        float elapsed;
        float nextCameraImpactTime;
        bool fired;

        const int ShotsPerGate = 6;
        public bool AllShotsLaunched { get; private set; }

        public void CollectProjectileObservations(List<StrikerProjectileObservation> observations) {
            foreach (var projectile in projectiles) {
                if (projectile.Embedded || projectile.Arrow == null) continue;
                observations.Add(new StrikerProjectileObservation(projectile.Arrow.position, projectile.Direction * speed,
                    hitRadius, 0f, Mathf.Max(0f, flightSeconds - (elapsed - projectile.StartTime))));
            }
        }
        float VolleyDuration => maxLaunchDelay * (ShotsPerGate - 1 + (gates.Length > 1 ? 1 : 0));

        public float ReadySeconds => circleOpenSeconds +
            Mathf.Max(0, gates.Length - 1) * deploymentIntervalSeconds +
            Mathf.Max(launchDelaySeconds, arrowEmergenceSeconds +
                Mathf.Max(0, gates.Length - 1) * deploymentIntervalSeconds);

        public void Begin(IStrikerContext strikerContext) {
            context = strikerContext;
            origin = transform.position;
            targetAtDeployment = context.GetOpponent().CenterPosition.CurrentValue;
            var towardTarget = targetAtDeployment - origin;
            rotation = towardTarget.sqrMagnitude > 0.000001f
                ? Quaternion.LookRotation(towardTarget.normalized, Vector3.up)
                : transform.rotation;
            transform.rotation = rotation;
            circleScales = new Vector3[gates.Length];
            arrowScales = new Vector3[gates.Length];
            arrowPositions = new Vector3[gates.Length];
            shotsFired = new int[gates.Length];
            launchDelays = new float[gates.Length];
            for (int i = 0; i < gates.Length; i++) {
                var gate = gates[i];
                circleScales[i] = gate.Circle.localScale;
                arrowScales[i] = gate.Arrow.localScale;
                arrowPositions[i] = gate.Arrow.localPosition;
                gate.Circle.localScale = Vector3.zero;
                gate.Arrow.localScale = Vector3.zero;
                gate.Arrow.gameObject.SetActive(false);
                gate.Trail.emitting = false;
            }
            AimGatesAtDeploymentTarget();
            summonSound.PlayAtApp(origin);
        }

        void AimGatesAtDeploymentTarget() {
            foreach (var gate in gates) {
                var pivot = gate.Circle.parent;
                var direction = targetAtDeployment - pivot.position;
                if (direction.sqrMagnitude > 0.000001f) {
                    pivot.rotation = Quaternion.LookRotation(direction, Vector3.up);
                }
            }
        }

        public void Fire() {
            if (fired) return;
            transform.SetPositionAndRotation(origin, rotation);
            AimGatesAtDeploymentTarget();
            var target = targetAtDeployment;
            sweepCenterDirection = (targetAtDeployment - origin).normalized;
            sweepAxis = Vector3.Cross(sweepCenterDirection, Vector3.up).normalized;
            for (int i = 0; i < gates.Length; i++) {
                var gate = gates[i];
                gate.Circle.localScale = circleScales[i];
                gate.Arrow.localScale = arrowScales[i];
                gate.Arrow.localPosition = arrowPositions[i];
                gate.Trail.Clear();
            }
            var order = new int[gates.Length];
            for (int i = 0; i < order.Length; i++) order[i] = i;
            Array.Sort(order, (a, b) => {
                int comparison = (gates[a].Arrow.position - target).sqrMagnitude.CompareTo(
                    (gates[b].Arrow.position - target).sqrMagnitude);
                return comparison != 0 ? comparison : a.CompareTo(b);
            });
            for (int i = 0; i < order.Length; i++) {
                launchDelays[order[i]] = order.Length > 1 ? maxLaunchDelay * i / (order.Length - 1) : 0f;
            }
            sweepAngle = -sweepHalfAngle;
            fired = true;
            elapsed = 0f;
            context.GenerateImpact(new StrikerImpact(Vector3.down * cameraImpact));
        }

        void Update() {
            if (context == null) return;
            // 所有キャラクターを親にして寿命を共有しつつ、展開位置はワールドに固定する。
            transform.SetPositionAndRotation(origin, rotation);
            float previousElapsed = elapsed;
            elapsed += Time.deltaTime;
            if (!fired) {
                AimGatesAtDeploymentTarget();
                for (int i = 0; i < gates.Length; i++) {
                    var gate = gates[i];
                    float progress = Mathf.Clamp01((elapsed - i * deploymentIntervalSeconds) / circleOpenSeconds);
                    float reveal = Mathf.SmoothStep(0f, 1f, progress);
                    gate.Circle.localScale = circleScales[i] * reveal;
                    // 全ての魔方陣が開いてから、槍を順にせり出す。
                    float emergenceStart = circleOpenSeconds +
                        Mathf.Max(0, gates.Length - 1) * deploymentIntervalSeconds +
                        i * deploymentIntervalSeconds;
                    float emergence = Mathf.SmoothStep(0f, 1f,
                        Mathf.Clamp01((elapsed - emergenceStart) / arrowEmergenceSeconds));
                    if (emergence > 0f) {
                        gate.Arrow.gameObject.SetActive(true);
                        gate.Arrow.localScale = Vector3.Scale(arrowScales[i], new Vector3(1f, 1f, emergence));
                        gate.Arrow.localPosition = arrowPositions[i] - Vector3.forward * (arrowEmergenceDistance * (1f - emergence));
                    }
                }
                return;
            }

            float previousSweepAngle = sweepAngle;
            float sweepDuration = Mathf.Max(0f, Mathf.Min(elapsed, VolleyDuration) - previousElapsed);
            AdvanceSweep(sweepDuration);
            bool pending = false;
            for (int i = 0; i < gates.Length; i++) {
                var gate = gates[i];
                while (shotsFired[i] < ShotsPerGate && elapsed >= launchDelays[i] + shotsFired[i] * maxLaunchDelay) {
                    float startTime = launchDelays[i] + shotsFired[i] * maxLaunchDelay;
                    var arrow = Instantiate(gate.Arrow, gate.Arrow.parent);
                    var trail = arrow.GetChild(gate.Trail.transform.GetSiblingIndex()).GetComponent<TrailRenderer>();
                    arrow.localPosition = arrowPositions[i];
                    arrow.localScale = arrowScales[i];
                    float frameProgress = sweepDuration > 0f
                        ? Mathf.Clamp01((startTime - previousElapsed) / sweepDuration) : 0f;
                    float angle = Mathf.Lerp(previousSweepAngle, sweepAngle, frameProgress);
                    var sweepRay = Quaternion.AngleAxis(angle, sweepAxis) * sweepCenterDirection;
                    float heightDifference = targetAtDeployment.y - origin.y;
                    float distance = sweepRay.y < -0.0001f && heightDifference < 0f
                        ? heightDifference / sweepRay.y : speed * flightSeconds;
                    var target = origin + sweepRay * distance;
                    var toTarget = target - arrow.position;
                    var direction = toTarget.sqrMagnitude > 0.000001f ? toTarget.normalized : transform.forward;
                    arrow.rotation = Quaternion.LookRotation(direction);
                    arrow.gameObject.SetActive(true);
                    launchSound.PlayAtApp(arrow.position);
                    trail.Clear();
                    trail.emitting = true;
                    projectiles.Add(new Projectile { Arrow = arrow, Trail = trail, Direction = direction, StartTime = startTime });
                    gate.Arrow.gameObject.SetActive(false);
                    shotsFired[i]++;
                }
                pending |= shotsFired[i] < ShotsPerGate;
                float closing = elapsed - (launchDelays[i] + (ShotsPerGate - 1) * maxLaunchDelay);
                gate.Circle.localScale = circleScales[i] * Mathf.Clamp01(1f - Mathf.Max(0f, closing) / circleCloseSeconds);
            }
            AllShotsLaunched = !pending;
            for (int i = projectiles.Count - 1; i >= 0; i--) {
                var projectile = projectiles[i];
                if (projectile.Arrow == null) {
                    projectiles.RemoveAt(i);
                    continue;
                }
                if (projectile.Embedded) {
                    if (projectile.EmbeddedTarget != null) {
                        projectile.Arrow.SetPositionAndRotation(
                            projectile.EmbeddedTarget.TransformPoint(projectile.EmbeddedLocalPosition),
                            projectile.EmbeddedTarget.rotation * projectile.EmbeddedLocalRotation);
                    }
                    if (Time.time >= projectile.EmbeddedUntil) {
                        Destroy(projectile.Arrow.gameObject);
                        projectiles.RemoveAt(i);
                    }
                    continue;
                }
                float flightElapsed = elapsed - projectile.StartTime;
                float stepSeconds = Mathf.Max(0f, Mathf.Min(flightElapsed, flightSeconds) - Mathf.Max(0f, previousElapsed - projectile.StartTime));
                if (!MoveAndDetect(projectile, speed * stepSeconds) && flightElapsed >= flightSeconds) {
                    projectile.Trail.emitting = false;
                    Destroy(projectile.Arrow.gameObject);
                    projectiles.RemoveAt(i);
                }
            }
            if (!pending && projectiles.Count == 0 && elapsed >= ShotsPerGate * maxLaunchDelay + circleCloseSeconds) Destroy(gameObject);
        }

        void AdvanceSweep(float duration) {
            // 現在の相手方向との角度差に応じて速度を変え、奥端への到達は強制しない。
            if (VolleyDuration <= 0f) return;
            float sweepAngularSpeed = 2f * sweepHalfAngle / VolleyDuration;
            var opponentDirection = context.GetOpponent().CenterPosition.CurrentValue - origin;
            float opponentAngle = Vector3.SignedAngle(sweepCenterDirection, opponentDirection, sweepAxis);
            while (duration > 0f) {
                float step = Mathf.Min(duration, 1f / 120f);
                float separation = Mathf.Abs(Mathf.DeltaAngle(sweepAngle, opponentAngle));
                float speedRatio = Mathf.Lerp(sweepMinimumSpeedRatio, 1f,
                    Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(separation / sweepSlowAngleRange)));
                sweepAngle = Mathf.Min(sweepHalfAngle, sweepAngle + sweepAngularSpeed * speedRatio * step);
                duration -= step;
            }
        }

        bool IsTarget(Collider collider) {
            if (collider.transform.IsChildOf(context.Rigidbody.transform)) return false;
            var body = collider.attachedRigidbody;
            if (body != null && body.transform.IsChildOf(context.Rigidbody.transform)) return false;
            return (wallMask.value & (1 << collider.gameObject.layer)) != 0 || collider.TryGetComponent<Hurtbox>(out _);
        }

        bool MoveAndDetect(Projectile projectile, float distance) {
            var position = projectile.Arrow.position;
            var direction = projectile.Direction;
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
            if (nearest > 0f && distance > 0f) {
                foreach (var hit in Physics.SphereCastAll(position, hitRadius, direction, distance, ~0, QueryTriggerInteraction.Collide)) {
                    if (hit.distance >= nearest || !IsTarget(hit.collider)) continue;
                    target = hit.collider;
                    nearest = hit.distance;
                    hitPosition = hit.point;
                }
            }
            if (target == null) {
                projectile.Arrow.position = position + direction * distance;
                return false;
            }

            projectile.Arrow.position = position + direction * nearest;
            projectile.EmbeddedTarget = target.transform;
            projectile.EmbeddedLocalPosition = target.transform.InverseTransformPoint(projectile.Arrow.position);
            projectile.EmbeddedLocalRotation = Quaternion.Inverse(target.transform.rotation) * projectile.Arrow.rotation;
            projectile.Trail.emitting = false;
            projectile.Embedded = true;
            projectile.EmbeddedUntil = Time.time + embeddedSeconds;

            if (target.TryGetComponent<Hurtbox>(out var hurtbox)) {
                hurtbox.GiveHit(new HitStatus(damage, direction * knockbackSpeed));
            }
            Destroy(Instantiate(impactPrefab, hitPosition, Quaternion.LookRotation(direction), context.Rigidbody.transform), impactLifetime);
            impactSound.PlayAtApp(hitPosition);
            if (Time.time >= nextCameraImpactTime) {
                context.GenerateImpact(new StrikerImpact(Vector3.down * cameraImpact));
                nextCameraImpactTime = Time.time + cameraImpactInterval;
            }
            return true;
        }

    }
}
