using UnityEngine;
using System;
using Core.Battle;
using R3;

[AddComponentMenu(" 🟠HitBox", 0)]
[RequireComponent(typeof(Rigidbody))]
public class HitBox : MonoBehaviour {
    public Observable<Collider> OnEnterTrigger => onEnterTrigger;
    public Observable<Collider> OnExitTrigger => onExitTrigger;
    public Observable<Collider> OnStayTrigger => onStayTrigger;
    public Observable<Collision> OnEnterCollision => onEnterCollision;
    public Observable<Collision> OnExitCollision => onExitCollision;
    public Observable<Collision> OnStayCollision => onStayCollision;

    readonly Subject<Collider> onEnterTrigger = new();
    readonly Subject<Collider> onExitTrigger = new();
    readonly Subject<Collider> onStayTrigger = new();
    readonly Subject<Collision> onEnterCollision = new();
    readonly Subject<Collision> onExitCollision = new();
    readonly Subject<Collision> onStayCollision = new();

    public void Awake() {
        if (TryGetComponent<Collider>(out var collider)) {
        }
        else {
            Debug.LogError($"Hurtbox requires a Collider component. {this.gameObject.name} has no Collider.");
        }
    }

    void OnTriggerEnter(Collider other) => onEnterTrigger.OnNext(other);
    void OnTriggerExit(Collider other) => onExitTrigger.OnNext(other);
    void OnTriggerStay(Collider other) => onStayTrigger.OnNext(other);
    void OnCollisionEnter(Collision collision) => onEnterCollision.OnNext(collision);
    void OnCollisionExit(Collision collision) => onExitCollision.OnNext(collision);
    void OnCollisionStay(Collision collision) => onStayCollision.OnNext(collision);
}