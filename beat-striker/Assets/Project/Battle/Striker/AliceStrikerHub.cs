using System;
using System.Collections.Generic;
using Alice;
using R3;
using UnityEngine;

public record StrikerImpact(Vector3 DirectionAndMagnitude);
public record AttentionRequest(float DurationSeconds, string TechniqueText = "");

public interface IStrikerContext {
    Rigidbody Rigidbody { get; }
    Vector2 InputDirection { get; }
    Vector2 LocalInputDirection { get; }
    IEnumerable<IObservableStriker> GetAllStrikers();
    IObservableStriker GetSelf();
    IObservableStriker GetOpponent();
    void PlayAnimation(StrikerAnimationClip animation, Action<IStrikerStateContext> onComplete = null);
    void PlayAnimation(StrikerAnimationClip animation, Vector3 positionOffset, Vector3 rotationOffset, Action<IStrikerStateContext> onComplete = null);
    void ApplyDamage(float damage);
    void GenerateImpact(StrikerImpact command);
    void RequestAttention(AttentionRequest request);
}

namespace Alice {
    public enum StrikerStateCategory {
        Idle,
        Dash,
        Attack,
        Charge,
        Special,
        Guard,
        Unknown
    }

    public interface IObservableStriker {
        ReadOnlyReactiveProperty<Striker> Striker { get; }
        ReadOnlyReactiveProperty<int> PlayerId { get; }
        ReadOnlyReactiveProperty<Vector3> Position { get; }
        ReadOnlyReactiveProperty<Vector3> CenterPosition { get; }
        ReadOnlyReactiveProperty<Vector3> LookDirection { get; }
        ReadOnlyReactiveProperty<Vector3> Velocity { get; }
        ReadOnlyReactiveProperty<float> HitPoint { get; }
        ReadOnlyReactiveProperty<float> MaxHitPoint { get; }
        ReadOnlyReactiveProperty<float> SpecialPoint { get; }
        ReadOnlyReactiveProperty<float> MaxSpecialPoint { get; }
        Observable<Unit> OnDead { get; }
        ReadOnlyReactiveProperty<StrikerStateCategory> CurrentStateCategory { get; }
    }

    public interface IStrikerHub : IObservableStriker, System.IDisposable {
        void DestroyGameObject();
        void SetPlayerId(int playerId);
        void Tick(float deltaTime);
        void ChangeDirection(Vector2 direction);
        void CancelDirection();
        void Default();
        void Dash();
        void Attack();
        void Charge();
        void Special();
        void Guard();
        void AddSpecialPoint(float value);
        void Die();
        void GiveHit(HitStatus status);
        void ExitState();
        void IntroPose();
        void VictoryPose();
        Observable<StrikerImpact> OnInpactGenerated { get; }
        Observable<AttentionRequest> OnAtentionRequested { get; }
        Observable<Unit> OnSpecialRequestFailed { get; }
    }

    public class AliceStrikerHub : IStrikerContext, IStrikerHub, IDisposable {
        IStrikerRegistry strikerRegistry;

        float maxHitPoint;
        float maxSpecialPoint;
        float deathHeightY;
        StrikerState defaultState;
        StrikerState deadState;
        StrikerState victoryState;
        StrikerState introState;

        Rigidbody rb;
        GameObject strikerGameObject;
        AnimationPlayer animationPlayer;
        StrikerStateMachine stateMachine;
        readonly Subject<Unit> onDeadSubject = new();
        readonly ReactiveProperty<int> playerIdSubject = new(0);
        readonly ReactiveProperty<Alice.Striker> strikerSubject = new(Alice.Striker.Fighter);
        readonly ReactiveProperty<Vector3> positionSubject = new(Vector3.zero);
        readonly ReactiveProperty<Vector3> centerPositionSubject = new(Vector3.zero);
        readonly ReactiveProperty<Vector3> lookDirectionSubject = new(Vector3.forward);
        readonly ReactiveProperty<Vector3> velocitySubject = new(Vector3.zero);
        readonly ReactiveProperty<float> hitPointSubject = new(0f);
        readonly ReactiveProperty<float> maxHitPointSubject = new(0f);
        readonly ReactiveProperty<float> specialPointSubject = new(0f);
        readonly ReactiveProperty<float> maxSpecialPointSubject = new(0f);
        readonly ReactiveProperty<StrikerStateCategory> currentStateCategorySubject = new(StrikerStateCategory.Unknown);
        readonly Subject<StrikerImpact> onInpactGeneratedSubject = new();
        readonly Subject<AttentionRequest> onAttentionRequestedSubject = new();
        readonly Subject<Unit> onSpecialRequestFailedSubject = new();
        IDisposable stateNameSubscription;

        Vector2 inputDirection;
        float currentHitPoint;
        float currentSpecialPoint;
        int playerId;
        bool initialized;
        Transform strikerTransform;
        Transform centerPositionTransform;
        Vector3 previousFramePosition;
        Vector3 frameVelocity;
        bool hasEnemyInFrontState;
        bool isEnemyInFront;
        IStrikerState observedState;
        bool hasObservedState;
        bool isDead;

