using Core.Battle;
using UnityEngine;

namespace Core.LargeWizard {

    [RequireComponent(typeof(Rigidbody))]
    public class Ice : MonoBehaviour {
        [SerializeField] float damage = 10f;
        [SerializeField] float knockbackSpeed = 10f;

        [Header("Growth Animation")]
        [SerializeField] float growDuration = 0.3f;    // 生え切るまでの時間
        [SerializeField] float lifetime = 2f;          // 生成後に自動破棄されるまでの時間
        [SerializeField] float colliderEnableRatio = 0.3f; // コライダーを有効にする成長割合 (0~1)

        [SerializeField] GameObject impactPrefab;

        Vector3 targetScale;
        float elapsed;
        bool grown;
        Collider iceCollider;
        Vector3 attackerPosition;

        void Awake() {
            // 目標スケールを保存し、Y方向を0にして地面から生えるように見せる
            targetScale = transform.localScale;
            transform.localScale = new Vector3(targetScale.x, 0f, targetScale.z);

            // コライダーを無効化しておき、成長途中で有効にする
            iceCollider = GetComponent<Collider>();
            iceCollider.enabled = false;
        }

        /// <summary>
        /// 攻撃者の位置をセットする（ノックバック方向の計算に使用）
        /// </summary>
        public void SetAttackerPosition(Vector3 position) {
            attackerPosition = position;
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
            if (other.TryGetComponent<Hurtbox>(out var hurtbox)) {
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
}





