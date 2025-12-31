using Unity.VisualScripting;
using UnityEngine;
using Core.Battle;


public class EmitSMB : StateMachineBehaviour {
    [SerializeField] private Colliden spawnPrefab;
    [SerializeField] private Vector2 spawnPosition;
    [SerializeField] private float damage;
    private IStrikerView strikerView;
    private Colliden spawnedObject;


    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex) {
        var strikerTransform = animator.GetComponent<Transform>();
        strikerView = animator.GetComponent<StrikerView>();
        var forwardDirection = strikerView.GetForwardDirection();

        var strikerPosition = (Vector2)strikerTransform.position;
        var relativeSpawnPosition = new Vector2(spawnPosition.x * forwardDirection.x, spawnPosition.y * forwardDirection.x);
        var worldSpawnPosition = strikerPosition + relativeSpawnPosition;

        var rotation = Quaternion.LookRotation(new Vector3(forwardDirection.x, 0, forwardDirection.y));
        spawnedObject = Instantiate(spawnPrefab, worldSpawnPosition, rotation);
        spawnedObject.OnEnterCollision += OnEnterCollision;
    }

    public void OnEnterCollision(Collision collision) {
        var hitTarget = collision.gameObject.GetComponentInParent<IStrikerHit>();
        if (hitTarget == null) return;
        hitTarget.GiveHit(new HitStatus(new HitPoint(damage)));
    }

    public override void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex) {

    }

    public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex) {
        spawnedObject.OnEnterCollision -= OnEnterCollision;
    }
}
