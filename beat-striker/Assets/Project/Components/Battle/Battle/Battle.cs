using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

public partial class Battle : MonoBehaviour {
    public const int STRIKER_COUNT = 2;
    public static Battle Instance { get; private set; }
    private int nextRank;

    [SerializeField] float despawnY = -10f;

    [NonSerialized] public Striker[] strikers = new Striker[STRIKER_COUNT];

    public readonly IntroState introState = new();
    public readonly PlayingState playingState = new();
    public readonly PausedState pausedState = new();
    public readonly OutroState outroState = new();
    public readonly ResultState resultState = new();
    private State currentState;

    [SerializeField] StrikerPrefab[] strikerPrefabs;

    private void Awake() {
        if (Instance != null && Instance != this) {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        App.Instance.OnPlayerJoin += OnPlayerJoin;
        App.Instance.cursorMode = false;

        for (int i = 0; i < STRIKER_COUNT; i++) {
            Player player = i >= App.Instance.players.Count ? null : App.Instance.players[i];
            if (!player) player = Instantiate(App.Instance.cpuPrefab);
            Transform trans = GameObject.Find($"SpawnPosition{i}").transform;
            strikers[i] = Instantiate(Array.Find(strikerPrefabs, s => s.type == player.striker).prefab, trans.position, trans.rotation, null);
            strikers[i].player = player;
            trans.SetParent(strikers[i].transform);
        }

        ChangeState(introState);
    }
    
    private void OnDestroy() {
        ChangeState(null);

        Instance = null;
        App.Instance.cursorMode = true;
        App.Instance.OnPlayerJoin -= OnPlayerJoin;
    }

    void Update() {
        currentState?.OnUpdateEvent(Time.deltaTime);
    }

    void RebindPlayers() {
        for (int i = 0; i < STRIKER_COUNT; i++) {
            Player player = i >= App.Instance.players.Count ? null : App.Instance.players[i];
            if (player) strikers[i].player = player;
        }
    }

    private void OnPlayerJoin(Player p) {
        RebindPlayers();
    }

    public void ChangeState(State newState) {
        currentState?.OnExitEvent(newState);
        newState?.OnEnterEvent(currentState);
        currentState = newState;
    }
}


[Serializable]
public class StrikerPrefab {
    public StrikerType type;
    public Striker prefab;
}