using Unity.VisualScripting;
using UnityEngine;
using Core.Battle;


public class AttackSMB : StateMachineBehaviour {
    [SerializeField] private string collidenKey;
    [SerializeField] private float damage;
    private StrikerView strikerView;
    private Colliden colliden;

    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex) {
        strikerView = animator.GetComponent<StrikerView>();
        colliden = strikerView.GetColliden(collidenKey);
        colliden.OnEnterTrigger += OnEnterTrigger;
    }

    public void OnEnterTrigger(Collider collider) {
        Debug.Log($"{strikerView.gameObject.name} collided with {collider.gameObject.name}");
            var hitTarget = collider.gameObject.GetComponentInParent<IStrikerHit>();
            if (hitTarget != null) hitTarget.GiveHit(new HitStatus(new HitPoint(damage)));
    }

    public override void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex) {

    }

    public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex) {
        colliden.OnEnterTrigger -= OnEnterTrigger;
    }
}
