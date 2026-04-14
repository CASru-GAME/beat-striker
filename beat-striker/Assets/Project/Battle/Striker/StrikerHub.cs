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
    [SerializeField] private Striker striker = Striker.Fighter;
    [SerializeField] private float maxHitPoint = 100f;
    [SerializeField] private float maxSpecialPoint = 100f;
    [SerializeField] private float deathHeightY = -10f;

    [Header("References")]
    [SerializeField] private StrikerState defaultState;
    [SerializeField] private StrikerState stunState;
    [SerializeField] private StrikerState deadState, VictoryState, IntroState;
    [SerializeField] private Alice.AiBrain aiBrain;
    [SerializeField] private Transform centerPositionTransform;

    private Rigidbody rb;

    public Rigidbody Rigidbody => rb;
    public Striker InspectorStriker => striker;
    public float MaxHitPoint => maxHitPoint;
    public float InspectorMaxHitPoint => maxHitPoint;
    public float InspectorMaxSpecialPoint => maxSpecialPoint;
    public float InspectorDeathHeightY => deathHeightY;
    public StrikerState InspectorDefaultState => defaultState;
    public StrikerState InspectorStunState => stunState;
    public StrikerState InspectorDeadState => deadState;
    public StrikerState InspectorVictoryState => VictoryState;
    public StrikerState InspectorIntroState => IntroState;
    public Alice.AiBrain InspectorAiBrain => aiBrain;
    public Transform InspectorCenterPositionTransform => centerPositionTransform;
    private Alice.IStrikerHub aliceRuntime;


    private AnimationPlayer animationPlayer;

    private void Awake() {
        rb = GetComponent<Rigidbody>();
        rb.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionZ;
        animationPlayer = GetComponent<AnimationPlayer>();
        EnsureAliceRuntimeHub();
    }

    public Alice.IStrikerHub EnsureAliceRuntimeHub() {
        if (aliceRuntime == null) {
            var runtime = new Alice.AliceStrikerHub();
            runtime.InitializeFromLegacy(this);
            aliceRuntime = runtime;
        }
        return aliceRuntime;
    }

    public AnimationPlayer GetAnimationPlayer() {
        return animationPlayer;
    }

    public Transform GetCenterPositionTransform() {
        if (centerPositionTransform != null) {
            return centerPositionTransform;
        }

        var fallbackCenter = new GameObject("CenterPosition");
        fallbackCenter.transform.SetParent(transform, false);
        fallbackCenter.transform.localPosition = new Vector3(0f, 1f, 0f);
        centerPositionTransform = fallbackCenter.transform;
        return centerPositionTransform;
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
    bool isChangingState;
    bool forceSameStateTransitionInProgress;
    readonly IStrikerContext context;
    public IStrikerState CurrentState => currentState;

    public Rigidbody Rigidbody => context.Rigidbody;
    public Vector2 InputDirection => context.InputDirection;
    public Vector2 LocalInputDirection => context.LocalInputDirection;
    public IEnumerable<Alice.IObservableStriker> GetAllStrikers() => context.GetAllStrikers();
    public Alice.IObservableStriker GetSelf() => context.GetSelf();
    public Alice.IObservableStriker GetOpponent() => context.GetOpponent();

    public void ApplyDamage(float damage) {
        context.ApplyDamage(damage);
    }

    public void PlayAnimation(StrikerAnimationClip animation, Action<IStrikerStateContext> onComplete = null) {
        context.PlayAnimation(animation, onComplete);
    }

    public void GenerateImpact(StrikerImpact command) {
        context.GenerateImpact(command);
    }

    public void RequestAttention(AttentionRequest request) {
        context.RequestAttention(request);
    }

    public StrikerStateMachine(IStrikerContext context, IStrikerState defaultState = null) {
        this.context = context;
        if (defaultState != null) {
            ChangeState(defaultState);
        }
    }


    public void ChangeState(IStrikerState newState, bool forceSameStateTransition = false) {
        if (isChangingState) {
            Debug.LogError($"StrikerStateMachine.ChangeState was called during an active transition. current={FormatStateName(currentState)}, requested={FormatStateName(newState)}");
            return;
        }

        if (newState == null) return;

        if (!forceSameStateTransition && !forceSameStateTransitionInProgress && ReferenceEquals(newState, currentState)) {
            return;
        }

        isChangingState = true;
        try {
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
        }
        finally {
            isChangingState = false;
        }
    }

    public void TryTransition(IStrikerNode node, bool forceSameStateTransition = false) {
        if (!forceSameStateTransition) {
            node?.OnTryTransition(this);
            return;
        }

        forceSameStateTransitionInProgress = true;
        try {
            node?.OnTryTransition(this);
        }
        finally {
            forceSameStateTransitionInProgress = false;
        }
    }

    public void Reset(IStrikerState defaultState) {
        ChangeState(defaultState);
    }

    static string FormatStateName(IStrikerState state) {
        if (state is Component stateComponent) {
            return stateComponent.gameObject.name;
        }
        return state?.GetType().Name ?? "<null>";
    }
}
