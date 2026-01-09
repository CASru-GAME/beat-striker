using UnityEngine;
using Core.Battle;

public class Warrier_Dash_SMB : StateMachineBehaviour
{
    TrailRenderer trailer;
    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex) {
        var colliden = animator.GetComponentInParent<StrikerView>().GetColliden("Trail");
        trailer = colliden.transform.GetComponentInChildren<TrailRenderer>();
        trailer.enabled = true;
    }

    public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex) {
        trailer.enabled = false;
    }
}
