using Core.Battle;
using UnityEngine;
using Core.Striker;

namespace Core.LargeWizard {
    
    [RequireComponent(typeof(Rigidbody))]
    public class Fire : MonoBehaviour {
        [SerializeField] float damage = 10f;
        [SerializeField] float nockbackSpeed = 10f;
        [SerializeField] float speed = 20f;
        Rigidbody rb;

        [SerializeField] GameObject impactPrefab;
        [SerializeField] AudioClip audioClip;
        [SerializeField] GameObject trail;
        [SerializeField] public Hurtbox Hurtbox;
        StrikerHub ownerStrikerHub;

        public void SetOwnerStrikerHub(StrikerHub strikerHub) {
            ownerStrikerHub = strikerHub;
        }

        void Awake() {
            rb = GetComponent<Rigidbody>();
        }

        void Start() {
            rb.linearVelocity = transform.forward * speed;
        }

        void Update() {
        }

        void OnTriggerEnter(Collider other) {
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
}

