using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[AddComponentMenu(" Striker", 0)]
public class Striker : MonoBehaviour {
    public float hp = 100;
    public bool isGround { get; private set; }
    [NonSerialized] public Player player;
    public event Action OnLanded, OnTakeoff, OnIntroPose, OnVictoryPose;
    public event Action<BeatResult> OnBeated;
    [NonSerialized] public readonly List<Beat> beats = new();
    [SerializeField] float goodTimeWidth = 0.1f, perfectTimeWidth = 0.05f;


    void Start() {
        isGround = false;
        Battle.Instance.Music.OnBeatSpawn += OnBeatSpawn;
    }

    void Update() {
        var removes = beats.RemoveAll(b => b.time < Battle.Instance.Music.Time - goodTimeWidth);
        if (removes >= 1) {
            OnBeated?.Invoke(new BeatResult(BeatResult.Status.MISS));
        }
    }

    void OnBeatSpawn(Beat beat) {
        beats.Add(beat);
    }

    public BeatResult Beat() {
        var status = BeatResult.Status.MISS;
        if (beats.Count != 0) {
            var dt = Mathf.Abs(beats[0].time - Battle.Instance.Music.Time);
            status = dt <= perfectTimeWidth ? BeatResult.Status.PERFECT : dt <= goodTimeWidth ? BeatResult.Status.GOOD : BeatResult.Status.MISS;
            if (status != BeatResult.Status.MISS) {
                beats.RemoveAt(0);
            }
        }
        var res = new BeatResult(status);
        OnBeated?.Invoke(res);
        return res;
    }

    private void OnCollisionStay(Collision collision) {
        foreach (var contact in collision.contacts) {
            if (contact.normal.y > 0.5f) {
                if (!isGround) OnLanded?.Invoke();
                isGround = true;
                return;
            }
        }
    }

    private void OnCollisionExit(Collision collision) {
        if (isGround) OnTakeoff?.Invoke();
        isGround = false;
    }

    private void OnDestroy() {
        if (!Battle.Instance || !Battle.Instance.Music) return;
        Battle.Instance.Music.OnBeatSpawn -= OnBeatSpawn;
    }
}
