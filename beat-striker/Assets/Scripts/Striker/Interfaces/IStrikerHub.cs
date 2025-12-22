using Core.App.Types;
using Core.Battle;
using UnityEngine;

namespace Core.Striker
{
    public interface IStrikerHub : Core.Battle.IStrikerView
    {
        void ChangeState(IStrikerState state);
        void PlayAnimation(AnimationClip clip, float fadeTime = 0f, float speed = 1f, System.Action onComplete = null);
        Vector2 InputDirection { get; }
        void ApplyDamage(HitPoint damage);
        Rigidbody Rigidbody { get; }
    }
}
