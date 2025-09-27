using System;
using System.Linq;
using TMPro;
using Unity.Mathematics;
using UnityEngine;

[RequireComponent(typeof(TextMeshProUGUI))]
public class BeatBar : MonoBehaviour {
    [SerializeField] int playerNumber;
    TextMeshProUGUI text;
    string result = "O";
    public float timeOffset;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start() {
        text = GetComponent<TextMeshProUGUI>();
        Battle.Instance.OnBattleStart += () => {
            Battle.Instance.strikers[playerNumber].OnBeated += res => {
                result = res.status == BeatResult.Status.PERFECT ? "P" : res.status == BeatResult.Status.GOOD ?  "G" : "M";
            };
        };
    }

    // Update is called once per frame
    void Update() {
        var striker = Battle.Instance.strikers[playerNumber];
        text.text = result;
        if (striker.beats.Count == 0) return;
        int dt = (int)Mathf.Floor(Mathf.Max(0, striker.beats[0].time - Battle.Instance.musicTime + timeOffset) * 20);
        text.text += new string(' ', dt);
        text.text += "o";
    }
}
