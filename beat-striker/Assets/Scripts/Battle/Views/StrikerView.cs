

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

        void Start() {
            // Ensure no stray velocity at the start of the scene/play mode.
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        private bool HasAnimParameter(string name) {
            if (anim == null) return false;
            foreach (var p in anim.parameters) {
                if (p.name == name) return true;
            }
            return false;
        }

        private void SetAnimBool(string name, bool value) {
            if (anim == null) return;
            if (!HasAnimParameter(name)) return;
            anim.SetBool(name, value);
        }

        private void SetAnimTrigger(string name) {
            if (anim == null) return;
            if (!HasAnimParameter(name)) return;
            anim.SetTrigger(name);
        }

        public void Construct(IStrikerHit strikerHit) {
            this.strikerHit = strikerHit;
        }

        public void ChangeDirection(Vector2 direction) {
            if (this.direction != direction) {
                Debug.Log($"StrikerView.ChangeDirection: old={this.direction} new={direction}");
            }
            this.direction = direction;
        }

        public void CancelDirection() {
            direction = Vector2.zero;
        }

        public void Dash() {
            if (this.direction == Vector2.zero) return;
            // map 2D input (x: left/right, y: forward/back) to 3D velocity (x, y, z)
            rb.linearVelocity = new Vector3(this.direction.x * dashSpeed, rb.linearVelocity.y, this.direction.y * dashSpeed);
        }

        public void Attack() {
            SetAnimTrigger(Anime.DoAttack.ToString());
        }

        public void Charge() {
            SetAnimTrigger(Anime.DoCharge.ToString());
        }

        public void ChargeEnd() {
            SetAnimTrigger(Anime.OnCharged.ToString());
        }

        public void Special() {
            SetAnimTrigger(Anime.DoSpecial.ToString());
        }

        public void Guard() {
            SetAnimTrigger(Anime.DoGuard.ToString());
        }

        void Update() {
            if (isGround != preIsGround) {
                SetAnimBool(Anime.IsGround.ToString(), isGround);
                preIsGround = isGround;
            }

            RotateTowardsDirection(direction);

            // use Rigidbody.linearVelocity and project onto XZ plane for movement-related animator params
            var velocity = rb.linearVelocity;
            var planarVelocity = new Vector3(velocity.x, 0f, velocity.z);
            var velocityMagnitude = planarVelocity.magnitude;

            // Debug: log direction and planar velocity when direction is non-zero
            string paramVelocity = Anime.Velocity.ToString();
            string paramInputX = Anime.InputX.ToString();
            string paramInputY = Anime.InputY.ToString();
            string paramMoveX = Anime.MoveX.ToString();
            string paramMoveY = Anime.MoveY.ToString();

            if (direction != Vector2.zero) {
                var animVelStr = HasAnimParameter(paramVelocity) ? anim.GetFloat(paramVelocity).ToString() : "(no-param)";
                Debug.Log($"StrikerView.Update: direction={direction} planarVel={planarVelocity} animVel={animVelStr}");
            }

            if (HasAnimParameter(paramVelocity)) anim.SetFloat(paramVelocity, velocityMagnitude);
            if (HasAnimParameter(paramInputX)) anim.SetFloat(paramInputX, direction.x);
            if (HasAnimParameter(paramInputY)) anim.SetFloat(paramInputY, direction.y);

            if (velocityMagnitude > 0f) {
                if (HasAnimParameter(paramMoveX)) anim.SetFloat(paramMoveX, planarVelocity.x / velocityMagnitude);
                if (HasAnimParameter(paramMoveY)) anim.SetFloat(paramMoveY, planarVelocity.z / velocityMagnitude);
            }


            if (direction != Vector2.zero && Mathf.Abs(rb.linearVelocity.x) < walkSpeed && !targetRotationAngle.HasValue) {
                Debug.Log($"StrikerView: applying walk speed. dir={direction} rb.v.x={rb.linearVelocity.x} rb.v.z={rb.linearVelocity.z} walkSpeed={walkSpeed}");
                var v = rb.linearVelocity;
                v.x = walkSpeed * direction.x;
                v.z = walkSpeed * direction.y;
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
                SetAnimBool(Anime.IsRotation.ToString(), false);
                targetRotationAngle = null;
                return;
            }

            SetAnimBool(Anime.IsRotation.ToString(), true);
            float rotationAmount = Mathf.Clamp(angleDifference, -rotationThisFrame, rotationThisFrame);
            float newRotationAngle = currentAngle + rotationAmount;
            transform.rotation = Quaternion.Euler(0, newRotationAngle, 0);
        }

        public void OnMiss() {
            SetAnimTrigger(Anime.OnMiss.ToString());
        }

        public void OnHit() {
            SetAnimTrigger(Anime.OnHit.ToString());
        }

        public void OnDead() {
            SetAnimTrigger(Anime.OnDead.ToString());
        }

        public void OnIntro() {
            SetAnimTrigger(Anime.OnIntro.ToString());
        }

        public void OnVictory() {
            SetAnimTrigger(Anime.OnVictory.ToString());
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
