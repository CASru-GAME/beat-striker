using UnityEngine;
using System;
using Core.Striker;
using Core.Battle;

[AddComponentMenu(" 🟠HurtBox", 0)]
public class Hurtbox : MonoBehaviour
{
    [SerializeField] bool isGuarding = true;
    [SerializeField] StrikerHub strikerHub;

    public bool IsGuarding { get => isGuarding; set => isGuarding = value; }

    public void Awake() {
        if (TryGetComponent<Collider>(out var collider)) {
        }
        else {
            Debug.LogError($"Hurtbox requires a Collider component. {this.gameObject.name} has no Collider.");
        }
    }

    public HitResult GiveHit(HitStatus hitStatus) {
        if(isGuarding) {
            strikerHub.GiveHit(hitStatus);
            return new HitResult(HitResult.Status.Guarded);
        }
        strikerHub.GiveHit(hitStatus);
        return new HitResult(HitResult.Status.Success);
    }
}

public struct HitResult {
    public readonly Status status;

    public HitResult(Status status) {
        this.status = status;
    }

    public enum Status {
        Success,
        Guarded
    }
}