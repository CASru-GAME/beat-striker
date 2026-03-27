using System;
using System.Collections.Generic;
using Alice;
using UnityEngine;
using R3;



[RequireComponent(typeof(AnimationPlayer))]
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CapsuleCollider))]
[AddComponentMenu(" 🟠Striker Hub", 0)]
public class StrikerHub : MonoBehaviour {

    [Header("Striker Settings")]
    [SerializeField] private float maxHitPoint = 100f;
    [SerializeField] private float maxSpecialPoint = 100f;

    [Header("References")]
    [SerializeField] private StrikerState defaultState;
    [SerializeField] private StrikerState deadState, VictoryState, IntroState;
    [SerializeField] private Alice.AiBrain aiBrain;

    private Rigidbody rb;

    public Rigidbody Rigidbody => rb;
    public float MaxHitPoint => maxHitPoint;
    public float InspectorMaxHitPoint => maxHitPoint;
    public float InspectorMaxSpecialPoint => maxSpecialPoint;
    public StrikerState InspectorDefaultState => defaultState;
    public StrikerState InspectorDeadState => deadState;
    public StrikerState InspectorVictoryState => VictoryState;
    public StrikerState InspectorIntroState => IntroState;
    public Alice.AiBrain InspectorAiBrain => aiBrain;
    private Alice.AliceStrikerHub aliceRuntime;


    private AnimationPlayer animationPlayer;

    private void Awake() {
        rb = GetComponent<Rigidbody>();
        rb.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionZ;
        animationPlayer = GetComponent<AnimationPlayer>();
        EnsureAliceRuntimeHub();
    }

    public Alice.AliceStrikerHub EnsureAliceRuntimeHub() {
        if (aliceRuntime == null) {
            aliceRuntime = new Alice.AliceStrikerHub();
            aliceRuntime.InitializeFromLegacy(this);
        }
        return aliceRuntime;
    }

    public AnimationPlayer GetAnimationPlayer() {
        return animationPlayer;
    }

    private void Update() {
        aliceRuntime?.Tick(Time.deltaTime);
    }

    void OnDestroy() {
        aliceRuntime?.Dispose();
    }

}

/// <summary>
/// Striker専用ステートマシン
/// 遷移ロジックを内包した単一のステートマシン実装
/// </summary>
public class StrikerStateMachine : IStrikerStateContext, IStrikerNodeContext {
    IStrikerState currentState;
    readonly IStrikerContext context;
    readonly BehaviorSubject<string> currentStateNameSubject = new(string.Empty);
    public ReadOnlyReactiveProperty<string> CurrentStateName { get; }
    public IStrikerState CurrentState => currentState;

    public Rigidbody Rigidbody => context.Rigidbody;
    public Vector2 InputDirection => context.InputDirection;
    public IEnumerable<Alice.IReadOnlyBattleEntity> GetAllStrikers() => context.GetAllStrikers();

    public void ApplyDamage(float damage) {
        context.ApplyDamage(damage);
    }

    public void PlayAnimation(StrikerAnimationClip animation, Action<IStrikerStateContext> onComplete = null) {
        context.PlayAnimation(animation, onComplete);
    }

    public StrikerStateMachine(IStrikerContext context, IStrikerState defaultState = null) {
        this.context = context;
        CurrentStateName = currentStateNameSubject.ToReadOnlyReactiveProperty();
        if (defaultState != null) {
            ChangeState(defaultState);
        }
        PublishCurrentStateName();
    }


    public void ChangeState(IStrikerState newState) {
        if (newState == null || ReferenceEquals(newState, currentState)) return;

        var oldParents = currentState != null
            ? new HashSet<IStrikerGroup>(currentState.Parents ?? Array.Empty<IStrikerGroup>())
            : new HashSet<IStrikerGroup>();
        var newParents = new HashSet<IStrikerGroup>(newState.Parents ?? Array.Empty<IStrikerGroup>());

        currentState?.OnExit(context);

        foreach (var parent in oldParents) {
            if (!newParents.Contains(parent)) parent.OnExit(context);
        }

        foreach (var parent in newParents) {
            if (!oldParents.Contains(parent)) parent.OnEnter(context);
        }

        newState.OnEnter(context);
        currentState = newState;
        PublishCurrentStateName();
    }

    public void TryTransition(IStrikerNode node) {
        node?.OnTryTransition(this);
    }

    public void Reset(IStrikerState defaultState) {
        ChangeState(defaultState);
        PublishCurrentStateName();
    }

    void PublishCurrentStateName() {
        currentStateNameSubject.OnNext(ResolveCurrentStateName());
    }

    string ResolveCurrentStateName() {
        if (CurrentState is Component stateComponent) {
            return stateComponent.gameObject.name;
        }
        return CurrentState?.GetType().Name ?? string.Empty;
    }
}
