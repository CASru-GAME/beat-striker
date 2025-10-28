namespace Core.Battle {

    public interface IBattleStateMutator {

        void ChangeState(IBattleState newState);
    
        void OnUpdate(float deltaTime);
    }
}