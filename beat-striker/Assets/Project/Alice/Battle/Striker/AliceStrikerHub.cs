using System;
using System.Collections.Generic;
using Core.App.Types;
using Alice;
using R3;
using UnityEngine;
using VContainer;

namespace Alice {
    public interface IReadOnlyBattleEntity {
        int PlayerId { get; }
        Vector3 Position { get; }
        Vector3 Velocity { get; }
        float HitPoint { get; }
        float MaxHitPoint { get; }
        Observable<Unit> OnHit { get; }
        ReadOnlyReactiveProperty<string> CurrentStateName { get; }
    }

    public interface IStrikerHub : IReadOnlyBattleEntity, System.IDisposable {
        float CurrentHitPoint { get; }
        ReadOnlyReactiveProperty<float> CurrentHitPointReactive { get; }
        ReadOnlyReactiveProperty<float> HitPointRatio { get; }
        AiBrain AiBrain { get; }
        Rigidbody Rigidbody { get; }
        Observable<PlayerId> OnDeadEvent { get; }

        void SetPlayerId(int playerId);
        void Tick(float deltaTime);
        void ChangeDirection(Vector2 direction);
        void CancelDirection();
        void Dash();
        void Attack();
        void Charge();
        void Special();
        void Guard();
        void Die();
        void GiveHit(HitStatus status);
    }

    public class AliceStrikerHub : IStrikerContext, IStrikerHub, IDisposable {
        [Inject] IStrikerRegistry strikerRegistry;

        float maxHitPoint;
        float maxSpecialPoint;
        StrikerState defaultState;
        StrikerState deadState;
        StrikerState victoryState;
        StrikerState introState;

        Rigidbody rb;
        AnimationPlayer animationPlayer;
        StrikerStateMachine stateMachine;
        AiBrain aiBrain;
        readonly Subject<Unit> onHit = new();
        readonly Subject<PlayerId> onDeadSubject = new();
        readonly BehaviorSubject<string> currentStateNameSubject = new(string.Empty);
        readonly ReadOnlyReactiveProperty<string> currentStateName;
        readonly BehaviorSubject<float> currentHitPointSubject = new(0f);
        readonly ReadOnlyReactiveProperty<float> currentHitPointReactive;
        readonly BehaviorSubject<float> hitPointRatioSubject = new(1f);
        readonly ReadOnlyReactiveProperty<float> hitPointRatio;
        IDisposable stateNameSubscription;

        Vector2 inputDirection;
        float currentHitPoint;
        int playerId;
        bool initialized;
        Vector3 previousFramePosition;
        Vector3 frameVelocity;

        public Vector2 InputDirection => inputDirection;
        public Rigidbody Rigidbody => rb;
        public float CurrentHitPoint => currentHitPoint;
        public ReadOnlyReactiveProperty<float> CurrentHitPointReactive => currentHitPointReactive;
        public ReadOnlyReactiveProperty<float> HitPointRatio => hitPointRatio;
        public AiBrain AiBrain => aiBrain;
        public float MaxHitPoint => maxHitPoint;
        public Vector3 Position => Rigidbody.position;
        public Vector3 Velocity => frameVelocity;
        public float HitPoint => currentHitPoint;
        public ReadOnlyReactiveProperty<string> CurrentStateName => currentStateName;
        public IEnumerable<IReadOnlyBattleEntity> GetAllStrikers() {
            return strikerRegistry.GetAllStrikers();
        }
        public IReadOnlyBattleEntity GetSelf() {
            return this;
        }
        public IReadOnlyBattleEntity GetOpponent() {
            foreach (var striker in strikerRegistry.GetAllStrikers()) {
                if (striker.PlayerId != playerId) {
                    return striker;
                }
            }
            return this;
        }
        public int PlayerId => playerId;

        public Observable<Unit> OnHit => onHit;
        public Observable<PlayerId> OnDeadEvent => onDeadSubject;

        public AliceStrikerHub() {
            currentStateName = currentStateNameSubject.ToReadOnlyReactiveProperty();
            currentHitPointReactive = currentHitPointSubject.ToReadOnlyReactiveProperty();
            hitPointRatio = hitPointRatioSubject.ToReadOnlyReactiveProperty();
        }

        public void Tick(float deltaTime) {
            if (!initialized) return;

            if (stateMachine == null) {
                stateMachine = new StrikerStateMachine(this, defaultState);
                stateNameSubscription = stateMachine.CurrentStateName.Subscribe(currentStateNameSubject.OnNext);
            }

            var currentPosition = rb.position;
            frameVelocity = deltaTime > 0f ? (currentPosition - previousFramePosition) / deltaTime : Vector3.zero;
            previousFramePosition = currentPosition;

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
            rb = legacy.Rigidbody;
            rb.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionZ;
            animationPlayer = legacy.GetAnimationPlayer();
            currentHitPoint = maxHitPoint;
            currentHitPointSubject.OnNext(currentHitPoint);
            hitPointRatioSubject.OnNext(1f);
            previousFramePosition = rb.position;
            frameVelocity = Vector3.zero;
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

        public void Dispose() {
            stateNameSubscription?.Dispose();
            onHit.Dispose();
            onDeadSubject.Dispose();
            currentStateName.Dispose();
            currentStateNameSubject.Dispose();
            currentHitPointReactive.Dispose();
            currentHitPointSubject.Dispose();
            hitPointRatio.Dispose();
            hitPointRatioSubject.Dispose();
        }

        public void ApplyDamage(float damage) {
            currentHitPoint = Mathf.Max(0f, currentHitPoint - damage);
            currentHitPointSubject.OnNext(currentHitPoint);
            var max = Mathf.Max(1f, maxHitPoint);
            hitPointRatioSubject.OnNext(Mathf.Clamp01(currentHitPoint / max));
            if (currentHitPoint <= 0f) {
                Die();
            }
        }

        public void ChangeDirection(Vector2 direction) {
            inputDirection = direction.sqrMagnitude > 0f ? direction.normalized : Vector2.zero;
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
            stateMachine.CurrentState.OnAttackRequested(stateMachine);
        }

        public void Guard() {
            if (stateMachine == null || currentHitPoint <= 0f) return;
            stateMachine.CurrentState.OnGuardRequested(stateMachine);
        }

        public void Die() {
            if (stateMachine == null) return;
            onDeadSubject.OnNext(new PlayerId(playerId));
            stateMachine.ChangeState(deadState);
        }

        public void IntroPose() {
            if (stateMachine == null) return;
            stateMachine.ChangeState(introState);
        }

        public void VictoryPose() {
            if (stateMachine == null) return;
            stateMachine.ChangeState(victoryState);
        }

        public void PlayAnimation(StrikerAnimationClip animation, Action<IStrikerStateContext> onComplete = null) {
            animationPlayer.PlayAnimation(animation, () => onComplete?.Invoke(stateMachine));
        }
    }
}