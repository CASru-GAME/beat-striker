using UnityEngine;
using R3;
using Core.Battle;

namespace Core.LargeWizard {
    [RequireComponent(typeof(Hurtbox))]
    [RequireComponent(typeof(Rigidbody))]
    public class Guard : MonoBehaviour {
        [SerializeField] float generatingKnockbackSpeed = 10f, waitingKnockbackSpeed = 10f;
        [SerializeField] LayerMask wallLayerMask;
        Hurtbox hurtbox;
        StrikerHub ownerStrikerHub;
        Rigidbody rb;
        Vector3 originalLocalPosition;
        Vector3 originalLocalScale;
        Vector3 launchVelocity;
        float launchedDamage;
        float launchingKnockbackSpeed;
        bool isLaunched;
        Coroutine moveCoroutine;
        bool isVirgin, isGenerating;

        void Awake() {
            isVirgin = true;
            isGenerating = false;
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

            rb.isKinematic = true;
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
            RestartMoveCoroutine(ReturnToOriginalPositionOverTime(returnDurationSeconds));
        }

        public void MoveToPositionAndFix(Vector3 targetPosition, float moveDurationSeconds) {
            var targetLocalPosition = transform.parent.InverseTransformPoint(targetPosition);
            RestartMoveCoroutine(MoveToTargetAndFixOverTime(targetLocalPosition, moveDurationSeconds));
        }

        public void LaunchForward(Vector3 forward, float speed, float damage, float knockbackSpeed, float lifetime) {
            rb.isKinematic = false;

            var moveDirection = forward;
            if (moveDirection.sqrMagnitude <= 0.0001f) {
                moveDirection = transform.forward;
            }

            moveDirection = moveDirection.normalized;
            transform.SetParent(null, true);
            isLaunched = true;
            launchedDamage = damage;
            this.launchingKnockbackSpeed = knockbackSpeed;
            launchVelocity = moveDirection * speed;
            transform.localScale = originalLocalScale;

            if (rb != null) {
                rb.useGravity = false;
                rb.linearVelocity = launchVelocity;
            }

            Destroy(gameObject, lifetime);
        }


        void OnTriggerEnter(Collider other) {
            if (!other.TryGetComponent<Hurtbox>(out var otherHurtbox) || otherHurtbox == hurtbox) return;

            isVirgin = false;

            TryGetVelocity(out var velocity);

            var knockbackSpeed = isGenerating ? generatingKnockbackSpeed :
                isLaunched ? launchingKnockbackSpeed :
                velocity + waitingKnockbackSpeed;

            var knockbackDirection = isLaunched && launchVelocity.sqrMagnitude > 0.0001f
                ? launchVelocity.normalized
                : transform.forward.normalized;

            if(TryGetPosition(out var position))    {
                var dir = (other.ClosestPoint(position) - position).normalized;
                if (Vector3.Dot(dir, knockbackDirection) < 0.3) {
                    knockbackDirection = dir;
                }
            }
                            
            otherHurtbox.GiveHit(new HitStatus(launchedDamage, knockbackDirection * knockbackSpeed));

            if (isLaunched || (wallLayerMask & (1 << other.gameObject.layer)) != 0) {
                Destroy(gameObject);
            }      
        }

        void OnTriggerStay(Collider other) {
            if(isVirgin) OnTriggerEnter(other);
        }

        bool TryGetPosition(out Vector3 position) {
            if (ownerStrikerHub == null) {
                position = Vector3.zero;
                return false;
            }
            position = ownerStrikerHub.EnsureAliceRuntimeHub().CenterPosition.CurrentValue;
            return true;
        }

        bool TryGetVelocity(out float velocity ) {
            if (ownerStrikerHub == null) {
                velocity = 0;
                return false;
            }
            velocity = Mathf.Max(0, Vector3.Dot(ownerStrikerHub.EnsureAliceRuntimeHub().Velocity.CurrentValue,
                this.transform.forward.normalized));
            return true;
        }

        System.Collections.IEnumerator ReturnToOriginalPositionOverTime(float durationSeconds) {
            var elapsed = 0f;
            var startLocalPosition = transform.localPosition;
            var startLocalScale = transform.localScale;
            isGenerating = true;

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

            isGenerating = false;

            if (isLaunched) {
                yield break;
            }

            transform.localPosition = originalLocalPosition;
            transform.localScale = originalLocalScale;
        }

        System.Collections.IEnumerator MoveToTargetAndFixOverTime(Vector3 targetLocalPosition, float durationSeconds) {
            var elapsed = 0f;
            var startLocalPosition = transform.localPosition;
            var startLocalScale = transform.localScale;

            if (durationSeconds <= 0f) {
                transform.localPosition = targetLocalPosition;
                transform.localScale = originalLocalScale;
                originalLocalPosition = targetLocalPosition;
                yield break;
            }

            while (elapsed < durationSeconds) {
                if (isLaunched) {
                    yield break;
                }

                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / durationSeconds);
                transform.localPosition = Vector3.Lerp(startLocalPosition, targetLocalPosition, t);
                transform.localScale = Vector3.Lerp(startLocalScale, originalLocalScale, t);
                yield return null;
            }

            if (isLaunched) {
                yield break;
            }

            transform.localPosition = targetLocalPosition;
            transform.localScale = originalLocalScale;
            originalLocalPosition = targetLocalPosition;
        }

        void RestartMoveCoroutine(System.Collections.IEnumerator routine) {
            if (moveCoroutine != null) {
                StopCoroutine(moveCoroutine);
            }

            moveCoroutine = StartCoroutine(routine);
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

