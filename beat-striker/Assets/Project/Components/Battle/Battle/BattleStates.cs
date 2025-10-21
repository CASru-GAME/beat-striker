using System;
using System.Collections;
using System.Collections.Generic;
using Codice.Client.BaseCommands;
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
        SortedDictionary<int, IEnumerator> strikerAnimes = new();
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
            if (stageAnime != null) yield return stageAnime;
            
            foreach (var anime in strikerAnimes.Values) {
                yield return anime;
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
            Instance.strikers.RebindPlayers();
            if (preState is PausedState) {
                OnResume?.Invoke();
            }
            else {
                Debug.Log("GameStart");
                OnEnter?.Invoke();
                Music.Instance.StartMusic();
            }
        }

        internal override void OnUpdateEvent(float deltaTime) {
            Music.Instance.UpdateMusic(deltaTime);

            if(Instance.strikers.Rank()) Instance.ChangeState(Instance.outroState);
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
            Instance.strikers.SliceByRank(1).ForEach(s => s.Striker.gameObject.SetActive(false));
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