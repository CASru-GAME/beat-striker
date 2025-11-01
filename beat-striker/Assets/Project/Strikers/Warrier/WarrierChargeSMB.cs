using UnityEngine;
using Core.Battle;

public class WarrierChargeSMB : StateMachineBehaviour {
    [SerializeField] private float minVelocityY = 40f;

    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex) {
        var striker = animator.GetComponent<StrikerView>();
        striker.Dash();
        var rb = animator.GetComponent<Rigidbody>();
        if(rb.linearVelocity.y < minVelocityY) {
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, minVelocityY, rb.linearVelocity.z);
        }
    }
}
