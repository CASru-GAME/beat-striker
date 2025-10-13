using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using Unity.Mathematics;
using UnityEngine;

[RequireComponent(typeof(TextMeshProUGUI))]
public class BeatBar : MonoBehaviour {
    [SerializeField] int strikerId;
    Striker Striker => Battle.Instance.strikers.Get(strikerId);
    TextMeshProUGUI text;
    string result = "O";
    public float timeOffset;


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start() {
        text = GetComponent<TextMeshProUGUI>();

        Striker.OnBeated += res => {
            result = res.status == BeatResult.Status.PERFECT ? "P" : res.status == BeatResult.Status.GOOD ? "G" : "M";
        };
    }

    void Update() {
        text.text = result;
        if (Striker.beats.Count == 0) return;
        int dt = (int)Mathf.Floor(Mathf.Max(0, Striker.beats[0].time - Music.Instance.Time + timeOffset) * 20);
        text.text += new string(' ', dt);
        text.text += "o";
    }
}
