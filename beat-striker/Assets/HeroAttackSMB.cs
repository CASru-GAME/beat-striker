using Core.Battle;
using UnityEngine;

public class HeroAttackSMB : StateMachineBehaviour {
    Colliden colliden;
    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex) {
        colliden = animator.GetComponent<StrikerView>().GetColliden("sword");
        colliden.OnEnterTrigger += OnEnterTrigger;
    }

    public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex) {
        colliden.OnEnterTrigger -= OnEnterTrigger;

    }

    void OnEnterTrigger(Collider other) {
        var target = other.GetComponent<StrikerView>();
        if (target != null) {
            target.TakeDamage(new HitStatus(new HitPoint(10)));
        }
    }
    }
