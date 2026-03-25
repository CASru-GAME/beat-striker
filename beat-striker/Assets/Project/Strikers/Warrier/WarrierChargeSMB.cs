using UnityEngine;
using Core.Striker;

public class WarrierChargeSMB : StateMachineBehaviour {
    [SerializeField] private float minVelocityY = 40f;

    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex) {
        var hub = animator.GetComponentInParent<StrikerHub>();
        hub?.EnsureAliceRuntimeHub().Dash();
        var rb = animator.GetComponent<Rigidbody>();
        if(rb.linearVelocity.y < minVelocityY) {
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, minVelocityY, rb.linearVelocity.z);
        }
    }
}
