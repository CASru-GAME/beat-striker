using UnityEngine;
using System;
using Core.Striker;
using Core.Battle;
using R3;

[AddComponentMenu(" 🟠HurtBox", 0)]
public class Hurtbox : MonoBehaviour
{
    [SerializeField] bool isGuarding = true;
    [SerializeField] StrikerHub strikerHub;
    Alice.AliceStrikerHub runtimeStrikerHit;
    readonly Subject<HitStatus> onHit = new();
    public Observable<HitStatus> OnHit => onHit;

    public bool IsGuarding { get => isGuarding; set => isGuarding = value; }

    public void Awake() {
        if (TryGetComponent<Collider>(out var collider)) {
        }
        else {
            Debug.LogError($"Hurtbox requires a Collider component. {this.gameObject.name} has no Collider.");
        }
        if (strikerHub) {
            runtimeStrikerHit = strikerHub.EnsureAliceRuntimeHub();
            onHit.Subscribe(status => {
                runtimeStrikerHit.GiveHit(status);
            }).AddTo(this);
        }
    }

    public HitResult GiveHit(HitStatus hitStatus) {
        onHit.OnNext(hitStatus);
        
        if(isGuarding) {
            return new HitResult(HitResult.Status.Guarded);
        }
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