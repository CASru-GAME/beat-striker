using Core.Battle;
using UnityEngine;
using Core.Striker;

namespace Core.LargeWizard {
    
    [RequireComponent(typeof(Rigidbody))]
    public class beam : MonoBehaviour {
        [SerializeField] float damage = 10f;
        [SerializeField] float nockbackSpeed = 10f;
        [SerializeField] float speed = 20f;
        [SerializeField, Min(0f)] float launchDelay = 0f;
        [SerializeField] Vector3 localMoveDirection = Vector3.forward;
        [SerializeField] public Hurtbox Hurtbox;
        StrikerHub ownerStrikerHub;
        Rigidbody rb;
        Collider hitCollider;
        Renderer[] renderers;
        bool isLaunched;

        public void SetOwnerStrikerHub(StrikerHub strikerHub) {
            ownerStrikerHub = strikerHub;
        }

        void Awake() {
            rb = GetComponent<Rigidbody>();
            hitCollider = GetComponent<Collider>();
            renderers = GetComponentsInChildren<Renderer>(true);
        }

        void Start() {
            if (launchDelay <= 0f) {
                Launch();
                return;
            }

            hitCollider.enabled = false;
            foreach (var r in renderers) {
                r.enabled = false;
            }
            StartCoroutine(LaunchAfterDelay());
        }

        void Update() {
        }

        void OnTriggerEnter(Collider other) {
            if (!isLaunched) return;

            // 敵に当たった場合の処理
            if (!other.TryGetComponent<Hurtbox>(out var hurtbox)) {
                hurtbox = other.GetComponent<Hurtbox>();
                if (hurtbox == null) {
                    return;
                }
            }

            if (hurtbox == Hurtbox) {
                return;
            }

            if (ownerStrikerHub != null) {
                var otherStrikerHub = hurtbox.GetComponentInParent<StrikerHub>();
                if (otherStrikerHub == ownerStrikerHub) {
                    return;
                }
            }

            var nockBackDirection = GetWorldMoveDirection();
            hurtbox.GiveHit(new HitStatus(damage, nockBackDirection * nockbackSpeed));

            var hitPoint = other.ClosestPoint(transform.position);
            Destroy(this.gameObject);
        }

        System.Collections.IEnumerator LaunchAfterDelay() {
            yield return Ex.Wait(launchDelay);
            Launch();
        }

        void Launch() {
            isLaunched = true;
            foreach (var r in renderers) {
                r.enabled = true;
            }
            hitCollider.enabled = true;
            rb.linearVelocity = GetWorldMoveDirection() * speed;

           
        }

        Vector3 GetWorldMoveDirection() {
            if (localMoveDirection.sqrMagnitude <= 0.0001f) {
                return transform.forward;
            }

            return transform.TransformDirection(localMoveDirection.normalized);
        }
    }
}

