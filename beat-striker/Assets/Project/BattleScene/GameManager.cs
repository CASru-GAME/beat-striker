using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour {
    public const int STRIKER_COUNT = 2;
    public static GameManager Instance { get; private set; }
    [SerializeField] Transform[] spawnPositions;
    [NonSerialized] public Striker[] strikers;
    [SerializeField] CPUPlayer cpuPrefab;
    [SerializeField] float despawnY = -10f;

    private void Awake() {
        if (Instance != null && Instance != this) {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start() {
        App.Instance.cursorMode = false;
        strikers = new Striker[STRIKER_COUNT];
        for (int i = 0; i < STRIKER_COUNT; i++) {
            Player player = i >= App.Instance.players.Count ? null : App.Instance.players[i];
            if (!player) player = Instantiate(cpuPrefab);
            strikers[i] = Instantiate(player.strikerPrefab, spawnPositions[i].position, spawnPositions[i].rotation, null);
            strikers[i].player = player;
        }
    }

    // Update is called once per frame
    void Update() {
        bool isGameSet = false;
        foreach (var striker in strikers) {
            isGameSet |= striker.hp <= 0;
            if (striker.transform.position.y <= despawnY) {
                striker.hp = 0;
            }
        }
        if (isGameSet) SceneManager.LoadScene("ResultScene");
    }

    private void OnDestroy() {
        App.Instance.cursorMode = true;
    }
}
