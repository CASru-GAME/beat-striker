using System;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Battle))]
public class Music : MonoBehaviour {
    [SerializeField] private AudioClip beatClip;
    [SerializeField] private float beatOffset;
    [SerializeField] private float beatMapTestSpan = 1f;
    [SerializeField] float beatSpawnTimeDelta = 3f;

    public float Time { get; private set; }
    Beat[] beatMap;
    int nextBeatSpawnIndex;
    int nextBeatIndex;

    public event Action<Beat> OnBeatSpawn;

    public static Music Instance { get; private set; }

    private void Awake() {
        //BattleのAwakeが先
        Instance = this;
    }

    private void OnDestroy() {
        Instance = null;
    }

    public void StartMusic() {
        beatMap = new Beat[1000];
        for (int i = 0; i < beatMap.Length; i++) {
            beatMap[i] = new Beat(1f + beatMapTestSpan * i);
        }
        Time = 0;
        nextBeatSpawnIndex = 0;
        nextBeatIndex = 0;
    }

    public void UpdateMusic(float deltaTime) {
        Time += deltaTime;

        if (nextBeatSpawnIndex < beatMap.Length &&
            beatMap[nextBeatSpawnIndex].time < Time + beatSpawnTimeDelta) {

            var beat = beatMap[nextBeatSpawnIndex];
            OnBeatSpawn?.Invoke(beat);
            nextBeatSpawnIndex++;
        }

        if (nextBeatIndex < beatMap.Length &&
            beatMap[nextBeatIndex].time < Time - beatOffset) {
            AudioSource.PlayClipAtPoint(beatClip, transform.position);
            nextBeatIndex++;
        }
    }
}