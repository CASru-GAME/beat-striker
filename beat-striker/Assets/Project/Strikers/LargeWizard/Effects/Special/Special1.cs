using Core.Battle;
using UnityEngine;

namespace Core.LargeWizard {
    
    [RequireComponent(typeof(Rigidbody))]
    public class Special1 : MonoBehaviour {
        [SerializeField] float damage = 10f;
        [SerializeField] float nockbackSpeed = 10f;
        [SerializeField] float speed = 20f;
        Rigidbody rb;

    

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
            if (other.TryGetComponent<Hurtbox>(out var hurtbox)) {
                var nockBackDirection = rb.linearVelocity.normalized;
                hurtbox.GiveHit(new HitStatus(damage, nockBackDirection * nockbackSpeed));

                var hitPoint = other.ClosestPoint(transform.position);
                Destroy(this.gameObject);
            }
        }
    }
}

