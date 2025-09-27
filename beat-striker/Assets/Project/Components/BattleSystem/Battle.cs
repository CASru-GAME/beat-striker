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


    Transform[] spawnPositions;
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
        // シングルトンパターンの実装：インスタンスが既に存在する場合、自身を破棄
        if (Instance != null && Instance != this) {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start() {
        // カーソルモードを無効化
        App.Instance.cursorMode = false;

        // スポーン位置の初期化
        spawnPositions = new Transform[STRIKER_COUNT];
        for (int i = 0; i < STRIKER_COUNT; i++) {
            GameObject obj = GameObject.Find($"SpawnPosition{i}");
            if (obj != null)
                spawnPositions[i] = obj.transform;
            else
                Debug.LogError($"SpawnPosition{i} not found in scene");
        }
        
        // ストライカーの初期化
        strikers = new Striker[STRIKER_COUNT];
        for (int i = 0; i < STRIKER_COUNT; i++) {
            Player player = i >= App.Instance.players.Count ? null : App.Instance.players[i];
            if (!player && i == 0) SceneManager.LoadScene("TitleScene");
            if (!player) player = Instantiate(cpuPrefab);
            strikers[i] = Instantiate(player.strikerPrefab, spawnPositions[i].position, spawnPositions[i].rotation, null);
            strikers[i].player = player;
        }

        // ビートマップの初期化
        beatMap = new Beat[1000];
        for (int i = 0; i < beatMap.Length; i++) {
            beatMap[i] = new Beat(1f + beatMapTestSpan * i);
        }
        musicTime = 0;
        nextBeatSpawnIndex = 0;
        nextBeatIndex = 0;

        // バトル開始コルーチンの開始
        StartCoroutine(StartBattle());
    }

    IEnumerator StartBattle() {
        // 1秒待機
        yield return new WaitForSeconds(1f);

        // バトル開始のログ出力とイベント発火
        Debug.Log("Battle Start!");
        OnBattleStart?.Invoke();
        isBattleStarted = true;
    }

    // Update is called once per frame
    void Update() {
        // バトルが開始されていない場合は処理をスキップ
        if (!isBattleStarted) return;

        // 音楽時間の更新
        musicTime += Time.deltaTime;

        // ビートのスポーン処理
        if (nextBeatSpawnIndex < beatMap.Length && beatMap[nextBeatSpawnIndex].time < musicTime + beatSpawnTimeDelta) {
            Array.ForEach(strikers, s => s.beats.Add(beatMap[nextBeatSpawnIndex]));
            nextBeatSpawnIndex++;
        }

        // ビートの再生処理
        if (nextBeatIndex < beatMap.Length && beatMap[nextBeatIndex].time < musicTime - beatOffset) {
            AudioSource.PlayClipAtPoint(beatClip, transform.position);
            nextBeatIndex++;
        }

        // ゲームセット判定
        bool isGameSet = false;
        foreach (var striker in strikers) {
            isGameSet |= striker.hp <= 0;
            if (striker.transform.position.y <= despawnY) {
                striker.hp = 0;
            }
        }
        if (isGameSet) {
            // バトル終了イベントの発火とシーン遷移
            OnBattleEnd?.Invoke();
            SceneManager.LoadScene("ResultScene");
        }
    }

    private void OnDestroy() {
        // カーソルモードを有効化し、インスタンスをnullに設定
        App.Instance.cursorMode = true;
        Instance = null;
    }
}