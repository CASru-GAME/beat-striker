using Core.Battle;
using UnityEngine;
using Core.Striker;

namespace Core.LargeWizard {
    
    [RequireComponent(typeof(Rigidbody))]
    public class Special : MonoBehaviour {
        [SerializeField] float damage = 10f;
        [SerializeField] float nockbackSpeed = 10f;
        [SerializeField] float speed = 20f;
        [SerializeField] float lifeTime = 5f;
        [SerializeField] float growDuration = 0.25f;
        [SerializeField] float shrinkDelay = 3f;
        [SerializeField] float shrinkDuration = 0.25f;
        [SerializeField] float beamSpawnDelay = 3f;
        Rigidbody rb;

        [SerializeField] beam11 beamPrefab;
        [SerializeField] AudioClip audioClip;
        [SerializeField] public Hurtbox Hurtbox;
        StrikerHub ownerStrikerHub;

        Vector3 targetScale;
        float elapsedTime;

        void Awake() {
            rb = GetComponent<Rigidbody>();
            targetScale = transform.localScale;
            transform.localScale = Vector3.zero;
        }

        void Start() {
            rb.linearVelocity = transform.forward * speed;
            Destroy(gameObject, lifeTime);
            StartCoroutine(SpawnBeamAfterDelay());
            audioClip.PlayAtApp(transform.position);
        }

        void Update() {
            elapsedTime += Time.deltaTime;
            if (elapsedTime < growDuration) {
                var growT = Mathf.Clamp01(elapsedTime / growDuration);
                transform.localScale = Vector3.Lerp(Vector3.zero, targetScale, growT);
                return;
            }

            var shrinkStart = growDuration + shrinkDelay;
            if (elapsedTime < shrinkStart) {
                transform.localScale = targetScale;
                return;
            }

            var shrinkT = Mathf.Clamp01((elapsedTime - shrinkStart) / shrinkDuration);
            transform.localScale = Vector3.Lerp(targetScale, Vector3.zero, shrinkT);
        }

        void OnTriggerEnter(Collider other) {
            // 敵に当たった場合の処理
            if (!other.TryGetComponent<Hurtbox>(out var hurtbox)) {
                hurtbox = other.GetComponent<Hurtbox>();
                if (hurtbox == null) {
                    return;
                }
            }

            if (hurtbox == Hurtbox) {
                return;
            }

            if (ownerStrikerHub != null) {
                var otherStrikerHub = hurtbox.GetComponentInParent<StrikerHub>();
                if (otherStrikerHub == ownerStrikerHub) {
                    return;
                }
            }

            var nockBackDirection = rb.linearVelocity.normalized;
            hurtbox.GiveHit(new HitStatus(damage, nockBackDirection * nockbackSpeed));

            var hitPoint = other.ClosestPoint(transform.position);
            Destroy(this.gameObject);
        }

        public void SetOwnerStrikerHub(StrikerHub strikerHub) {
            ownerStrikerHub = strikerHub;
        }

        System.Collections.IEnumerator SpawnBeamAfterDelay() {
            yield return Ex.Wait(beamSpawnDelay);
            var beamInstance = Instantiate(beamPrefab, transform.position, transform.rotation);
            beamInstance.Hurtbox = Hurtbox;
            beamInstance.SetOwnerStrikerHub(ownerStrikerHub);
            Destroy(beamInstance, 10f);
        }
    }
}