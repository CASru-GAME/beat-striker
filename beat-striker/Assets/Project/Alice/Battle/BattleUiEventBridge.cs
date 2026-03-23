using System;
using Core.Battle;
using Core.Utils;
using UnityEngine;

namespace Alice {
    public sealed class BattleUiEventBridge : MonoBehaviour {
        IBus bus;

        public event Action<int> RoundStarted;
        public event Action BattleFinished;
        public event Action OutroStarted;
        public event Action ResultStarted;

        void Awake() {
            bus = this.GetBus();
            bus.Subscribe<BattleMessages.OnRoundStarted>(OnRoundStarted);
            bus.Subscribe<BattleMessages.OnBattleFinished>(OnBattleFinished);
            bus.Subscribe<BattleMessages.OnOutroStarted>(OnOutroStarted);
            bus.Subscribe<BattleMessages.OnResultStarted>(OnResultStarted);
        }

        void OnDestroy() {
            bus.Unsubscribe<BattleMessages.OnRoundStarted>(OnRoundStarted);
            bus.Unsubscribe<BattleMessages.OnBattleFinished>(OnBattleFinished);
            bus.Unsubscribe<BattleMessages.OnOutroStarted>(OnOutroStarted);
            bus.Unsubscribe<BattleMessages.OnResultStarted>(OnResultStarted);
        }

        public void NotifyRoundStartAnimationFinished() {
            bus.Publish(new BattleMessages.NotifyRoundStartAnimationFinished());
        }

        public void NotifyRoundFinishAnimationFinished() {
            bus.Publish(new BattleMessages.NotifyRoundFinishAnimationFinished());
        }

        void OnRoundStarted(BattleMessages.OnRoundStarted msg) {
            RoundStarted?.Invoke(msg.battlemodel.GetCurrentRound() + 1);
        }

        void OnBattleFinished(BattleMessages.OnBattleFinished _) {
            BattleFinished?.Invoke();
        }

        void OnOutroStarted(BattleMessages.OnOutroStarted _) {
            OutroStarted?.Invoke();
        }

        void OnResultStarted(BattleMessages.OnResultStarted _) {
            ResultStarted?.Invoke();
        }
    }
}