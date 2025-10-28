
using Core.Utils;

namespace Core.Battle {
    public class BattleFlowPresenter : IBattleStateMutator {
        private IBattleState currentState;
        private readonly IBus bus;

        public BattleFlowPresenter(IBattleState initialState, IBus bus, ILife life) {
            this.bus = bus;
            currentState = initialState;
            currentState.Enter();
            life.Link(OnEnable, OnDisable);
        }

        public void ChangeState(IBattleState newState) {
            currentState.Exit();
            currentState = newState;
            currentState.Enter();
        }

        public void OnEnable() {
        }

        public void OnDisable() {
            currentState.Exit();
        }
    }
}