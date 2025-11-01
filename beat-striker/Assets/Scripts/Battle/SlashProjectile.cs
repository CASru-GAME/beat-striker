using UnityEngine;

namespace Core.Battle {

    // Simple projectile used for the special move slashes.
    // Add a Collider (isTrigger = true) and optionally a Rigidbody (kinematic) in the prefab.
    public class SlashProjectile : MonoBehaviour
    {
        public float speed = 12f;
        public float lifeTime = 2f;
        public int damage = 10;
        public GameObject hitEffectPrefab;

        // owner to avoid hitting the spawner
        public StrikerView owner;

        void Start()
        {
            Destroy(gameObject, lifeTime);
        }

        void Update()
        {
            transform.position += transform.right * (speed * Time.deltaTime);
        }

        void OnTriggerEnter(Collider other)
        {
            // debug
            Debug.Log($"SlashProjectile.OnTriggerEnter: Other={other.name}, Owner={(owner? owner.name : "null")}, hitEffectPrefab={(hitEffectPrefab? hitEffectPrefab.name : "null")} ");

            // ignore self-collision or hitting the owner
            var target = other.GetComponent<StrikerView>();
            if (target == null) {
                Debug.Log("SlashProjectile: collided object has no StrikerView, ignoring.");
                return;
            }
            if (owner != null && target == owner) {
                Debug.Log("SlashProjectile: hit owner, ignoring.");
                return;
            }

            // apply damage
            Debug.Log($"SlashProjectile: hitting target {target.name} for {damage} damage.");
            target.TakeDamage(new HitStatus(new HitPoint(damage)));

            // spawn hit effect if assigned
            if (hitEffectPrefab != null)
            {
                Vector3 pos = other.ClosestPoint(transform.position);
                GameObject e = Instantiate(hitEffectPrefab, pos, Quaternion.identity);
                Debug.Log($"SlashProjectile: instantiated hit effect: {(e? e.name : "null")} at {pos}");

                // try root ParticleSystem first, then children
                var ps = e.GetComponent<ParticleSystem>();
                if (ps == null) ps = e.GetComponentInChildren<ParticleSystem>();

                if (ps != null)
                {
                    ps.Play();
                    var main = ps.main;
                    float extra = 1f;
                    try { extra = main.startLifetime.constantMax; } catch { }
                    float destroyAfter = main.duration + extra + 0.1f;
                    Debug.Log($"SlashProjectile: will destroy effect in {destroyAfter} seconds.");
                    Destroy(e, destroyAfter);
                }
                else
                {
                    Debug.LogWarning("SlashProjectile: no ParticleSystem found on hit effect instance.");
                    Destroy(e, 2f);
                }
            }

            // remove the projectile on hit
            Destroy(gameObject);
        }
    }
}
