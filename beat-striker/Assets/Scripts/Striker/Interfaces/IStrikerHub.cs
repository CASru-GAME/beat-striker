using Core.App.Types;
using UnityEngine;

namespace Core.Striker
{
    public interface IStrikerHub : Core.Battle.IStrikerView
    {
        // Requests
        void ChangeState(IStrikerState state);
        
        // Animation
        void PlayAnimation(AnimationClip clip, System.Action onComplete = null);

        // Properties
        Vector2 Direction { get; }
    }
}
