using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;


public partial class Battle {
    public abstract class State {
        internal abstract void OnEnterEvent(State preState);
        internal abstract void OnUpdateEvent(float deltaTime);
        internal abstract void OnExitEvent(State nextState);
    }

    public class IntroState : State {
        public event Action OnExit;
        private float timer = 0f;
        private const float INTRO_TIMEOUT = 30f;

        internal IntroState() {
        }

        internal override void OnEnterEvent(State preState) {
            //基本呼ばれません
            timer = 0f;
        }

        internal override void OnUpdateEvent(float deltaTime) {
            timer += deltaTime;
            if (timer >= INTRO_TIMEOUT) {
                Debug.Log("IntroState timeout - transitioning to PlayingState");
                Instance.ChangeState(Instance.playingState);
            }
        }

        internal override void OnExitEvent(State nextState) {
            OnExit?.Invoke();
        }
    }

    public class PlayingState : State {
        public event Action OnEnter, OnPause, OnResume, OnExit;

        internal PlayingState() {
        }

        internal override void OnEnterEvent(State preState) {
            Instance.RebindPlayers();
            if (preState is PausedState) {
                OnResume?.Invoke();
            }
            else {
                Debug.Log("GameStart");
                OnEnter?.Invoke();
                Music.Instance.StartMusic();
                Instance.nextRank = STRIKER_COUNT;
            }
        }

        internal override void OnUpdateEvent(float deltaTime) {
            Music.Instance.UpdateMusic(deltaTime);

            foreach (var striker in Instance.strikers) {
                if (Instance.nextRank == 1) {
                    striker.Rank = Instance.nextRank;
                    Instance.ChangeState(Instance.outroState);
                    break;
                }
                else if (striker.Rank < Instance.nextRank && striker.Hp <= 0) {
                    striker.Rank = Instance.nextRank;
                    Instance.nextRank--;
                }
            }
        }

        internal override void OnExitEvent(State nextState) {
            if (nextState is PausedState) {
                OnPause?.Invoke();
            }
            else OnExit?.Invoke();
        }
    }

    public class OutroState : State {
        public event Action OnEnter, OnExit;
        private float timer = 0f;
        private const float OUTRO_TIMEOUT = 30f;

        internal OutroState() {
        }

        internal override void OnEnterEvent(State preState) {
            OnEnter?.Invoke();
            timer = 0f;
        }

        internal override void OnUpdateEvent(float deltaTime) {
            timer += deltaTime;
            if (timer >= OUTRO_TIMEOUT) {
                Debug.Log("OutroState timeout - transitioning to ResultState");
                Instance.ChangeState(Instance.resultState);
            }
        }

        internal override void OnExitEvent(State nextState) {
            OnExit?.Invoke();
        }
    }

    public class ResultState : State {
        public event Action OnEnter, OnExit;

        internal ResultState() {
        }

        internal override void OnEnterEvent(State preState) {
            OnEnter?.Invoke();
            SceneManager.LoadScene("ResultScene");
        }

        internal override void OnUpdateEvent(float deltaTime) {
        }

        internal override void OnExitEvent(State nextState) {
            OnExit?.Invoke();
        }
    }

    public class PausedState : State {
        public event Action OnEnter, OnExit;

        internal PausedState() {
        }

        internal override void OnEnterEvent(State preState) {
            App.Instance.cursorMode = true;
            OnEnter?.Invoke();
        }

        internal override void OnUpdateEvent(float deltaTime) {
        }

        internal override void OnExitEvent(State nextState) {
            App.Instance.cursorMode = false;
            OnExit?.Invoke();
        }
    }
}