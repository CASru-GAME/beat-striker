using System;
using System.Collections;
using Core.App.Interfaces;
using Core.App.Types;
using Core.Utils;
using UnityEngine;

namespace Core.Battle {

    [RequireComponent(typeof(Rigidbody))]
    public class StrikerView : MonoBehaviour, IStrikerView, IStrikerHit {
        private Vector2 direction;
        private Rigidbody rb;
        [SerializeField] private Animator anim;
        private bool isGround = false, preIsGround = false;
        [SerializeField] float dashSpeed = 50f;
        [SerializeField] float walkSpeed = 5f;
        [SerializeField] float rotationSpeed = 360f;
        private bool isGuard = false;
        private float? targetRotationAngle = null;

        private Vector3 initialPosition;
        private Quaternion initialRotation;

        [SerializeField] private CollidenRef[] collidenRefs;
        [Header("Special spawn settings")]
        [SerializeField] private float specialSpawnHeight = 2.0f;
        [SerializeField] private float specialSpawnForward = 0.8f;

        // Model references (set via Construct)
        private IStrikerModel model;
        private IRythmTrackModel rythmTrackModel;
        private IPlayerRegistry playerRegistry;
        private IBattleModel battleModel;

        // Subscriptions
        private CompositeDisposable subscriptions;
        private bool isInputEnabled = false;

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
            anim ??= GetComponent<Animator>();
        }

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
            }
            catch { }

            float finalHeight = specialSpawnHeight + heightOffset;
            Vector3 origin = transform.position + Vector3.up * finalHeight + transform.forward * specialSpawnForward;
            if (spawnTransform != null) {
                origin = spawnTransform.position + Vector3.up * (finalHeight - 1.0f);
            }

            for (int i = 0; i < count; i++) {
                float characterYRotation = transform.eulerAngles.y;
                float t = (count == 1) ? 0f : ((float)i / (count - 1) - 0.5f);
                float angle = characterYRotation + (t * spreadAngle);

                Quaternion rot = Quaternion.Euler(0f, angle, 0f);
                GameObject go = Instantiate(slashPrefab, origin, rot);

                var sp = go.GetComponent<SlashProjectile>();
                if (sp != null) {
                    sp.speed = speed;
                    sp.damage = damage;
                    sp.hitEffectPrefab = hitEffectPrefab;
                    sp.owner = this;
                }

                var crescentGen = go.GetComponentInChildren<Core.Battle.CrescentMeshGenerator>();
                if (crescentGen != null) {
                    crescentGen.SetHueOffset(hueOffset);
                }

                yield return new WaitForSeconds(spawnInterval);
            }
        }

        /// <summary>
        /// Construct view with model and events - no presenter needed
        /// </summary>
        public void Construct(IStrikerModel model, IRythmTrackModel rythmTrackModel, IPlayerRegistry playerRegistry, IBattleModel battleModel) {
            this.model = model;
            this.rythmTrackModel = rythmTrackModel;
            this.playerRegistry = playerRegistry;
            this.battleModel = battleModel;

            subscriptions = new CompositeDisposable();

            // Subscribe to model events
            subscriptions.Add(model.SubscribeDied(OnModelDied));

            // Subscribe to battle events
            subscriptions.Add(battleModel.SubscribeRequireIntroPose(OnRequireIntroPose));
            subscriptions.Add(battleModel.SubscribeRequireVictoryPose(OnRequireVictoryPose));
            subscriptions.Add(battleModel.SubscribeBattleStarted(OnBattleStarted));
            subscriptions.Add(battleModel.SubscribeRoundFinished(OnBattleFinished));
            subscriptions.Add(battleModel.SubscribeOutroStarted(OnBattleFinished));
            subscriptions.Add(rythmTrackModel.SubscribeMissedBeat(OnMissedBeat));
        }

        public IStrikerModelGetter Construct(PlayerId playerId, ScoreRule rule, IRythmTrackModel rythmTrackModel, IPlayerRegistry playerRegistry) {
            throw new System.NotSupportedException("StrikerView should be constructed via StrikerInstaller with IBattleModel.");
        }

        private void OnDestroy() {
            subscriptions?.Dispose();
        }

        // Battle events handlers
        private void OnRequireIntroPose(PlayerId playerId) {
            if (model.PlayerId != playerId) return;
            OnIntro();
        }

        private void OnRequireVictoryPose(PlayerId playerId) {
            if (model.PlayerId != playerId) return;
            OnVictory();
        }

        private void OnBattleStarted(IBattlemodelGetter _) {
            isInputEnabled = true;
        }

        private void OnBattleFinished(IBattlemodelGetter _) {
            isInputEnabled = false;
        }

        private void OnMissedBeat(PlayerId playerId) {
            if (model.PlayerId != playerId || model.IsDead()) return;
            OnMiss();
        }

        private void OnModelDied() {
            OnDead();
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

        public void GiveHit(HitStatus status) {
            if (model.IsDead()) return;

            OnHit();
            var damage = CalcHit(status);
            model.TakeDamage(damage);
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

    [Serializable]
    public class CollidenRef {
        public string key;
        public Colliden colliden;
    }
}
