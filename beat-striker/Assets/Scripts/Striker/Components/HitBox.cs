using UnityEngine;
using System;
using Core.Battle;

[AddComponentMenu(" 🟠HitBox", 0)]
[RequireComponent(typeof(Rigidbody))]
public class HitBox : MonoBehaviour {
    public event Action<Collider> OnEnterTrigger;
    public event Action<Collider> OnExitTrigger;
    public event Action<Collider> OnStayTrigger;
    public event Action<Collision> OnEnterCollision;
    public event Action<Collision> OnExitCollision;
    public event Action<Collision> OnStayCollision;

    public void Awake() {
        if (TryGetComponent<Collider>(out var collider)) {
            if (collider.isTrigger) {
                // Triggerモードの場合、Collisionイベントは発火しない
                if (OnEnterCollision != null || OnExitCollision != null || OnStayCollision != null) {
                    Debug.LogError($"HitBox on {gameObject.name}: Collider is set to Trigger mode, but Collision events are subscribed. These events will never fire.");
                }
            } else {
                // Colliderモードの場合、Triggerイベントは発火しない
                if (OnEnterTrigger != null || OnExitTrigger != null || OnStayTrigger != null) {
                    Debug.LogError($"HitBox on {gameObject.name}: Collider is NOT set to Trigger mode, but Trigger events are subscribed. These events will never fire.");
                }
            }
        }
        else {
            Debug.LogError($"Hurtbox requires a Collider component. {this.gameObject.name} has no Collider.");
        }
    }

    void OnTriggerEnter(Collider other) => OnEnterTrigger?.Invoke(other);
    void OnTriggerExit(Collider other) => OnExitTrigger?.Invoke(other);
    void OnTriggerStay(Collider other) => OnStayTrigger?.Invoke(other);
    void OnCollisionEnter(Collision collision) => OnEnterCollision?.Invoke(collision);
    void OnCollisionExit(Collision collision) => OnExitCollision?.Invoke(collision);
    void OnCollisionStay(Collision collision) => OnStayCollision?.Invoke(collision);
}