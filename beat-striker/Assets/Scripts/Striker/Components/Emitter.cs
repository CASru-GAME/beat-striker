using UnityEngine;
using System;
using Core.Striker;
using Core.Battle;
using R3;

public class Emitter : MonoBehaviour {
    [SerializeField] GameObject prefab;

    void Awake() {
    }

    public void Emit() {
            Instantiate(prefab, prefab.transform.position, prefab.transform.rotation).SetActive(true);
    }
}