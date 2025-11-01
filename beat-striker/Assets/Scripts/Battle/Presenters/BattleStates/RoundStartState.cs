using Core.App.Types;
using Core.Utils;
using UnityEngine;

namespace Core.Battle {
    public class RoundStartState : IBattleState {
        private readonly IBattleStateMutator mutator;
        private readonly IBus bus;
        private readonly IBattleModel battleModel;
        private readonly IRythmTrackModel rythmTrackModel;
        private readonly IBattleResetter resetter;
        private readonly IBattleView view;
        private readonly TrackId trackId;

        public RoundStartState(IBattleStateMutator mutator, IBus bus, IBattleModel battleModel, IRythmTrackModel rythmTrackModel, IBattleResetter resetter, IBattleView view, TrackId trackId) {
            this.mutator = mutator;
            this.bus = bus;
            this.battleModel = battleModel;
            this.rythmTrackModel = rythmTrackModel;
            this.resetter = resetter;
            this.view = view;
            this.trackId = trackId;
        }

        public void Enter() {
            Debug.Log("Entering Round Start State");
            bus.Subscribe<BattleMessages.NotifyRoundStartAnimationFinished>(OnRoundStartAnimationFinished);
            bus.Publish(new BattleMessages.OnRoundStarted(battleModel));
        }
        public void OnUpdate(float deltaTime) {
        }

        public void Exit() {
            bus.Unsubscribe<BattleMessages.NotifyRoundStartAnimationFinished>(OnRoundStartAnimationFinished);
        }

        private void OnRoundStartAnimationFinished(BattleMessages.NotifyRoundStartAnimationFinished msg) {
            Debug.Log("Round Start Animation Finished");
            mutator.ChangeState(new RoundState(mutator, bus, battleModel, rythmTrackModel, resetter, view, trackId));
        }
    }
}