        public Vector2 InputDirection => inputDirection;
        public Rigidbody Rigidbody => rb;
        public ReadOnlyReactiveProperty<Alice.Striker> Striker => strikerSubject;
        public ReadOnlyReactiveProperty<int> PlayerId => playerIdSubject;
        public ReadOnlyReactiveProperty<Vector3> Position => positionSubject;
        public ReadOnlyReactiveProperty<Vector3> CenterPosition => centerPositionSubject;
        public ReadOnlyReactiveProperty<Vector3> LookDirection => lookDirectionSubject;
        public ReadOnlyReactiveProperty<Vector3> Velocity => velocitySubject;
        public ReadOnlyReactiveProperty<float> HitPoint => hitPointSubject;
        public ReadOnlyReactiveProperty<float> MaxHitPoint => maxHitPointSubject;
        public ReadOnlyReactiveProperty<float> SpecialPoint => specialPointSubject;
        public ReadOnlyReactiveProperty<float> MaxSpecialPoint => maxSpecialPointSubject;
        public ReadOnlyReactiveProperty<StrikerStateCategory> CurrentStateCategory => currentStateCategorySubject;
        public Observable<StrikerImpact> OnInpactGenerated => onInpactGeneratedSubject;
        public Observable<AttentionRequest> OnAtentionRequested => onAttentionRequestedSubject;
        public Observable<Unit> OnSpecialRequestFailed => onSpecialRequestFailedSubject;

        public Vector2 LocalInputDirection {
            get {
                var dir = inputDirection;
                if (Vector3.Dot(LookDirection.CurrentValue, Camera.main.transform.right) < 0) {
                    dir.x = -dir.x;
                }
                return dir;
            }
        }

        public IEnumerable<IObservableStriker> GetAllStrikers() {
            return strikerRegistry.GetAllStrikers();
        }
        public IObservableStriker GetSelf() {
            return this;
        }
        public IObservableStriker GetOpponent() {
            foreach (var striker in strikerRegistry.GetAllStrikers()) {
                if (striker.PlayerId.CurrentValue != playerId) {
                    return striker;
                }
            }
            return this;
        }

        public void DestroyGameObject() {
            UnityEngine.Object.Destroy(strikerGameObject);
        }

        public Observable<Unit> OnDead => onDeadSubject;

        public AliceStrikerHub() {
        }

        public void InitializeRuntimeDependencies(IStrikerRegistry strikerRegistry) {
            this.strikerRegistry = strikerRegistry;
        }

        public void Tick(float deltaTime) {
            if (!initialized) return;

            if (stateMachine == null) {
                stateMachine = new StrikerStateMachine(this, defaultState);
            }

            var currentPosition = rb.position;
            frameVelocity = deltaTime > 0f ? (currentPosition - previousFramePosition) / deltaTime : Vector3.zero;
            previousFramePosition = currentPosition;
            positionSubject.OnNext(currentPosition);
            centerPositionSubject.OnNext(centerPositionTransform.position);
            lookDirectionSubject.OnNext(strikerTransform.forward);
            velocitySubject.OnNext(frameVelocity);

            if (!isDead && currentPosition.y <= deathHeightY) {
                currentHitPoint = 0f;
                hitPointSubject.OnNext(currentHitPoint);
                Die();
            }

            UpdateEnemyInFrontState();
            NotifyEnemyBehindOnStateChanged();
            currentStateCategorySubject.OnNext(stateMachine.CurrentState.Category);

            stateMachine.CurrentState.OnUpdate(stateMachine);
            NotifyEnemyBehindOnStateChanged();
            currentStateCategorySubject.OnNext(stateMachine.CurrentState.Category);
        }

        public void InitializeFromLegacy(StrikerHub legacy) {
            maxHitPoint = legacy.InspectorMaxHitPoint;
            maxSpecialPoint = legacy.InspectorMaxSpecialPoint;
            deathHeightY = legacy.InspectorDeathHeightY;
            defaultState = legacy.InspectorDefaultState;
            deadState = legacy.InspectorDeadState;
            victoryState = legacy.InspectorVictoryState;
            introState = legacy.InspectorIntroState;
            rb = legacy.Rigidbody;
            strikerSubject.OnNext(legacy.InspectorStriker);
            strikerGameObject = legacy.gameObject;
            strikerTransform = legacy.transform;
            centerPositionTransform = legacy.GetCenterPositionTransform();
            rb.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionZ;
            animationPlayer = legacy.GetAnimationPlayer();
            currentHitPoint = maxHitPoint;
            currentSpecialPoint = 0f;
            maxHitPointSubject.OnNext(maxHitPoint);
            hitPointSubject.OnNext(currentHitPoint);
            maxSpecialPointSubject.OnNext(maxSpecialPoint);
            specialPointSubject.OnNext(currentSpecialPoint);
            previousFramePosition = rb.position;
            frameVelocity = Vector3.zero;
            positionSubject.OnNext(previousFramePosition);
            centerPositionSubject.OnNext(centerPositionTransform.position);
            lookDirectionSubject.OnNext(strikerTransform.forward);
            velocitySubject.OnNext(frameVelocity);
            hasEnemyInFrontState = false;
            isEnemyInFront = true;
            hasObservedState = false;
            isDead = false;
            currentStateCategorySubject.OnNext(StrikerStateCategory.Unknown);
            initialized = true;
        }

