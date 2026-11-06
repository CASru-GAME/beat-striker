using Core.Battle;
using UnityEngine;
using Core.Striker;

namespace Core.LargeWizard {

    [RequireComponent(typeof(Rigidbody))]
    public class Rock : MonoBehaviour {
        [SerializeField] float damage = 10f;
        [SerializeField] float knockbackSpeed = 10f;

        [SerializeField] float lifetime = 5f;          // 生成後に自動破棄されるまでの時間
        [SerializeField] CapsuleCollider rockCollider;

        [Header("Position / Rotation Offset")]
        [SerializeField] Vector3 positionOffset;   // 生成位置からのオフセット
        [SerializeField] Vector3 rotationOffset;   // 追加の回転（オイラー角）

        Vector3 attackerPosition;
        Transform attackerRoot;
        StrikerHub ownerStrikerHub;
        bool hasHit;
        float spawnedAt;
        public Transform ObservationOwner => ownerStrikerHub != null ? ownerStrikerHub.transform : attackerRoot;
        public bool IsObservationThreat => !hasHit;
        public float ObservationLifetime => lifetime;
        public float ObservationDamage => damage;
        public float ObservationEndsAt => Mathf.Max(0f, lifetime - (Time.time - spawnedAt));

        public Bounds ObserveSpawnBounds(Vector3 spawnPosition) {
            var scale = transform.localScale;
            var direction = rockCollider.direction;
            var axis = direction == 0 ? Vector3.right : direction == 1 ? Vector3.up : Vector3.forward;
            var axialScale = Mathf.Abs(scale[direction]);
            var radialScale = Mathf.Max(Mathf.Abs(scale[(direction + 1) % 3]), Mathf.Abs(scale[(direction + 2) % 3]));
            var radius = rockCollider.radius * radialScale;
            var halfLine = Mathf.Max(0f, rockCollider.height * axialScale * .5f - radius);
            var rotation = Quaternion.Euler(rotationOffset);
            var line = rotation * axis * halfLine;
            var extents = Vector3.one * radius + new Vector3(Mathf.Abs(line.x), Mathf.Abs(line.y), Mathf.Abs(line.z));
            return new Bounds(spawnPosition + positionOffset + rotation * Vector3.Scale(rockCollider.center, scale), extents * 2f);
        }

        void Awake() {
            spawnedAt = Time.time;
            // インスペクタで指定したオフセットを適用
            transform.position += positionOffset;
            transform.rotation *= Quaternion.Euler(rotationOffset);

            // Rigidbodyの重力を有効にして落下させる
            var rb = GetComponent<Rigidbody>();
            rb.useGravity = true;
        }

        /// <summary>
        /// 攻撃者の位置をセットする（ノックバック方向の計算に使用）
        /// </summary>
        public void SetAttackerPosition(Vector3 position) {
            attackerPosition = position;
        }

        public void SetAttackerRoot(Transform root) {
            attackerRoot = root;
        }

        public void SetOwnerStrikerHub(StrikerHub strikerHub) {
            ownerStrikerHub = strikerHub;
        }

        void Start() {
            Destroy(gameObject, lifetime);
        }

        void OnTriggerEnter(Collider other) {
            if (hasHit) return;

            if (!other.TryGetComponent<Hurtbox>(out var hurtbox)) {
                hurtbox = other.GetComponent<Hurtbox>();
                if (hurtbox == null) {
                    return;
                }
            }

            if (other.transform.root == attackerRoot) return;

            if (ownerStrikerHub != null) {
                var otherStrikerHub = hurtbox.GetComponentInParent<StrikerHub>();
                if (otherStrikerHub == ownerStrikerHub) {
                    return;
                }
            }

            // 攻撃者から相手への水平方向にノックバック
            var dir = other.transform.position - attackerPosition;
            dir.y = 0f;
            var knockbackDir = dir.sqrMagnitude > 0.001f ? dir.normalized : Vector3.forward;
            hasHit = true;
            hurtbox.GiveHit(new HitStatus(damage, knockbackDir * knockbackSpeed));

            Destroy(gameObject, 5f);
        }
    }
}





