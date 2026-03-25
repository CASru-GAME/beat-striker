using UnityEngine;
using Core.Battle;
using System.Collections;

public class WarrierChargeAttackSMB : StateMachineBehaviour {
    Colliden colliden;
    TrailRenderer trailer;
    [SerializeField] private float damage = 10f;
    [SerializeField] private float descendSpeed = 5f;
    [SerializeField] private float delayBeforeDescend = 0.2f;

    private Rigidbody rb;
    private Coroutine descendCoroutine;
    private MonoBehaviour coroutineRunner;
    [SerializeField] private AudioClip hitSound;
    [SerializeField] private GameObject hitEffectPrefab;
    bool virgine = true;
    float linearDampingBefore;


    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex) {
        colliden = FindColliden(animator, "Ax");
        if (colliden == null) {
            Debug.LogError("WarrierChargeAttackSMB: Colliden 'Ax' not found.");
            return;
        }
        trailer = colliden.transform.GetComponentInChildren<TrailRenderer>();
        trailer.enabled = true;
        colliden.OnEnterTrigger += OnEnterTrigger;

        rb = animator.GetComponent<Rigidbody>();
        coroutineRunner = animator.GetComponent<MonoBehaviour>();
        descendCoroutine = coroutineRunner.StartCoroutine(StartDescendAfterDelay());
        virgine = true;
        linearDampingBefore = rb.linearDamping;
        rb.linearDamping = 0f;
    }

    public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex) {
        if (colliden == null || trailer == null || rb == null) {
            return;
        }
        colliden.OnEnterTrigger -= OnEnterTrigger;
        rb.linearDamping = linearDampingBefore;

        if (descendCoroutine != null) {
            coroutineRunner.StopCoroutine(descendCoroutine);
            descendCoroutine = null;
        }

        trailer.enabled = false;
    }

    private IEnumerator StartDescendAfterDelay() {
        yield return new WaitForSeconds(delayBeforeDescend);
        rb.linearVelocity = new Vector3(rb.linearVelocity.x, -descendSpeed, rb.linearVelocity.z);
    }

    void OnEnterTrigger(Collider other) {
        rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);

        var hurtbox = other.GetComponent<Hurtbox>();
        if (hurtbox != null) {
            hurtbox.GiveHit(new HitStatus(damage));
        }
        else if (virgine) {
            virgine = false;
            AudioSource.PlayClipAtPoint(hitSound, trailer.transform.position);
            GameObject effect = GameObject.Instantiate(hitEffectPrefab, trailer.transform.position, Quaternion.identity);
            GameObject.Destroy(effect, 2f);
        }
    }

    static Colliden FindColliden(Animator animator, string collidenName) {
        var collidens = animator.GetComponentsInChildren<Colliden>(true);
        foreach (var item in collidens) {
            if (item.name == collidenName) {
                return item;
            }
        }
        return null;
    }
}
