
using Core.Utils;

namespace Core.Battle {
    public class BattleFlowPresenter : IBattleStateMutator {
        private IBattleState currentState;
        private readonly IBus bus;

        public BattleFlowPresenter(IBattleState initialState, IBus bus, ILife life) {
            this.bus = bus;
            currentState = initialState;
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