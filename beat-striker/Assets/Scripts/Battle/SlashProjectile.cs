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
        [SerializeField] public float hitEffectScale = 2f; // ヒットエフェクトのサイズ倍率

        // owner to avoid hitting the spawner (MonoBehaviourならStrikerViewでもStrikerHubでも対応可能)
        public MonoBehaviour owner;

        protected virtual void Start()
        {
            Destroy(gameObject, lifeTime);
        }

        protected virtual void Update()
        {
            // 地面に平行に移動（Y軸の移動なし、X-Z平面で移動）
            Vector3 moveDirection = transform.forward;
            moveDirection.y = 0f; // Y軸の成分を0にして地面に平行にする
            moveDirection.Normalize();
            transform.position += moveDirection * (speed * Time.deltaTime);
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
            // StrikerViewかStrikerHubかに関わらず、同じGameObjectなら無視
            if (owner != null && (target == owner || target.gameObject == owner.gameObject)) {
                Debug.Log("SlashProjectile: hit owner, ignoring.");
                return;
            }

            // apply damage via IStrikerHit interface
            var hitTarget = other.GetComponentInParent<IStrikerHit>();
            if (hitTarget == null) {
                Debug.Log("SlashProjectile: target has no IStrikerHit, ignoring.");
                return;
            }
            
            Debug.Log($"SlashProjectile: hitting target {target.name} for {damage} damage.");
            hitTarget.GiveHit(new HitStatus(new HitPoint(damage)));

            // spawn hit effect if assigned
            if (hitEffectPrefab != null)
            {
                Vector3 pos = other.ClosestPoint(transform.position);
                GameObject e = Instantiate(hitEffectPrefab, pos, Quaternion.identity);
                
                // ヒットエフェクトのサイズを調整
                if (hitEffectScale != 1f)
                {
                    e.transform.localScale = Vector3.one * hitEffectScale;
                }
                
                Debug.Log($"SlashProjectile: instantiated hit effect: {(e? e.name : "null")} at {pos} with scale {hitEffectScale}");

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
