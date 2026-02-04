using System.Collections.Generic;
using Core.Battle;
using UnityEngine;

namespace Core.LargeSatanf {
    [RequireComponent(typeof(Rigidbody))]
    public class Bullet : MonoBehaviour {
        [SerializeField] float damage = 10f;
        [SerializeField] float nockbackSpeed = 5f;
        [SerializeField] float speed = 1;
        [SerializeField] float scalingSpeed = 100f;
        [SerializeField] Transform rotationTarget;
        [SerializeField] float rotationSpeed;
        Rigidbody rb;
        Vector3 targetScale;

        void Awake() {
            rb = GetComponent<Rigidbody>();
            targetScale = transform.localScale;
            transform.localScale = Vector3.zero;
        }

        void Start() {
            rb.linearVelocity = speed * rb.transform.forward;
        }

        void Update() {
            if (transform.localScale.x < targetScale.x) {
                transform.localScale = Vector3.MoveTowards(transform.localScale, targetScale, scalingSpeed * Time.deltaTime);
            }
            rotationTarget.Rotate(Vector3.forward, rotationSpeed * Time.deltaTime, Space.Self);
        }

        void OnTriggerEnter(Collider other) {
            if (other.TryGetComponent<Hurtbox>(out var hurtbox)) {
                var nockBackDirection = rb.linearVelocity.normalized;
                hurtbox.GiveHit(new HitStatus(damage, nockbackSpeed * nockBackDirection));
            }
        }
    }
}