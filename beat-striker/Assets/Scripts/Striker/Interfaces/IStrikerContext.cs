using Core.App.Types;
using Core.Battle;
using UnityEngine;

namespace Core.Striker {
    public interface IStrikerContext{
        Vector2 InputDirection { get; }
        void ApplyDamage(HitPoint damage);
        Rigidbody Rigidbody { get; }
        void PlayAnimation(StrikerAnimationClip animation, System.Action<IStrikerStateContext> onComplete = null);
    }
}
