using UnityEngine;
using System;
using R3;

public class Emitter : MonoBehaviour {
    [SerializeField] GameObject prefab;

    void Awake() {
    }

    public void Emit() {
            Instantiate(prefab, prefab.transform.position, prefab.transform.rotation).SetActive(true);
    }
}