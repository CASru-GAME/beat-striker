
using System;
using UnityEngine;
using Core.App.Types;
using Core.GamePad.Types;

namespace Core.Battle {

    public interface IStrikerModel : IStrikerModelGetter {
        public void TakeDamage(HitPoint damage);
        public void Heal(HitPoint heal);
        public void GainSpecial(SpecialPoint gain);
        public void AddBeatResult(BeatResult result);
        public void GainSpecial();
        public void Reset();

        public void HandleInput(GamePadInput input);
        public void HandleDirection(Vector2 direction);
        public void SetInputEnabled(bool isEnabled);

        // Observable subscriptions
        IDisposable SubscribeHitPoint(Action<HitPoint> listener);
        IDisposable SubscribeSpecialPoint(Action<SpecialPoint> listener);
        IDisposable SubscribeBeatResult(Action<BeatResult> listener);
        IDisposable SubscribeDied(Action listener);
        IDisposable SubscribeReset(Action listener);

        // Action subscriptions
        IDisposable SubscribeAttack(Action listener);
        IDisposable SubscribeDash(Action listener);
        IDisposable SubscribeCharge(Action listener);
        IDisposable SubscribeGuard(Action listener);
        IDisposable SubscribeMiss(Action listener);
        IDisposable SubscribeSpecial(Action listener);
    }

    public interface IStrikerModelGetter {
        public Vector2 InputDirection { get; }
        public bool IsInputEnabled { get; }
        public PlayerId PlayerId { get; }
        public HitPoint MaxHitPoint { get; }
        public HitPoint HitPoint { get; }
        public SpecialPoint SpecialPoint { get; }
        public SpecialPoint MaxSpecialPoint { get; }
        public int MissCount { get; }
        public int GoodCount { get; }
        public int ExcellentCount { get; }
        public int Score { get; }
        public int ComboCount { get; }
        public bool IsDead();
    }
}
