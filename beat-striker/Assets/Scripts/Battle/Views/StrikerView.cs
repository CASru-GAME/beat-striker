

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

        void Awake() {
            rb = GetComponent<Rigidbody>();
            anim = GetComponent<Animator>();
        }

        public void ChangeDirection(Vector2 direction) {
            this.direction = direction;
        }

        public void CancelDirection() {
            direction = Vector2.zero;
        }

        public void Dash() {
            var direction = this.direction == Vector2.zero ? (Vector2)transform.forward : this.direction;
            rb.linearVelocity = dashSpeed * direction;
        }

        public void Attack() {
            anim.SetTrigger(Anime.DoAttack.ToString());
        }

        public void Charge() {
            anim.SetTrigger(Anime.DoCharge.ToString());
        }

        public void ChargeEnd() {
            anim.SetTrigger(Anime.DoCharge.ToString());
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

            if (direction != Vector2.zero) {
                RotateTowardsDirection(direction);
            }

            anim.SetFloat(Anime.Velocity.ToString(), rb.linearVelocity.magnitude);
            anim.SetFloat(Anime.InputX.ToString(), direction.x);
            anim.SetFloat(Anime.InputY.ToString(), direction.y);

            var velocity = rb.linearVelocity;
            var velocityMagnitude = velocity.magnitude;
            if (velocityMagnitude > 0) {
                anim.SetFloat(Anime.MoveX.ToString(), velocity.x / velocityMagnitude);
                anim.SetFloat(Anime.MoveY.ToString(), velocity.y / velocityMagnitude);
            }


            if (direction != Vector2.zero && Mathf.Abs(rb.linearVelocity.x) < walkSpeed && !anim.GetBool(Anime.IsRotation.ToString())) {
                var v = rb.linearVelocity;
                v.x = walkSpeed * Mathf.Sign(direction.x);
                rb.linearVelocity = v;
            }
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
            float targetAngle = Mathf.Atan2(targetDirection.x, targetDirection.y) * Mathf.Rad2Deg;
            float currentAngle = transform.eulerAngles.y;
            float angleDifference = Mathf.DeltaAngle(currentAngle, targetAngle);
            float rotationThisFrame = rotationSpeed * Time.deltaTime;

            if (Mathf.Abs(angleDifference) < rotationThisFrame) {
                transform.rotation = Quaternion.Euler(transform.eulerAngles.x, targetAngle, transform.eulerAngles.z);
                anim.SetBool(Anime.IsRotation.ToString(), false);
                return;
            }

            anim.SetBool(Anime.IsRotation.ToString(), true);
            float rotationAmount = Mathf.Clamp(angleDifference, -rotationThisFrame, rotationThisFrame);
            float newRotationAngle = currentAngle + rotationAmount;
            transform.rotation = Quaternion.Euler(transform.eulerAngles.x, newRotationAngle, transform.eulerAngles.z);
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
            if(isGuard) {
                return new HitPoint(status.damage.value / 2);
            }
            return new HitPoint(status.damage.value);
        }
    }
}
