using Core.App.Types;
using Core.Battle;
using UnityEngine;

namespace Core.Striker
{
    public interface IStrikerHub : Core.Battle.IStrikerView
    {
        void ChangeState(IStrikerState state);
        void PlayAnimation(AnimationClip clip, System.Action onComplete = null);
        Vector2 InputDirection { get; }
        void ApplyDamage(HitPoint damage);
    }
}
