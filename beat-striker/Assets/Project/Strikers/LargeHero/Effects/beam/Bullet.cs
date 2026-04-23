using UnityEngine;
using Core.Battle;

namespace Core.LargeHero {
    [RequireComponent(typeof(Rigidbody))]

    public class Bullet : MonoBehaviour {
        [SerializeField] float damage = 10f;
        [SerializeField] float nockbackSpeed = 10f;
        [SerializeField] float speed = 20;
        Rigidbody rb;

        [SerializeField] GameObject rotationTarget;
        [SerializeField] float rotationSpeed = 700f;
        [SerializeField] GameObject impactPrefab;
        [SerializeField] GameObject trail;
        [SerializeField] LayerMask rootSearchMask;
        public GameObject OwnerRoot { get; set; }

        void Awake() {
            rb = GetComponent<Rigidbody>();
        }

        void Start() {
            var backward = -rb.transform.forward;
            if (Physics.Raycast(transform.position, backward, out var hit, Mathf.Infinity, rootSearchMask)) {
                OwnerRoot = hit.transform.root.gameObject;
            }

            rb.linearVelocity = speed * rb.transform.forward ;
        }

        void Update() {
            rotationTarget.transform.Rotate(Vector3. forward, rotationSpeed * Time.deltaTime, Space.Self); 
        }

        void OnTriggerEnter(Collider other) {
            if (other.transform.root.gameObject == OwnerRoot) {
                return;
            }
            if (other.TryGetComponent<Hurtbox>(out var hurtbox)) {
                var nockBackDirection = rb.linearVelocity.normalized;
                hurtbox.GiveHit(new HitStatus(damage, nockBackDirection * nockbackSpeed));

                var hitPoint = other.ClosestPoint(transform.position);
                Destroy(Instantiate(impactPrefab, hitPoint, transform.rotation), 5f);
                trail.transform.SetParent(null);
                Destroy(trail, 5f);
                Destroy(this.gameObject);
            }
        }
    }
}



