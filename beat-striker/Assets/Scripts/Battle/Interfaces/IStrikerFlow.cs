using UnityEngine;

namespace Core.Battle
{
    public interface IStrikerFlow
    {
        void OnReset();
        void ResetPosition();
        void SavePosition();
        Vector2 GetForwardDirection();
    }
}
