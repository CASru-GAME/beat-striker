

using System;
using System.Collections;
using Core.Battle;
using UnityEngine;

namespace Core.Battle {

    [RequireComponent(typeof(Animator))]
    [RequireComponent(typeof(Rigidbody))]
    public class StrikerView : MonoBehaviour, IStrikerView {
        private Vector2 direction;
        private Rigidbody rb;
        private Animator anim;
        private bool isGround = false, preIsGround = false;
        [SerializeField] float dashSpeed = 50f;
        [SerializeField] float walkSpeed = 5f;
        [SerializeField] float rotationSpeed = 360f;
        private bool isGuard = false;
        private float? targetRotationAngle = null;
        private IStrikerHit strikerHit;

        private Vector3 initialPosition;
        private Quaternion initialRotation;

    [SerializeField] private CollidenRef[] collidenRefs;
    [Header("Special spawn settings")]
    [SerializeField] private float specialSpawnHeight = 2.0f;
    [SerializeField] private float specialSpawnForward = 0.8f;

        public Colliden GetColliden(string key) {
            foreach (var collidenRef in collidenRefs) {
                if (collidenRef.key == key) {
                    return collidenRef.colliden;
                }
            }
            return null;
        }

        void Awake() {
            rb = GetComponent<Rigidbody>();
            anim = GetComponent<Animator>();
        }

            // 必殺技の時間差発射用メソッド
            public void SpawnSpecialProjectiles(GameObject slashPrefab, int count, float spreadAngle, float speed, int damage, GameObject hitEffectPrefab, float spawnInterval, float heightOffset = 0f, float hueOffset = 0f) {
                if (slashPrefab == null) {
                    Debug.LogWarning("StrikerView.SpawnSpecialProjectiles: slashPrefab not assigned.");
                    return;
                }
                StartCoroutine(SpawnProjectilesCoroutine(slashPrefab, count, spreadAngle, speed, damage, hitEffectPrefab, spawnInterval, heightOffset, hueOffset));
            }

            private IEnumerator SpawnProjectilesCoroutine(GameObject slashPrefab, int count, float spreadAngle, float speed, int damage, GameObject hitEffectPrefab, float spawnInterval, float heightOffset, float hueOffset) {
                Transform spawnTransform = null;
                try {
                    var c = GetColliden("sword");
                    if (c != null) spawnTransform = c.transform;
                } catch { }

                // 発射位置を設定（Inspectorで調整できるspecialSpawnHeight/specialSpawnForwardを使用 + 高さオフセット）
                float finalHeight = specialSpawnHeight + heightOffset;
                Vector3 origin = transform.position + Vector3.up * finalHeight + transform.forward * specialSpawnForward;
                if (spawnTransform != null) {
                    origin = spawnTransform.position + Vector3.up * (finalHeight - 1.0f); // 剣の位置から少し上に
                }

                for (int i = 0; i < count; i++) {
                    // キャラクターの向いている方向を中心に扇形で配置
                    float characterYRotation = transform.eulerAngles.y;
                    float t = (count == 1) ? 0f : ((float)i / (count - 1) - 0.5f); // -0.5 .. 0.5
                    float angle = characterYRotation + (t * spreadAngle); // spreadAngleを前方中心の扇形として使用

                    Quaternion rot = Quaternion.Euler(0f, angle, 0f);
                    Debug.Log($"Spawning projectile {i+1}/{count} at origin {origin}, spread angle: {t * spreadAngle}° (final: {angle}°)");
                    GameObject go = Instantiate(slashPrefab, origin, rot);

                    Debug.Log($"Instantiated projectile at {go.transform.position}");
                    var sp = go.GetComponent<SlashProjectile>();
                    if (sp != null) {
                        sp.speed = speed;
                        sp.damage = damage;
                        sp.hitEffectPrefab = hitEffectPrefab;
                        sp.owner = this;
                        Debug.Log($"Spawned slash projectile {i+1}/{count} with hitEffectPrefab: {(hitEffectPrefab ? hitEffectPrefab.name : "null")}");
                    }
                    
                    // 色相オフセットをCrescentMeshGeneratorに設定
                    var crescentGen = go.GetComponentInChildren<Core.Battle.CrescentMeshGenerator>();
                    if (crescentGen != null) {
                        crescentGen.SetHueOffset(hueOffset);
                        Debug.Log($"Set hue offset {hueOffset} on projectile {i+1}/{count}");
                    }

                    yield return new WaitForSeconds(spawnInterval);
                }
            }

        public void Construct(IStrikerHit strikerHit) {
            this.strikerHit = strikerHit;
        }

        public void ChangeDirection(Vector2 direction) {
            this.direction = direction;
        }

