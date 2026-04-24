using Core.Battle;
using UnityEngine;
using Core.Striker;

namespace Core.LargeWizard {

    [RequireComponent(typeof(Rigidbody))]
    public class Rock : MonoBehaviour {
        [SerializeField] float damage = 10f;
        [SerializeField] float knockbackSpeed = 10f;

        [SerializeField] float lifetime = 5f;          // 生成後に自動破棄されるまでの時間

        [Header("Position / Rotation Offset")]
        [SerializeField] Vector3 positionOffset;   // 生成位置からのオフセット
        [SerializeField] Vector3 rotationOffset;   // 追加の回転（オイラー角）

        Vector3 attackerPosition;
        Transform attackerRoot;
        StrikerHub ownerStrikerHub;
        bool hasHit;

        void Awake() {
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





