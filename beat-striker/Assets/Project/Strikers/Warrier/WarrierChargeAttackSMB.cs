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

    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex) {
        colliden = animator.GetComponent<StrikerView>().GetColliden("Ax");
        trailer = colliden.transform.GetComponentInChildren<TrailRenderer>();
        trailer.enabled = true;
        colliden.OnEnterTrigger += OnEnterTrigger;

        rb = animator.GetComponent<Rigidbody>();
        coroutineRunner = animator.GetComponent<MonoBehaviour>();
        descendCoroutine = coroutineRunner.StartCoroutine(StartDescendAfterDelay());
    }

    public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex) {
        colliden.OnEnterTrigger -= OnEnterTrigger;

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

        var target = other.GetComponent<StrikerView>();
        if (target != null) {
            target.TakeDamage(new HitStatus(new HitPoint(damage)));
        }
    }
}
