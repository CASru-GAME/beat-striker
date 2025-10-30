
using Core.Utils;

namespace Core.Battle {
    public class BattleFlowPresenter : IBattleStateMutator {
        private IBattleState currentState;
        private readonly IBus bus;
        private readonly ILife life;
        private readonly IRythmTrackModel rythmTrackModel;
        private readonly IBattleResetter resetter;

        public BattleFlowPresenter(IBus bus, ILife life, IBattleModel battleModel,IRythmTrackModel rythmTrackModel, IBattleResetter resetter) {
            this.bus = bus;
            this.life = life;
            this.rythmTrackModel = rythmTrackModel;
            this.resetter = resetter;
            currentState = new IntroState(this, bus, battleModel, rythmTrackModel, resetter);
            life.Link(OnEnable, OnDisable);
        }

        public void OnUpdate(float deltaTime) {
            currentState.OnUpdate(deltaTime);
        }

        public void ChangeState(IBattleState newState) {
            currentState.Exit();
            currentState = newState;
            currentState.Enter();
        }

        public void OnEnable() {
            currentState.Enter();
        }

        public void OnDisable() {
            currentState.Exit();
        }
    }
}