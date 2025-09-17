using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

public class Battle : MonoBehaviour {
    public const int STRIKER_COUNT = 2;
    public static Battle Instance { get; private set; }
    public bool isBattleStarted { get; private set; } = false;
    [SerializeField] AudioClip beatClip; 
    [SerializeField] float beatOffset; 
    

    [SerializeField] Transform[] spawnPositions;
    [NonSerialized] public Striker[] strikers;
    [SerializeField] CPUPlayer cpuPrefab;
    [SerializeField] float despawnY = -10f;


    public float musicTime { get; private set; }
    Beat[] beatMap;
    int nextBeatSpawnIndex;
    int nextBeatIndex;
    public float beatSpawnTimeDelta = 3f;
    [SerializeField] float beatMapTestSpan = 1f;

    public event Action OnBattleStart, OnBattleEnd;

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
            if (!player && i == 0) SceneManager.LoadScene("SelectScene");
            if (!player) player = Instantiate(cpuPrefab);
            strikers[i] = Instantiate(player.strikerPrefab, spawnPositions[i].position, spawnPositions[i].rotation, null);
            strikers[i].player = player;
        }

        beatMap = new Beat[1000];
        for (int i = 0; i < beatMap.Length; i++) {
            beatMap[i] = new Beat(1f + beatMapTestSpan * i);
        }
        musicTime = 0;
        nextBeatSpawnIndex = 0;
        nextBeatIndex = 0;
        
        StartCoroutine(StartBattle());
    }

    IEnumerator StartBattle() {
        yield return new WaitForSeconds(1f);

        Debug.Log("Battle Start!");
        OnBattleStart?.Invoke();
        isBattleStarted = true;
    }

    // Update is called once per frame
    void Update() {
        if (!isBattleStarted) return;

        musicTime += Time.deltaTime;
        if (nextBeatSpawnIndex < beatMap.Length && beatMap[nextBeatSpawnIndex].time < musicTime + beatSpawnTimeDelta) {
            Array.ForEach(strikers, s => s.beats.Add(beatMap[nextBeatSpawnIndex]));
            nextBeatSpawnIndex++;
        }

        if (nextBeatIndex < beatMap.Length && beatMap[nextBeatIndex].time < musicTime - beatOffset) {
            AudioSource.PlayClipAtPoint(beatClip,transform.position);
            nextBeatIndex++;
        }

        bool isGameSet = false;
        foreach (var striker in strikers) {
            isGameSet |= striker.hp <= 0;
            if (striker.transform.position.y <= despawnY) {
                striker.hp = 0;
            }
        }
        if (isGameSet) {
            OnBattleEnd?.Invoke();
            SceneManager.LoadScene("ResultScene");
        }
    }

    private void OnDestroy() {
        App.Instance.cursorMode = true;
        Instance = null;
    }
}