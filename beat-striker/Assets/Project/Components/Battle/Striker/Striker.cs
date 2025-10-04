using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[AddComponentMenu(" Striker", 0)]
public class Striker : MonoBehaviour {
    public float Rank { get; internal set; }
    public float maxHp = 100;
    private float hp;
    public float Hp { 
        get => hp; 
        set => hp = Mathf.Clamp(value, 0, maxHp); 
    }
    public bool isGround { get; private set; }
    [NonSerialized] public Player player;
    public event Action OnLanded, OnTakeoff, OnIntroPose, OnOutroPose;
    public event Action<BeatResult> OnBeated;
    [NonSerialized] public readonly List<Beat> beats = new();
    [SerializeField] float goodTimeWidth = 0.1f, perfectTimeWidth = 0.05f;


    void Start() {
        isGround = false;
        Music.Instance.OnBeatSpawn += OnBeatSpawn;
        Hp = maxHp;
        Rank = 0;
    }

    void Update() {
        var removes = beats.RemoveAll(b => b.time < Music.Instance.Time - goodTimeWidth);
        if (removes >= 1) {
            OnBeated?.Invoke(new BeatResult(BeatResult.Status.MISS));
        }

        if (transform.position.y < -1e-2f) transform.position = transform.position.Y(-1e-2f);
        if (transform.position.y < Battle.Instance.despawnY) hp = 0;
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
        if (!Music.Instance) return;
        Music.Instance.OnBeatSpawn -= OnBeatSpawn;
    }
}
