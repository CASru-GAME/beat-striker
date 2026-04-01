using Core.App.Types;
using R3;
using UnityEngine;

namespace Alice {
    public interface IStrikerHub : IReadOnlyBattleEntity, System.IDisposable {
        float CurrentHitPoint { get; }
        ReadOnlyReactiveProperty<float> CurrentHitPointReactive { get; }
        ReadOnlyReactiveProperty<float> HitPointRatio { get; }
        AiBrain AiBrain { get; }
        Rigidbody Rigidbody { get; }
        Observable<PlayerId> OnDeadEvent { get; }

        void SetPlayerId(int playerId);
        void Tick(float deltaTime);
        void ChangeDirection(Vector2 direction);
        void CancelDirection();
        void Dash();
        void Attack();
        void Charge();
        void Special();
        void Guard();
        void Die();
        void GiveHit(HitStatus status);
    }
}