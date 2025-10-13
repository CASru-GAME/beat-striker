using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[AddComponentMenu(" Striker", 0)]
public class Striker : MonoBehaviour {
    public float maxHp;
    public float hp{ get; private set; }
    [NonSerialized] public Player player;
    public event Action OnIntroPose, OnOutroPose;
    public event Action<BeatResult> OnBeated;
    [NonSerialized] public readonly List<Beat> beats = new();
    [SerializeField] float goodTimeWidth = 0.1f, perfectTimeWidth = 0.05f;


    void Start() {
        Music.Instance.OnBeatSpawn += OnBeatSpawn;
        hp = maxHp;
    }

    void Update() {
        var removes = beats.RemoveAll(b => b.time < Music.Instance.Time - goodTimeWidth);
        if (removes >= 1) {
            OnBeated?.Invoke(new BeatResult(BeatResult.Status.MISS));
        }

        if (transform.position.y < -1e-2f) transform.position = transform.position.Y(-1e-2f);
        if (transform.position.y < Battle.Instance.despawnY) Damage(hp);
    }

    void OnBeatSpawn(Beat beat) {
        beats.Add(beat);
    }

    public BeatResult Beat() {
        var status = BeatResult.Status.MISS;
        if (beats.Count != 0) {
            var dt = Mathf.Abs(beats[0].time - Music.Instance.Time);
            status = dt <= perfectTimeWidth ? BeatResult.Status.PERFECT : dt <= goodTimeWidth ? BeatResult.Status.GOOD : BeatResult.Status.MISS;
            if (status != BeatResult.Status.MISS) {
                beats.RemoveAt(0);
            }
        }
        var res = new BeatResult(status);
        OnBeated?.Invoke(res);
        return res;
    }

    public void IntroPose() {
        OnIntroPose?.Invoke();
    }

    public void OutroPose() {
        OnOutroPose?.Invoke();
    }

    private void OnDestroy() {
        if (!Music.Instance) return;
        Music.Instance.OnBeatSpawn -= OnBeatSpawn;
    }

    public void Damage(float value) {
        hp = Mathf.Clamp(hp - value, 0, maxHp);
    }
}