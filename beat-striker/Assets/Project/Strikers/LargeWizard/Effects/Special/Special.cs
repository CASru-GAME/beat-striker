using Core.Battle;
using UnityEngine;

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

        Vector3 targetScale;
        float elapsedTime;

        StrikerHub self;

        void Awake() {
            rb = GetComponent<Rigidbody>();
            targetScale = transform.localScale;
            transform.localScale = Vector3.zero;
        }

        public void SetSelf(StrikerHub hub) {
            self = hub;
        } 

        void Start() {
            rb.linearVelocity = transform.forward * speed;
            Destroy(gameObject, lifeTime);
            StartCoroutine(SpawnBeamAfterDelay());
            AudioSource.PlayClipAtPoint(audioClip, transform.position);
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
            if (other.TryGetComponent<Hurtbox>(out var hurtbox)) {
                var nockBackDirection = rb.linearVelocity.normalized;
                hurtbox.GiveHit(new HitStatus(damage, nockBackDirection * nockbackSpeed));

                var hitPoint = other.ClosestPoint(transform.position);
                Destroy(this.gameObject);
            }
        }

        System.Collections.IEnumerator SpawnBeamAfterDelay() {
            yield return new WaitForSeconds(beamSpawnDelay);
            Instantiate(beamPrefab, transform.position, transform.rotation).SetOwnerStrikerHub(self);
        }
    }
}

