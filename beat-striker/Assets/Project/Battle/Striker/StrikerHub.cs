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
    [SerializeField] private StrikerState deadState, VictoryState, IntroState;
    [SerializeField] private Transform centerPositionTransform;

    private Rigidbody rb;
    readonly Dictionary<string, StrikerState> stateByPathId = new();
    readonly Dictionary<StrikerState, string> pathIdByState = new();

    public Rigidbody Rigidbody => rb;
    public Striker InspectorStriker => striker;
    public float MaxHitPoint => maxHitPoint;
    public float InspectorMaxHitPoint => maxHitPoint;
    public float InspectorMaxSpecialPoint => maxSpecialPoint;
    public float InspectorDeathHeightY => deathHeightY;
    public StrikerState InspectorDefaultState => defaultState;
    public StrikerState InspectorDeadState => deadState;
    public StrikerState InspectorVictoryState => VictoryState;
    public StrikerState InspectorIntroState => IntroState;
    public Transform InspectorCenterPositionTransform => centerPositionTransform;
    private Alice.IStrikerHub aliceRuntime;


    private AnimationPlayer animationPlayer;

    private void Awake() {
        rb = GetComponent<Rigidbody>();
        rb.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionZ;
        animationPlayer = GetComponent<AnimationPlayer>();
        BuildStatePathTable();
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

    private void FixedUpdate() {
        aliceRuntime?.TickPhysics(Time.fixedDeltaTime);
    }

    void OnDestroy() {
        aliceRuntime?.Dispose();
    }

    public bool TryGetStatePathId(IStrikerState state, out string pathId) {
        pathId = string.Empty;
        if (state is not StrikerState strikerState) {
            return false;
        }

        return pathIdByState.TryGetValue(strikerState, out pathId);
    }

    public bool TryGetStateByPathId(string pathId, out StrikerState state) {
        return stateByPathId.TryGetValue(pathId, out state);
    }

    void BuildStatePathTable() {
        stateByPathId.Clear();
        pathIdByState.Clear();
        var states = GetComponentsInChildren<StrikerState>(true);
        foreach (var state in states) {
            var pathId = BuildStatePathId(state.transform);
            stateByPathId[pathId] = state;
            pathIdByState[state] = pathId;
        }
    }

    string BuildStatePathId(Transform stateTransform) {
        var segments = new Stack<string>();
        var current = stateTransform;
        while (current != null && current != transform) {
            segments.Push(current.name);
            current = current.parent;
        }

        return string.Join("/", segments);
    }

}

/// <summary>
/// Striker専用ステートマシン
/// 遷移ロジックを内包した単一のステートマシン実装
/// </summary>
public class StrikerStateMachine : IStrikerStateContext, IStrikerNodeContext {
    const float WORLD_Z_EPSILON = 0.0001f;
    const float FORWARD_XZ_EPSILON = 0.0001f;
    IStrikerState currentState;
    bool isChangingState;
    bool forceSameStateTransitionInProgress;
    bool isGroupProcessingPrevented;
    readonly IStrikerContext context;
    readonly float deployWorldZ;
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

    public void PlayAnimation(StrikerAnimationClip animation, Vector3 positionOffset, Vector3 rotationOffset, Action<IStrikerStateContext> onComplete = null) {
        context.PlayAnimation(animation, positionOffset, rotationOffset, onComplete);
    }

    public void GenerateImpact(StrikerImpact command) {
        context.GenerateImpact(command);
    }

    public void RequestAttention(AttentionRequest request) {
        context.RequestAttention(request);
    }

    public void PreventGroup() {
        isGroupProcessingPrevented = true;
    }

    public void ClearPreventGroup() {
        isGroupProcessingPrevented = false;
    }

    public bool IsGroupProcessingPrevented => isGroupProcessingPrevented;

    public StrikerStateMachine(IStrikerContext context, IStrikerState defaultState = null) {
        this.context = context;
        deployWorldZ = context.Rigidbody.position.z;
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
            RestoreTransformIfNeededAfterStateExit();

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

    public void Restart(IStrikerState defaultState) {
        ExitCurrentState();
        ChangeState(defaultState);
    }

    public void ExitCurrentState() {
        if (currentState == null) return;
        
        isChangingState = true;
        try {
            currentState.OnExit(context);
            var parents = new HashSet<IStrikerGroup>(currentState.Parents ?? Array.Empty<IStrikerGroup>());
            foreach (var parent in parents) {
                parent.OnExit(context);
            }
            RestoreTransformIfNeededAfterStateExit();
            currentState = null;
        }
        finally {
            isChangingState = false;
        }
    }

    static string FormatStateName(IStrikerState state) {
        if (state is Component stateComponent) {
            return stateComponent.gameObject.name;
        }
        return state?.GetType().Name ?? "<null>";
    }

    void RestoreTransformIfNeededAfterStateExit() {
        var body = context.Rigidbody;
        var position = body.position;
        var forward = body.transform.forward;
        var isWorldZChanged = Mathf.Abs(position.z - deployWorldZ) > WORLD_Z_EPSILON;
        var flatForward = Vector3.ProjectOnPlane(forward, Vector3.up);
        var hasValidFlatForward = flatForward.sqrMagnitude > FORWARD_XZ_EPSILON;
        var shouldAlignForwardToWorldX = hasValidFlatForward && Mathf.Abs(flatForward.normalized.z) > FORWARD_XZ_EPSILON;

        if (!isWorldZChanged && !shouldAlignForwardToWorldX) {
            return;
        }

        if (isWorldZChanged) {
            position.z = deployWorldZ;
        }

        body.position = position;
        if (!shouldAlignForwardToWorldX) {
            return;
        }

        flatForward.Normalize();
        var targetForward = flatForward.x >= 0f ? Vector3.right : Vector3.left;
        body.rotation = Quaternion.LookRotation(targetForward, Vector3.up);
    }
}
