

using System;
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

        public void Dash(Vector2 dir) {
            rb.linearVelocity = dashSpeed * dir * new Vector2(Mathf.Sign(transform.forward.x), 1);
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
            anim.SetBool(Anime.IsDead.ToString(), true);
        }

        public void OnIntro() {
            anim.SetTrigger(Anime.OnIntro.ToString());
        }

        public void OnVictory() {
            anim.SetTrigger(Anime.OnVictory.ToString());
        }

        public void OnReset() {
            anim.SetTrigger(Anime.OnReset.ToString());
            anim.SetBool(Anime.IsDead.ToString(), false);
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
