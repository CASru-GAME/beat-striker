using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(BattleMusicController))]
public partial class Battle : MonoBehaviour {
    public const int STRIKER_COUNT = 2;
    public static Battle Instance { get; private set; }
    public bool IsBattleStarted => currentState == playingState;

    [SerializeField] CPUPlayer cpuPrefab;
    [SerializeField] float despawnY = -10f;
    public BattleMusicController Music { get; private set; }

    public float beatSpawnTimeDelta = 3f;

    Transform[] spawnPositions;
    [NonSerialized] public Striker[] strikers;

    private IBattleState currentState;
    public readonly BattleReadyState readyState = new();
    public readonly BattlePlayingState playingState = new();
    public readonly BattlePausedState pausedState = new();
    public readonly BattleFinishState finishState = new();

    private void Awake() {
        if (Instance != null && Instance != this) {
            Destroy(gameObject);
            return;
        }

        App.Instance.OnPlayerJoin += OnPlayerJoin;

        Instance = this;
    }

    void Start() {
        App.Instance.cursorMode = false;
        spawnPositions = new Transform[STRIKER_COUNT];
        for (int i = 0; i < STRIKER_COUNT; i++) {
            GameObject obj = GameObject.Find($"SpawnPosition{i}");
            if (obj != null)
                spawnPositions[i] = obj.transform;
            else
                Debug.LogError($"SpawnPosition{i} not found in scene");
        }

        Music = GetComponent<BattleMusicController>();

        strikers = new Striker[STRIKER_COUNT];
        for (int i = 0; i < STRIKER_COUNT; i++) {
            Player player = i >= App.Instance.players.Count ? null : App.Instance.players[i];
            if (!player) player = Instantiate(cpuPrefab);
            strikers[i] = Instantiate(player.strikerPrefab, spawnPositions[i].position, spawnPositions[i].rotation, null);
            strikers[i].player = player;
        }

        ChangeState(readyState);
    }

    void Update() {
        currentState.OnUpdateEvent(this, Time.deltaTime);
    }

    public void RebindPlayers() {
        for (int i = 0; i < STRIKER_COUNT; i++) {
            Player player = i >= App.Instance.players.Count ? null : App.Instance.players[i];
            if (player) strikers[i].player = player;
        }
    }

    private void OnPlayerJoin(Player p) {
        RebindPlayers();
    }

    public void ChangeState(IBattleState newState) {
        currentState?.OnExitEvent(this, newState);
        newState?.OnEnterEvent(this, currentState);
        currentState = newState;
    }

    public bool CheckGameSet() {
        bool isGameSet = false;
        foreach (var striker in strikers) {
            isGameSet |= striker.hp <= 0;
            if (striker.transform.position.y <= despawnY) {
                striker.hp = 0;
            }
        }
        return isGameSet;
    }

    private void OnDestroy() {
        ChangeState(null);

        Instance = null;
        App.Instance.cursorMode = true;
        App.Instance.OnPlayerJoin -= OnPlayerJoin;
    }
}
