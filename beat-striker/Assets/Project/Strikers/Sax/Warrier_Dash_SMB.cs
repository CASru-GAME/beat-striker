using UnityEngine;

public class Warrier_Dash_SMB : StateMachineBehaviour
{
    TrailRenderer trailer;
    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex) {
        var colliden = FindColliden(animator, "Trail");
        if (colliden == null) {
            Debug.LogError("Warrier_Dash_SMB: Colliden 'Trail' not found.");
            return;
        }
        trailer = colliden.transform.GetComponentInChildren<TrailRenderer>();
        trailer.enabled = true;
    }

    public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex) {
        if (trailer != null) {
            trailer.enabled = false;
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
