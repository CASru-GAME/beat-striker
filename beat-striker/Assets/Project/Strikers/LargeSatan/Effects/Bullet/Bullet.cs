using Alice;
using UnityEngine;

namespace Core.LargeSatan {

    [RequireComponent(typeof(Rigidbody))]
    public class Bullet : MonoBehaviour {
        [SerializeField] float damage = 10f;
        [SerializeField] float nockbackSpeed = 10f;
        [SerializeField] float speed = 20;
        Rigidbody rb;

        [SerializeField] GameObject rotationTarget;
        [SerializeField] float rotationSpeed = 700;

        [SerializeField] GameObject impactPrefab;
        [SerializeField] GameObject trail;

        [SerializeField] AudioClip whizClip;
        [SerializeField] AudioClip impactClip;

        void Awake() {
            rb = GetComponent<Rigidbody>();
        }

        void Start() {
            rb.linearVelocity = speed * rb.transform.forward;
            whizClip.PlayAtApp(transform.position);
        }

        void Update() {
            rotationTarget.transform.Rotate(Vector3.forward, rotationSpeed * Time.deltaTime, Space.Self);
        }

        void OnTriggerEnter(Collider other) {
            if (other.TryGetComponent<Hurtbox>(out var hurtbox)) {
                var nockBackDirection = rb.linearVelocity.normalized;
                hurtbox.GiveHit(new HitStatus(damage, nockbackSpeed * nockBackDirection));

                var hitPoint = other.ClosestPoint(transform.position);

                impactClip.PlayAtApp(hitPoint);

                Destroy(Instantiate(impactPrefab, hitPoint, transform.rotation), 5f);
                trail.transform.SetParent(null);
                Destroy(trail, 5f);
                Destroy(this.gameObject);
            }
        }
    }
}
