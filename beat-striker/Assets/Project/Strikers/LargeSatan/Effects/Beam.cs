using System.Collections.Generic;
using Core.Battle;
using UnityEngine;

namespace Core.LargeSatan {
    [RequireComponent(typeof(ParticleSystem))]
    public class Beam : MonoBehaviour {
        ParticleSystem system;
        readonly List<ParticleCollisionEvent> collisionEvents = new();
        [SerializeField] float damage = 10f;
        [SerializeField] float nockbackSpeed = 5f;


        void Awake() {
            system = GetComponent<ParticleSystem>();
        }

        void OnParticleCollision(GameObject other) {
            if(other.TryGetComponent<Hurtbox>(out var hurtbox)){
                int numCollisionEvents = system.GetCollisionEvents(other, collisionEvents);
                
                if (numCollisionEvents >= 1) {
                    Vector3 hitPoint = collisionEvents[0].intersection;
                    var nockBackDirection = Mathf.Sign(hitPoint.x - transform.position.x) * Vector2.right;
                    hurtbox.GiveHit(new HitStatus(damage, nockbackSpeed * nockBackDirection));
                }
            }
        }
    }
}