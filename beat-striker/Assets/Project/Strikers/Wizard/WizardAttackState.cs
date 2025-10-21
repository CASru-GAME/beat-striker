using Unity.VisualScripting;
using UnityEditor.Experimental.GraphView;
using UnityEngine;

public class WizardAttackState : StateMachineBehaviour {
    public Hero hero;
    override public void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex) {
        if (hero == null) {
            hero = animator.GetComponent<Hero>();
        }

        hero.swardColliden.enabled = true;
        hero.swardColliden.OnEnterTrigger += OnSwardEnter;
    }

    override public void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex) {
    }

    override public void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex) {
        hero.swardColliden.enabled = false;
        hero.swardColliden.OnEnterTrigger -= OnSwardEnter;
    }

    void OnSwardEnter(Collider collider) {
        var enemy = collider.gameObject.GetComponent<Striker>();
        if (!enemy) return;
        enemy.Damage(10);
    }
}
