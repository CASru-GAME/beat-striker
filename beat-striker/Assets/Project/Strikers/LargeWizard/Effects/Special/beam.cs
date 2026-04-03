using Core.Battle;
using UnityEngine;

namespace Core.LargeWizard {
    
    [RequireComponent(typeof(Rigidbody))]
    public class beam : MonoBehaviour {
        [SerializeField] float damage = 10f;
        [SerializeField] float nockbackSpeed = 10f;
        [SerializeField] float speed = 20f;
        [SerializeField, Min(0f)] float launchDelay = 0f;
        Rigidbody rb;
        Collider hitCollider;
        Renderer[] renderers;
        bool isLaunched;

        [SerializeField] GameObject impactPrefab;
        [SerializeField] AudioClip audioClip;
        [SerializeField] GameObject trail;

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
            if (other.TryGetComponent<Hurtbox>(out var hurtbox)) {
                var nockBackDirection = rb.linearVelocity.normalized;
                hurtbox.GiveHit(new HitStatus(damage, nockBackDirection * nockbackSpeed));

                var hitPoint = other.ClosestPoint(transform.position);
                Destroy(Instantiate(impactPrefab, hitPoint, transform.rotation), 5f);
                AudioSource.PlayClipAtPoint(audioClip, hitPoint);
                trail.transform.SetParent(null);
                Destroy(trail, 5f);
                Destroy(this.gameObject);
            }
        }

        System.Collections.IEnumerator LaunchAfterDelay() {
            yield return new WaitForSeconds(launchDelay);
            Launch();
        }

        void Launch() {
            isLaunched = true;
            foreach (var r in renderers) {
                r.enabled = true;
            }
            hitCollider.enabled = true;
            rb.linearVelocity = transform.forward * speed;
        }
    }
}

