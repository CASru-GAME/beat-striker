using UnityEngine;
using R3;
using Core.Battle;

namespace Core.LargeWizard {
    [RequireComponent(typeof(Hurtbox))]
    public class Guard : MonoBehaviour {
        Hurtbox hurtbox;
        StrikerHub ownerStrikerHub;
        Rigidbody rb;
        Vector3 originalLocalPosition;
        Vector3 originalLocalScale;
        Vector3 launchVelocity;
        float launchedDamage;
        float knockbackSpeed;
        bool isLaunched;

        void Awake() {
            hurtbox = GetComponent<Hurtbox>();
            rb = GetComponent<Rigidbody>();
            originalLocalPosition = transform.localPosition;
            originalLocalScale = transform.localScale;
            transform.localScale = Vector3.zero;

            hurtbox
                .OnHit
                .Subscribe(_ => {
                    Destroy(gameObject);
                })
                .AddTo(this);

            if (ownerStrikerHub == null) {
                ownerStrikerHub = GetComponentInParent<StrikerHub>();
            }
            ApplyOwnerCollisionIgnore();
        }

        void Update() {
            if (!isLaunched) {
                return;
            }

            if (rb != null) {
                return;
            }

            transform.position += launchVelocity * Time.deltaTime;
        }

        public void SetOwnerStrikerHub(StrikerHub strikerHub) {
            ownerStrikerHub = strikerHub;
            ApplyOwnerCollisionIgnore();
        }

        public bool IsOwnedBy(StrikerHub strikerHub) {
            return ownerStrikerHub == strikerHub;
        }

        public void SpawnAtPositionThenReturn(Vector3 spawnPosition, float returnDurationSeconds) {
            transform.position = spawnPosition;
            StartCoroutine(ReturnToOriginalPositionOverTime(returnDurationSeconds));
        }

        public void LaunchForward(Vector3 forward, float speed, float damage, float knockbackSpeed, float lifetime) {
            var moveDirection = forward;
            if (moveDirection.sqrMagnitude <= 0.0001f) {
                moveDirection = transform.forward;
            }

            moveDirection = moveDirection.normalized;
            transform.SetParent(null, true);
            isLaunched = true;
            launchedDamage = damage;
            this.knockbackSpeed = knockbackSpeed;
            launchVelocity = moveDirection * speed;
            transform.localScale = originalLocalScale;

            if (rb != null) {
                rb.useGravity = false;
                rb.linearVelocity = launchVelocity;
            }

            Destroy(gameObject, lifetime);
        }

        void OnTriggerEnter(Collider other) {
            if (!isLaunched) {
                return;
            }

            if (!other.TryGetComponent<Hurtbox>(out var otherHurtbox)) {
                otherHurtbox = other.GetComponentInParent<Hurtbox>();
                if (otherHurtbox == null) {
                    return;
                }
            }

            if (otherHurtbox == hurtbox) {
                return;
            }

            if (ownerStrikerHub != null) {
                var otherStrikerHub = otherHurtbox.GetComponentInParent<StrikerHub>();
                if (otherStrikerHub == ownerStrikerHub) {
                    return;
                }
            }

            var knockbackDirection = launchVelocity.sqrMagnitude > 0.0001f ? launchVelocity.normalized : transform.forward;
            otherHurtbox.GiveHit(new HitStatus(launchedDamage, knockbackDirection * knockbackSpeed));
            Destroy(gameObject);
        }

        System.Collections.IEnumerator ReturnToOriginalPositionOverTime(float durationSeconds) {
            var elapsed = 0f;
            var startLocalPosition = transform.localPosition;
            var startLocalScale = transform.localScale;

            if (durationSeconds <= 0f) {
                transform.localPosition = originalLocalPosition;
                transform.localScale = originalLocalScale;
                yield break;
            }

            while (elapsed < durationSeconds) {
                if (isLaunched) {
                    yield break;
                }

                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / durationSeconds);
                transform.localPosition = Vector3.Lerp(startLocalPosition, originalLocalPosition, t);
                transform.localScale = Vector3.Lerp(startLocalScale, originalLocalScale, t);
                yield return null;
            }

            if (isLaunched) {
                yield break;
            }

            transform.localPosition = originalLocalPosition;
            transform.localScale = originalLocalScale;
        }

        void ApplyOwnerCollisionIgnore() {
            if (ownerStrikerHub == null) {
                return;
            }

            var guardColliders = GetComponentsInChildren<Collider>(true);
            var ownerColliders = ownerStrikerHub.GetComponentsInChildren<Collider>(true);

            for (var i = 0; i < guardColliders.Length; i++) {
                var guardCollider = guardColliders[i];
                if (guardCollider == null) {
                    continue;
                }

                for (var j = 0; j < ownerColliders.Length; j++) {
                    var ownerCollider = ownerColliders[j];
                    if (ownerCollider == null || ownerCollider == guardCollider) {
                        continue;
                    }

                    Physics.IgnoreCollision(guardCollider, ownerCollider, true);
                }
            }
        }
    }
}

