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
        IEnumerator stageAnime, readyAnime;
        IEnumerator[] strikerAnimes = new IEnumerator[STRIKER_COUNT];
        private Coroutine currentCoroutine;

        internal IntroState() {
        }

        internal override void OnEnterEvent(State preState) {
            currentCoroutine = Instance.StartCoroutine(Anime());
        }

        internal override void OnUpdateEvent(float deltaTime) {
        }

        internal override void OnExitEvent(State nextState) {
            if (currentCoroutine != null) {
                Instance.StopCoroutine(currentCoroutine);
                currentCoroutine = null;
            }
            OnExit?.Invoke();
        }

         public void SetStageAnime(IEnumerator animation) {
            this.stageAnime = animation;
        }

         public void SetReadyAnime(IEnumerator animation) {
            this.readyAnime = animation;
        }

         public void SetStrikerAnime(int strikerNumber,IEnumerator animation) {
            this.strikerAnimes[strikerNumber] = animation;
        }

        public void Skip() {
            Instance.ChangeState(Instance.playingState);
        }

        IEnumerator Anime() {
            if(stageAnime != null) yield return stageAnime;
            for (int i = 0; i < STRIKER_COUNT; i++) {
                var anime = strikerAnimes[i];
                if(anime != null) yield return anime;
            }
            if(readyAnime != null) yield return readyAnime;
            Instance.ChangeState(Instance.playingState);
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
                Instance.Winner = -1;
            }
        }

        internal override void OnUpdateEvent(float deltaTime) {
            Music.Instance.UpdateMusic(deltaTime);

            for (int i = 0; i < Instance.strikers.Length; i++) {
                var striker = Instance.strikers[i];
                if (Instance.nextRank == 1) {
                    striker.Rank = Instance.nextRank;
                    Instance.Winner = i;
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
        IEnumerator victoryAnime;
        private Coroutine currentCoroutine;

        internal OutroState() {
        }

        internal override void OnEnterEvent(State preState) {
            OnEnter?.Invoke();
            currentCoroutine = Instance.StartCoroutine(Anime());
        }

        internal override void OnUpdateEvent(float deltaTime) {
        }

        internal override void OnExitEvent(State nextState) {
            if (currentCoroutine != null) {
                Instance.StopCoroutine(currentCoroutine);
                currentCoroutine = null;
            }
            OnExit?.Invoke();
        }

         public void SetVictoryAnime(IEnumerator animation) {
            this.victoryAnime = animation;
        }

        IEnumerator Anime() {
            Instance.strikers[1 - Instance.Winner].gameObject.SetActive(false);
            if (victoryAnime != null) yield return victoryAnime;
            Instance.ChangeState(Instance.resultState);
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