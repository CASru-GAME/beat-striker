

namespace Core.Battle {

    public interface IBattleState {
        void Enter();
        void Exit();
        void OnUpdate(float deltaTime);
    }
}