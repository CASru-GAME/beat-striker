using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public partial class Battle {
    public interface IBattleState {
        public abstract void OnEnterEvent(Battle battle, IBattleState preState);
        public abstract void OnUpdateEvent(Battle battle, float deltaTime);
        public abstract void OnExitEvent(Battle battle, IBattleState nextState);
    }

    public class BattleReadyState : IBattleState {
        public event Action OnEnter, OnExit;

        public BattleReadyState(){}

        public void OnEnterEvent(Battle battle, IBattleState preState) {
            battle.StartCoroutine(StartBattleDelayed(battle));
            
            OnEnter?.Invoke();
        }

        public void OnUpdateEvent(Battle battle, float deltaTime) {
        }

        public void OnExitEvent(Battle battle, IBattleState nextState) {
            OnExit?.Invoke();
        }

        private IEnumerator StartBattleDelayed(Battle battle) {
            yield return new WaitForSeconds(1f);

            Debug.Log("Battle Start!");

            battle.ChangeState(battle.playingState);
        }
    }

    public class BattlePlayingState : IBattleState {
        public event Action OnEnter, OnPause, OnResume, OnExit;

        public void OnEnterEvent(Battle battle, IBattleState preState) {
            battle.RebindPlayers();
            if (preState is BattlePausedState) {
                OnResume?.Invoke();
            }
            else OnEnter?.Invoke();
        }

        public void OnUpdateEvent(Battle battle, float deltaTime) {
            battle.UpdateMusicTime(deltaTime);

            if (battle.CheckGameSet()) {
                battle.ChangeState(battle.finishState);
            }
        }

        public void OnExitEvent(Battle battle, IBattleState nextState) {
            if (nextState is BattlePausedState) {
                OnPause?.Invoke();
            }
            else OnExit?.Invoke();
        }
    }

    public class BattleFinishState : IBattleState {
        public event Action OnEnter, OnExit;

        public void OnEnterEvent(Battle battle, IBattleState preState) {
            OnEnter?.Invoke();
            SceneManager.LoadScene("ResultScene");
        }

        public void OnUpdateEvent(Battle battle, float deltaTime) {
        }

        public void OnExitEvent(Battle battle, IBattleState nextState) {
            OnExit?.Invoke();
        }
    }

    public class BattlePausedState : IBattleState {
        public event Action OnEnter, OnExit;

        public void OnEnterEvent(Battle battle, IBattleState preState) {
            App.Instance.cursorMode = true;
            OnEnter?.Invoke();
        }

        public void OnUpdateEvent(Battle battle, float deltaTime) {
        }

        public void OnExitEvent(Battle battle, IBattleState nextState) {
            App.Instance.cursorMode = false;
            OnExit?.Invoke();
        }
    }
}