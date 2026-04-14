using UnityEngine;

public class DeathState : StrikerState {
    public override Alice.StrikerStateCategory Category => Alice.StrikerStateCategory.Unknown;

    [SerializeField] StrikerAnimationClip animationClip;
    [SerializeField] bool resetVelocityOnEnter = true;

    public override void OnEnter(IStrikerContext context) {
        if (resetVelocityOnEnter) {
            context.Rigidbody.linearVelocity = Vector3.zero;
            context.Rigidbody.angularVelocity = Vector3.zero;
        }

        context.PlayAnimation(animationClip);
    }
}


