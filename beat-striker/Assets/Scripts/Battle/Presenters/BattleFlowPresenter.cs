
using Core.Utils;

namespace Core.Battle {
    public class BattleFlowPresenter : IBattleStateMutator {
        private IBattleState currentState;
        private readonly IBus bus;
        private readonly ILife life;
        private readonly IRythmTrackModel rythmTrackModel;

        public BattleFlowPresenter(IBus bus, ILife life, IBattleModel battleModel,IRythmTrackModel rythmTrackModel) {
            this.bus = bus;
            this.life = life;
            this.rythmTrackModel = rythmTrackModel;
            currentState = new IntroState(this, bus, battleModel, rythmTrackModel);
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