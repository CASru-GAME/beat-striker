

using Core.App.Types;
using UnityEngine;


namespace Core.Battle
{

    public interface IStrikerView
    {
        void ChangeDirection(Vector2 direction);
        void CancelDirection();
        void Dash();
        void Attack();
        void Charge();
        void ChargeEnd();
        void Special();
        void Guard();
        void OnMiss();
        void OnHit();
        void OnDead();
        void OnIntro();
        void OnVictory();
        void OnReset();
        HitPoint CalcHit(HitStatus status);
        void ResetPosition();
        void SavePosition();
        Vector2 GetForwardDirection();
    }
}