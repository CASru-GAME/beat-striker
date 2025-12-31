using Core.App.Types;
using Core.Utils;
using UnityEngine;

namespace Core.Battle {
    public class RoundState : IBattleState {
        private readonly IBattleStateMutator mutator;
        private readonly IBus bus;
        private readonly IBattleModel battleModel;
        private readonly IRythmTrackModel rythmTrackModel;
        private readonly IBattleResetter resetter;
        private readonly IBattleView view;
        private readonly TrackId trackId;

        public RoundState(IBattleStateMutator mutator, IBus bus, IBattleModel battleModel, IRythmTrackModel rythmTrackModel, IBattleResetter resetter, IBattleView view, TrackId trackId) {
            this.mutator = mutator;
            this.bus = bus;
            this.battleModel = battleModel;
            this.rythmTrackModel = rythmTrackModel;
            this.resetter = resetter;
            this.view = view;
            this.trackId = trackId;
        }

        public void Enter() {
            Debug.Log("Entering Round State");
            view.PlayTrack(trackId);
            bus.Publish(new BattleMessages.OnBattleStarted(battleModel));
            bus.Subscribe<BattleMessages.NotifyPlayerDead>(ProcessPlayerDeathNotification);
        }

        public void OnUpdate(float deltaTime) {
        }

        public void Exit() {
            view.StopTrack();
            bus.Publish(new BattleMessages.OnBattleFinished(battleModel));
            bus.Unsubscribe<BattleMessages.NotifyPlayerDead>(ProcessPlayerDeathNotification);
        }

        private void ProcessPlayerDeathNotification(BattleMessages.NotifyPlayerDead message) {
            battleModel.AddLoser(message.playerId);
            if (!battleModel.IsFinished()) {
                mutator.ChangeState(new RoundFinishState(mutator, bus, battleModel, rythmTrackModel, resetter, view, trackId));
            }
            else {
                mutator.ChangeState(new OutroState(mutator, bus, battleModel, rythmTrackModel, resetter, view, trackId));
            }
        }
    }
}