        public void SetPlayerId(int playerId) {
            this.playerId = playerId;
            playerIdSubject.OnNext(playerId);
        }

        public void GiveHit(HitStatus status) {
            if (stateMachine == null || currentHitPoint <= 0f) return;
            stateMachine.CurrentState.OnHit(stateMachine, status);
        }

        public void Dispose() {
            stateNameSubscription?.Dispose();
            onDeadSubject.Dispose();
            playerIdSubject.Dispose();
            strikerSubject.Dispose();
            positionSubject.Dispose();
            centerPositionSubject.Dispose();
            lookDirectionSubject.Dispose();
            velocitySubject.Dispose();
            hitPointSubject.Dispose();
            maxHitPointSubject.Dispose();
            specialPointSubject.Dispose();
            maxSpecialPointSubject.Dispose();
            currentStateCategorySubject.Dispose();
            onInpactGeneratedSubject.Dispose();
            onAttentionRequestedSubject.Dispose();
            onSpecialRequestFailedSubject.Dispose();
        }

        public void ApplyDamage(float damage) {
            if (isDead) {
                return;
            }

            currentHitPoint = Mathf.Max(0f, currentHitPoint - damage);
            hitPointSubject.OnNext(currentHitPoint);
            if (currentHitPoint <= 0f) {
                Die();
            }
        }

        public void ChangeDirection(Vector2 direction) {
            /*if (Vector3.Dot(LookDirection.CurrentValue, Camera.main.transform.right) < 0) {
                Debug.Log("Direction changed by camera");
                direction.x = -direction.x;
            }*/
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

            if (!CanUseSpecial()) {
                onSpecialRequestFailedSubject.OnNext(Unit.Default);
                return;
            }

            currentSpecialPoint = 0f;
            specialPointSubject.OnNext(currentSpecialPoint);
            stateMachine.CurrentState.OnSpecialRequested(stateMachine);
        }

        public void Guard() {
            if (stateMachine == null || currentHitPoint <= 0f) return;
            stateMachine.CurrentState.OnGuardRequested(stateMachine);
        }

        public void AddSpecialPoint(float value) {
            if (value <= 0f) return;

            currentSpecialPoint = Mathf.Clamp(currentSpecialPoint + value, 0f, maxSpecialPoint);
            specialPointSubject.OnNext(currentSpecialPoint);
        }

        public void Default() {
            if (!initialized || currentHitPoint <= 0f) return;
            if (stateMachine == null) {
                stateMachine = new StrikerStateMachine(this, defaultState);
                return;
            }
            stateMachine.ChangeState(defaultState);
        }

        public void Die() {
            if (stateMachine == null || isDead) return;

            isDead = true;
            currentHitPoint = 0f;
            hitPointSubject.OnNext(currentHitPoint);
            onDeadSubject.OnNext(Unit.Default);
            stateMachine.ChangeState(deadState);
        }

        public void ExitState() {
            if (stateMachine == null) return;
            stateMachine.ExitCurrentState();
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

        public void PlayAnimation(StrikerAnimationClip animation, Vector3 positionOffset, Vector3 rotationOffset, Action<IStrikerStateContext> onComplete = null) {
            animationPlayer.PlayAnimation(animation, positionOffset, rotationOffset, () => onComplete?.Invoke(stateMachine));
        }

        public void GenerateImpact(StrikerImpact command) {
            onInpactGeneratedSubject.OnNext(command);
        }

        public void RequestAttention(AttentionRequest request) {
            onAttentionRequestedSubject.OnNext(request);
        }

        bool CanUseSpecial() {
            return currentSpecialPoint + 0.0001f >= maxSpecialPoint;
        }

        void UpdateEnemyInFrontState() {
            var opponent = GetOpponent();
            if (opponent.PlayerId.CurrentValue == playerId) {
                hasEnemyInFrontState = false;
                return;
            }

            var toOpponent = opponent.Position.CurrentValue - rb.position;
            if (toOpponent.sqrMagnitude <= 0.0001f) {
                return;
            }

            var nextIsEnemyInFront = Vector3.Dot(strikerTransform.forward, toOpponent.normalized) >= 0f;

            if (!hasEnemyInFrontState) {
                isEnemyInFront = nextIsEnemyInFront;
                hasEnemyInFrontState = true;
                return;
            }

            if (isEnemyInFront && !nextIsEnemyInFront) {
                stateMachine.CurrentState.OnEnemyBehind(stateMachine);
            }

            isEnemyInFront = nextIsEnemyInFront;
        }

        void NotifyEnemyBehindOnStateChanged() {
            var currentState = stateMachine.CurrentState;
            if (hasObservedState && ReferenceEquals(observedState, currentState)) {
                return;
            }

            observedState = currentState;
            hasObservedState = true;

            if (hasEnemyInFrontState && !isEnemyInFront) {
                currentState.OnEnemyBehind(stateMachine);
            }
        }
    }
}