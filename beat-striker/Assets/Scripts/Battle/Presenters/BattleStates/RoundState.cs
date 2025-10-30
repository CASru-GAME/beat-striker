using Core.Utils;
using UnityEngine;

namespace Core.Battle {
    public class RoundState : IBattleState {
        private readonly IBattleStateMutator mutator;
        private readonly IBus bus;
        private readonly IBattleModel battleModel;
        private readonly IRythmTrackModel rythmTrackModel;
        private readonly IBattleResetter resetter;

        public RoundState(IBattleStateMutator mutator, IBus bus, IBattleModel battleModel, IRythmTrackModel rythmTrackModel, IBattleResetter resetter) {
            this.mutator = mutator;
            this.bus = bus;
            this.battleModel = battleModel;
            this.rythmTrackModel = rythmTrackModel;
            this.resetter = resetter;
        }

        public void Enter() {
            Debug.Log("Entering Round State");
            bus.Publish(new BattleMessages.OnBattleStarted(battleModel.GetCurrentRound()));
            bus.Subscribe<BattleMessages.NotifyPlayerDead>(ProcessPlayerDeathNotification);
        }

        public void OnUpdate(float deltaTime) {
            rythmTrackModel.AddTime(deltaTime);
        }

        public void Exit() {
            bus.Publish(new BattleMessages.OnBattleFinished(battleModel.GetCurrentRound()));
            bus.Unsubscribe<BattleMessages.NotifyPlayerDead>(ProcessPlayerDeathNotification);
        }

        private void ProcessPlayerDeathNotification(BattleMessages.NotifyPlayerDead message) {
            battleModel.AddLoser(message.playerId);
            if (!battleModel.IsFinished()) {
                mutator.ChangeState(new RoundFinishState(mutator, bus, battleModel, rythmTrackModel, resetter));
            }
            else {
                mutator.ChangeState(new OutroState(mutator, bus, battleModel, rythmTrackModel, resetter));
            }
        }
    }
}
