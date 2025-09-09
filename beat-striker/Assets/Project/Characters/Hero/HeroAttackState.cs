using UnityEngine;

public class HeroAttackState : StateMachineBehaviour
{
    private SphereCollider attackCollider;
    override public void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        if (attackCollider == null)
            attackCollider = animator.GetComponent<SphereCollider>();

        attackCollider.enabled = true;
    }

    override public void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
    }

    override public void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        attackCollider.enabled = false;
    }
}