        public void CancelDirection() {
            direction = Vector2.zero;
        }

        public void Dash() {
            if (this.direction == Vector2.zero) return;
            rb.linearVelocity = dashSpeed * this.direction;
        }

        public void Attack() {
            anim.SetTrigger(Anime.DoAttack.ToString());
        }

        public void Charge() {
            anim.SetTrigger(Anime.DoCharge.ToString());
        }

        public void ChargeEnd() {
            anim.SetTrigger(Anime.OnCharged.ToString());
        }

        public void Special() {
            anim.SetTrigger(Anime.DoSpecial.ToString());
        }

        public void Guard() {
            anim.SetTrigger(Anime.DoGuard.ToString());
        }

        void Update() {
            if (isGround != preIsGround) {
                anim.SetBool(Anime.IsGround.ToString(), isGround);
                preIsGround = isGround;
            }

            RotateTowardsDirection(direction);

            anim.SetFloat(Anime.Velocity.ToString(), rb.linearVelocity.magnitude);
            anim.SetFloat(Anime.InputX.ToString(), direction.x);
            anim.SetFloat(Anime.InputY.ToString(), direction.y);

            var velocity = rb.linearVelocity;
            var velocityMagnitude = velocity.magnitude;
            if (velocityMagnitude > 0) {
                anim.SetFloat(Anime.MoveX.ToString(), velocity.x / velocityMagnitude);
                anim.SetFloat(Anime.MoveY.ToString(), velocity.y / velocityMagnitude);
            }


            if (direction != Vector2.zero && Mathf.Abs(rb.linearVelocity.x) < walkSpeed && !targetRotationAngle.HasValue) {
                var v = rb.linearVelocity;
                v.x = walkSpeed * direction.x;
                rb.linearVelocity = v;
            }
        }

        public void TakeDamage(HitStatus status) {
            this.strikerHit.TakeDamage(status);
        }

        private void OnCollisionEnter(Collision collision) {
            var view = collision.gameObject.GetComponent<StrikerView>();
            if (view == null) return;
            view.TakeDamage(new HitStatus(CalcHit(new HitStatus(new HitPoint(10)))));
        }

        private void OnCollisionStay(Collision collision) {
            foreach (var contact in collision.contacts) {
                if (contact.normal.y > 0.5f) {
                    isGround = true;
                    return;
                }
            }
        }

        private void OnCollisionExit(Collision collision) {
            isGround = false;
        }

        private void RotateTowardsDirection(Vector2 targetDirection) {
            if (targetDirection.x != 0) {
                targetRotationAngle = targetDirection.x > 0 ? 90f : -90f;
            }
            if (!targetRotationAngle.HasValue) return;

            float currentAngle = transform.eulerAngles.y;
            float angleDifference = Mathf.DeltaAngle(currentAngle, targetRotationAngle.Value);
            float rotationThisFrame = rotationSpeed * Time.deltaTime;

            if (Mathf.Abs(angleDifference) < rotationThisFrame) {
                transform.rotation = Quaternion.Euler(0, targetRotationAngle.Value, 0);
                anim.SetBool(Anime.IsRotation.ToString(), false);
                targetRotationAngle = null;
                return;
            }

            anim.SetBool(Anime.IsRotation.ToString(), true);
            float rotationAmount = Mathf.Clamp(angleDifference, -rotationThisFrame, rotationThisFrame);
            float newRotationAngle = currentAngle + rotationAmount;
            transform.rotation = Quaternion.Euler(0, newRotationAngle, 0);
        }

        public void OnMiss() {
            anim.SetTrigger(Anime.OnMiss.ToString());
        }

        public void OnHit() {
            anim.SetTrigger(Anime.OnHit.ToString());
        }

        public void OnDead() {
            anim.SetTrigger(Anime.OnDead.ToString());
        }

        public void OnIntro() {
            anim.SetTrigger(Anime.OnIntro.ToString());
        }

        public void OnVictory() {
            anim.SetTrigger(Anime.OnVictory.ToString());
        }

        public HitPoint CalcHit(HitStatus status) {
            if (isGuard) {
                return new HitPoint(status.damage.value / 2);
            }
            return new HitPoint(status.damage.value);
        }

        public void SavePosition() {
            initialPosition = transform.position;
            initialRotation = transform.rotation;
        }

        public void ResetPosition() {
            transform.position = initialPosition;
            transform.rotation = initialRotation;

            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;

            direction = Vector2.zero;
            targetRotationAngle = null;

            isGuard = false;
        }

        public Vector2 GetForwardDirection() {
            Vector3 forward = transform.forward;
            return new Vector2(forward.x, forward.z).normalized;
        }
    }
}

[Serializable]
public class CollidenRef {
    public string key;
    public Colliden colliden;
}
