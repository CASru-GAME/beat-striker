using System;
using System.Collections.Generic;
using Core.App.Types;
using Core.Battle;
using Core.Striker;
using Core.Striker.Components;
using R3;
using UnityEngine;
using VContainer;

namespace Alice {
    public record BattleCommandLog(float Time, GamePadButton Button);

    public interface IReadOnlyBattleEntity {
        int PlayerId { get; }
        Vector3 Position { get; }
        Vector3 Velocity { get; }
        float HitPoint { get; }
        float MaxHitPoint { get; }
        Observable<Unit> OnHit { get; }
        ReadOnlyReactiveProperty<string> CurrentStateName { get; }
        IReadOnlyList<BattleCommandLog> CommandHistory { get; }
    }

    [RequireComponent(typeof(AnimationPlayer))]
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(CapsuleCollider))]
    public class AliceStrikerHub : MonoBehaviour, IStrikerContext, IReadOnlyBattleEntity {
        [Inject] IBattleFlow battleFlow;
        [Inject] IStrikerRegistry strikerRegistry;

        HitPoint maxHitPoint;
        SpecialPoint maxSpecialPoint;
        StrikerState defaultState;
        StrikerState deadState;
        StrikerState victoryState;
        StrikerState introState;

        Rigidbody rb;
        AnimationPlayer animationPlayer;
        StrikerStateMachine stateMachine;
        AiBrain aiBrain;
        readonly Subject<Unit> onHit = new();
        readonly BehaviorSubject<string> currentStateNameSubject = new(string.Empty);
        readonly ReadOnlyReactiveProperty<string> currentStateName;
        IDisposable stateNameSubscription;

        Vector2 inputDirection;
        float currentHitPoint;
        int playerId;
        bool initialized;
        Vector3 previousFramePosition;
        Vector3 frameVelocity;
        readonly List<BattleCommandLog> commandHistory = new();
        const int MAX_COMMAND_HISTORY_COUNT = 32;

        public Vector2 InputDirection => inputDirection;
        public Rigidbody Rigidbody => rb;
        public float CurrentHitPoint => currentHitPoint;
        public AiBrain AiBrain => aiBrain;
        public float MaxHitPoint => maxHitPoint.value;
        public Vector3 Position => Rigidbody.position;
        public Vector3 Velocity => frameVelocity;
        public float HitPoint => currentHitPoint;
        public ReadOnlyReactiveProperty<string> CurrentStateName => currentStateName;
        public IEnumerable<IReadOnlyBattleEntity> GetAllStrikers() {
            return strikerRegistry.GetAllStrikers();
        }
        public int PlayerId => playerId;
        public IReadOnlyList<BattleCommandLog> CommandHistory => commandHistory;

        public Observable<Unit> OnHit => onHit;

        public AliceStrikerHub() {
            currentStateName = currentStateNameSubject.ToReadOnlyReactiveProperty();
        }

        void Awake() {
            rb = GetComponent<Rigidbody>();
            rb.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionZ;
            animationPlayer = GetComponent<AnimationPlayer>();
            previousFramePosition = rb.position;
            frameVelocity = Vector3.zero;
        }

        void Start() {
            if (!initialized) return;
            stateMachine = new StrikerStateMachine(this, defaultState);
            stateNameSubscription = stateMachine.CurrentStateName.Subscribe(currentStateNameSubject.OnNext);
        }

        void Update() {
            var deltaTime = Time.deltaTime;
            var currentPosition = rb.position;
            frameVelocity = deltaTime > 0f ? (currentPosition - previousFramePosition) / deltaTime : Vector3.zero;
            previousFramePosition = currentPosition;

            if (stateMachine == null) return;
            stateMachine.CurrentState.OnUpdate(stateMachine);
        }

        public void InitializeFromLegacy(StrikerHub legacy) {
            maxHitPoint = legacy.InspectorMaxHitPoint;
            maxSpecialPoint = legacy.InspectorMaxSpecialPoint;
            defaultState = legacy.InspectorDefaultState;
            deadState = legacy.InspectorDeadState;
            victoryState = legacy.InspectorVictoryState;
            introState = legacy.InspectorIntroState;
            aiBrain = legacy.InspectorAiBrain;
            currentHitPoint = maxHitPoint.value;
            initialized = true;
        }

        public void SetPlayerId(int playerId) {
            this.playerId = playerId;
        }

        public void GiveHit(HitStatus status) {
            if (stateMachine == null || currentHitPoint <= 0f) return;
            stateMachine.CurrentState.OnHit(stateMachine, status);
            onHit.OnNext(Unit.Default);
        }

        void OnDestroy() {
            stateNameSubscription?.Dispose();
            onHit.Dispose();
            currentStateName.Dispose();
            currentStateNameSubject.Dispose();
        }

        public void ApplyDamage(float damage) {
            currentHitPoint = Mathf.Max(0f, currentHitPoint - damage);
            if (currentHitPoint <= 0f) {
                OnDead();
            }
        }

        public void RecordExecutedCommand(BattleCommandLog commandLog) {
            commandHistory.Add(commandLog);
            if (commandHistory.Count > MAX_COMMAND_HISTORY_COUNT) {
                commandHistory.RemoveAt(0);
            }
        }

        public void ChangeDirection(Vector2 direction) {
            inputDirection = direction;
        }

        public void CancelDirection() {
            inputDirection = Vector2.zero;
        }

        public void Dash() {
            if (stateMachine == null || currentHitPoint <= 0f) return;
            stateMachine.CurrentState.OnDashRequested(stateMachine);
        }

        public void Attack() {
            if (stateMachine == null || currentHitPoint <= 0f) return;
            stateMachine.CurrentState.OnAttackRequested(stateMachine);
        }

        public void Charge() {
            if (stateMachine == null || currentHitPoint <= 0f) return;
            stateMachine.CurrentState.OnChargeRequested(stateMachine);
        }

        public void Special() {
            if (stateMachine == null || currentHitPoint <= 0f) return;
            // Alice runtime keeps special executable path without old model dependency.
            stateMachine.CurrentState.OnAttackRequested(stateMachine);
        }

        public void Guard() {
            if (stateMachine == null || currentHitPoint <= 0f) return;
            stateMachine.CurrentState.OnGuardRequested(stateMachine);
        }

        public void OnDead() {
            if (stateMachine == null) return;
            battleFlow.NotifyPlayerDead(new PlayerId(playerId));
            stateMachine.ChangeState(deadState);
        }

        public void OnIntro() {
            if (stateMachine == null) return;
            stateMachine.ChangeState(introState);
        }

        public void OnVictory() {
            if (stateMachine == null) return;
            stateMachine.ChangeState(victoryState);
        }

        public void OnReset() {
            currentHitPoint = maxHitPoint.value;
            inputDirection = Vector2.zero;
            commandHistory.Clear();
            previousFramePosition = rb.position;
            frameVelocity = Vector3.zero;
            if (stateMachine != null) {
                stateMachine.Reset(defaultState);
            }
        }

        public void PlayAnimation(StrikerAnimationClip animation, Action<IStrikerStateContext> onComplete = null) {
            animationPlayer.PlayAnimation(animation, () => onComplete?.Invoke(stateMachine));
        }
    }
}