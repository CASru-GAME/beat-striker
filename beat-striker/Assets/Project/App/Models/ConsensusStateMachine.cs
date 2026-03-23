using System;
using R3;

namespace App {

    public interface IConsensusRequest {
        public void Diny();
        public void Approve();
    }

    public interface IConsensusStateCallback {
        public void OnEnter();
        public ReadOnlyReactiveProperty<bool> CanExit { get; }
        public void OnExitRequested();
        public void OnExit();
    }


    public interface IConsunsusStateOwnership {
        public bool IsValid { get; }
        public void Release();
    }

    public interface IOwnableConsensusState {
        public IConsunsusStateOwnership Own();
        public Observable<Unit> OnEnter { get; }
        public Observable<Unit> OnExitRequested { get; }
        public Observable<Unit> OnExit { get; }
    }

    public interface IConsensusStateMachine {
        public ChangeStateResult RequestChangeState(IConsensusStateCallback newState = null);

        public enum ChangeStateResult {
            Success,
            WaitingForCurrentStateToExit,
            AlreadyPending,
        }
    }

    public class ConsensusState : IConsensusStateCallback, IOwnableConsensusState {
        private int totalOwners = 0;
        private readonly ReactiveProperty<bool> canExit = new(true);
        private readonly Subject<Unit> onEnter = new();
        private readonly Subject<Unit> onExitRequested = new();
        private readonly Subject<Unit> onExit = new();

        ReadOnlyReactiveProperty<bool> IConsensusStateCallback.CanExit => canExit;
        Observable<Unit> IOwnableConsensusState.OnEnter => onEnter;
        Observable<Unit> IOwnableConsensusState.OnExitRequested => onExitRequested;
        Observable<Unit> IOwnableConsensusState.OnExit => onExit;

        public IConsunsusStateOwnership Own() {
            totalOwners++;
            canExit.Value = false;
            return new Ownership(this);
        }

        void IConsensusStateCallback.OnEnter() {
            onEnter.OnNext(Unit.Default);
        }

        void IConsensusStateCallback.OnExitRequested() {
            onExitRequested.OnNext(Unit.Default);
        }

        void IConsensusStateCallback.OnExit() {
            onExit.OnNext(Unit.Default);
        }

        private class Ownership : IConsunsusStateOwnership {
            private readonly ConsensusState parent;
            public Ownership(ConsensusState parent) {
                this.parent = parent;
            }
            public bool IsValid { get; private set; } = true;
            public void Release() {
                if(!IsValid) return;
                IsValid = false;
                parent.totalOwners--;
                if (parent.totalOwners == 0) {
                    parent.canExit.Value = true;
                }
            }
        }
    }


    public class ConsensusStateMachine : IConsensusStateMachine {
        readonly ConsensusState defaultState = new();
        IConsensusStateCallback currentState;
        IConsensusStateCallback pendingState;
        IDisposable pendingCanExitSubscription;

        public ConsensusStateMachine() {
            currentState = defaultState;
        }

        void CommitStateChange(IConsensusStateCallback newState) {
            currentState.OnExit();
            currentState = newState;
            currentState.OnEnter();
        }

        void CommitPendingStateChange() {
            if (pendingState == null) return;

            var nextState = pendingState;
            pendingState = null;

            pendingCanExitSubscription?.Dispose();
            pendingCanExitSubscription = null;

            CommitStateChange(nextState);
        }

        public IConsensusStateMachine.ChangeStateResult RequestChangeState(IConsensusStateCallback newState = null) {
            if (pendingState != null) return IConsensusStateMachine.ChangeStateResult.AlreadyPending;

            newState ??= defaultState;

            currentState.OnExitRequested();

            if (!currentState.CanExit.CurrentValue) {
                pendingState = newState;

                pendingCanExitSubscription?.Dispose();
                pendingCanExitSubscription = currentState.CanExit
                    .Where(canExit => canExit)
                    .Take(1)
                    .Subscribe(_ => CommitPendingStateChange());

                if (currentState.CanExit.CurrentValue) {
                    CommitPendingStateChange();
                }

                return IConsensusStateMachine.ChangeStateResult.AlreadyPending;
            }

            CommitStateChange(newState);
            return IConsensusStateMachine.ChangeStateResult.Success;
        }
    }
}