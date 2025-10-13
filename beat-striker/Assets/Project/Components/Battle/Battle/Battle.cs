using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

public partial class Battle : MonoBehaviour {
    public static Battle Instance { get; private set; }

    [SerializeField] internal float despawnY = -10f;

    [NonSerialized] public readonly Strikers strikers = new(2);
    [SerializeField] StrikerPrefab[] strikerPrefabs;

    public readonly IntroState introState = new();
    public readonly PlayingState playingState = new();
    public readonly PausedState pausedState = new();
    public readonly OutroState outroState = new();
    public readonly ResultState resultState = new();
    private State currentState;

    private void Awake() {
        Instance = this;

        App.Instance.OnPlayerJoin += OnPlayerJoin;
        App.Instance.OnEscape += OnEscape;
        App.Instance.cursorMode = false;

        var spawnTransforms = Enumerable.Range(0, strikers.Count)
            .Select(i => GameObject.Find($"SpawnPosition{i}").transform);

        strikers.Spawn(strikerPrefabs, spawnTransforms);
    }

    void Start() {
        ChangeState(introState);
    }

    void OnDestroy() {
        ChangeState(null);

        Instance = null;
        App.Instance.cursorMode = true;
        App.Instance.OnPlayerJoin -= OnPlayerJoin;
        App.Instance.OnPlayerJoin -= OnEscape;
    }

    void Update() {
        currentState?.OnUpdateEvent(Time.deltaTime);
    }

    void OnPlayerJoin(Player p) {
        strikers.RebindPlayers();
    }

    void OnEscape(Player p) {
        if (currentState == introState) introState.Skip();
    }

    void ChangeState(State newState) {
        currentState?.OnExitEvent(newState);
        newState?.OnEnterEvent(currentState);
        currentState = newState;
    }
}

[System.Serializable]
public class StrikerPrefab {
    public StrikerType type;
    public Striker prefab;
}