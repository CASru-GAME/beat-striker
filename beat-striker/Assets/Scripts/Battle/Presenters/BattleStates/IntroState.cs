using Core.App.Types;
using Core.Utils;
using UnityEngine;

namespace Core.Battle {
    public class IntroState : IBattleState {
        private readonly IBattleStateMutator mutator;
        private readonly IBus bus;
        private readonly IBattleModel battleModel;
        private readonly IRythmTrackModel rythmTrackModel;
        private readonly IBattleResetter resetter;
        private readonly IBattleView view;
        private readonly TrackId trackId;

        public IntroState(IBattleStateMutator mutator, IBus bus, IBattleModel battleModel, IRythmTrackModel rythmTrackModel, IBattleResetter resetter, IBattleView view, TrackId trackId) {
            this.mutator = mutator;
            this.bus = bus;
            this.battleModel = battleModel;
            this.rythmTrackModel = rythmTrackModel;
            this.resetter = resetter;
            this.view = view;
            this.trackId = trackId;
        }

        public void Enter() {
            Debug.Log("Entering Intro State");
            bus.Subscribe<BattleMessages.NotifyIntroAnimationFinished>(OnIntroAnimationFinished);
        }

        public void OnUpdate(float deltaTime) {
        }

        public void Exit() {
            resetter.ResetBattle();
            bus.Unsubscribe<BattleMessages.NotifyIntroAnimationFinished>(OnIntroAnimationFinished);
        }

        private void OnIntroAnimationFinished(BattleMessages.NotifyIntroAnimationFinished msg) {
            Debug.Log("Intro Animation Finished");
            mutator.ChangeState(new RoundStartState(mutator, bus, battleModel, rythmTrackModel, resetter, view, trackId));
        }
    }
}
