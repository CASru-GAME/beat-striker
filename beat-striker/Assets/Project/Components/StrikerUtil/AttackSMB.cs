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
        colliden.OnEnterCollision += OnEnterCollision;
    }

    public void OnEnterCollision(Collision collision) {
        var view = collision.gameObject.GetComponent<StrikerView>();
        if (view == null) return;
        view.TakeDamage(new HitStatus(new HitPoint(damage)));
    }

    public override void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex) {

    }

    public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex) {
        colliden.OnEnterCollision -= OnEnterCollision;
    }
}
