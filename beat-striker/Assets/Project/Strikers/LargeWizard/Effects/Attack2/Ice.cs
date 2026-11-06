using Core.Battle;
using UnityEngine;
using Core.Striker;

namespace Core.LargeWizard {

    [RequireComponent(typeof(Rigidbody))]
    public class Ice : MonoBehaviour {
        [SerializeField] float damage = 10f;
        [SerializeField] float knockbackSpeed = 10f;

        [Header("Growth Animation")]
        [SerializeField] float growDuration = 0.3f;    // 生え切るまでの時間
        [SerializeField] float lifetime = 1.5f;          // 生成後に自動破棄されるまでの時間
        [SerializeField] float colliderEnableRatio = 0.3f; // コライダーを有効にする成長割合 (0~1)
        [SerializeField] BoxCollider iceCollider;

        [SerializeField] GameObject impactPrefab;

        [Header("Position / Rotation Offset")]
        [SerializeField] Vector3 positionOffset;   // 生成位置からのオフセット
        [SerializeField] Vector3 rotationOffset;   // 追加の回転（オイラー角）

        Vector3 targetScale;
        float elapsed;
        bool grown;
        bool hasHit;
        float spawnedAt;
        public Transform ObservationOwner => ownerStrikerHub != null ? ownerStrikerHub.transform : attackerRoot;
        public bool IsObservationThreat => !hasHit;
        public float ObservationActivationDelay => growDuration * colliderEnableRatio;
        public float ObservationLifetime => lifetime;
        public float ObservationDamage => damage;
        public float ObservationStartsAt => Mathf.Max(0f, ObservationActivationDelay - (Time.time - spawnedAt));
        public float ObservationEndsAt => Mathf.Max(0f, lifetime - (Time.time - spawnedAt));
        public Bounds ObservationBounds => BoundsAt(transform.position, transform.rotation, targetScale);

        public Bounds ObserveSpawnBounds(Vector3 groundPosition) =>
            BoundsAt(groundPosition + positionOffset, Quaternion.Euler(rotationOffset), transform.localScale);

        Bounds BoundsAt(Vector3 position, Quaternion rotation, Vector3 scale) {
            var matrix = Matrix4x4.TRS(position, rotation, scale);
            var bounds = new Bounds(matrix.MultiplyPoint3x4(iceCollider.center), Vector3.zero);
            var half = iceCollider.size * .5f;
            for (var i = 0; i < 8; i++) {
                var corner = iceCollider.center + Vector3.Scale(half,
                    new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
                bounds.Encapsulate(matrix.MultiplyPoint3x4(corner));
            }
            return bounds;
        }
        Vector3 attackerPosition;
        Transform attackerRoot;
        StrikerHub ownerStrikerHub;

        void Awake() {
            spawnedAt = Time.time;
            // インスペクタで指定したオフセットを適用
            transform.position += positionOffset;
            transform.rotation *= Quaternion.Euler(rotationOffset);

            // 目標スケールを保存し、Y方向を0にして地面から生えるように見せる
            targetScale = transform.localScale;
            transform.localScale = new Vector3(targetScale.x, 0f, targetScale.z);

            // コライダーを無効化しておき、成長途中で有効にする
            iceCollider.enabled = false;
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

        void Update() {
            if (grown) return;

            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / growDuration);

            // EaseOutBack で勢いよく突き出す演出
            float ease = 1f + 2.70158f * Mathf.Pow(t - 1f, 3f) + 1.70158f * Mathf.Pow(t - 1f, 2f);

            transform.localScale = new Vector3(
                targetScale.x,
                targetScale.y * ease,
                targetScale.z
            );

            // ある程度成長したらコライダーを有効化し、OnTriggerEnter を発火させる
            if (!iceCollider.enabled && t >= colliderEnableRatio) {
                iceCollider.enabled = true;
            }

            if (t >= 1f) grown = true;
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

            hasHit = true;
            iceCollider.enabled = false;

            // 攻撃者から相手への水平方向にノックバック
            var dir = other.transform.position - attackerPosition;
            dir.y = 0f;
            var knockbackDir = dir.sqrMagnitude > 0.001f ? dir.normalized : Vector3.forward;
            hurtbox.GiveHit(new HitStatus(damage, knockbackDir * knockbackSpeed));

            var hitPoint = other.ClosestPoint(transform.position);
            Destroy(Instantiate(impactPrefab, hitPoint, transform.rotation), 5f);
        }
    }
}





