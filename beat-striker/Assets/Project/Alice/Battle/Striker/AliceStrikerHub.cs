using System;
using System.Collections.Generic;
using Core.App.Types;
using Alice;
using R3;
using UnityEngine;
using VContainer;

public record StrikerInpact(Vector3 DirectionAndMagnitude);
public record AttentionRequest(float DurationSeconds);

public interface IStrikerContext {
    Rigidbody Rigidbody { get; }
    Vector2 InputDirection { get; }
    Vector2 LocalInputDirection { get; }
    IEnumerable<IObservableStriker> GetAllStrikers();
    IObservableStriker GetSelf();
    IObservableStriker GetOpponent();
    void PlayAnimation(StrikerAnimationClip animation, Action<IStrikerStateContext> onComplete = null);
    void ApplyDamage(float damage);
    void GenerateInpact(StrikerInpact command);
    void RequestAttention(AttentionRequest request);
}

namespace Alice {
    public interface IObservableStriker {
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
    }

    public interface IStrikerHub : IObservableStriker, System.IDisposable {
        AiBrain AiBrain { get; }
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
        void IntroPose();
        void VictoryPose();
        Observable<StrikerInpact> OnInpactGenerated { get; }
        Observable<AttentionRequest> OnAtentionRequested { get; }
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
        readonly Subject<Unit> onDeadSubject = new();
        readonly ReactiveProperty<int> playerIdSubject = new(0);
        readonly ReactiveProperty<Vector3> positionSubject = new(Vector3.zero);
        readonly ReactiveProperty<Vector3> centerPositionSubject = new(Vector3.zero);
        readonly ReactiveProperty<Vector3> lookDirectionSubject = new(Vector3.forward);
        readonly ReactiveProperty<Vector3> velocitySubject = new(Vector3.zero);
        readonly ReactiveProperty<float> hitPointSubject = new(0f);
        readonly ReactiveProperty<float> maxHitPointSubject = new(0f);
        readonly ReactiveProperty<float> specialPointSubject = new(0f);
        readonly ReactiveProperty<float> maxSpecialPointSubject = new(0f);
        readonly Subject<StrikerInpact> onInpactGeneratedSubject = new();
        readonly Subject<AttentionRequest> onAttentionRequestedSubject = new();
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

        public Vector2 InputDirection => inputDirection;
        public Rigidbody Rigidbody => rb;
        public AiBrain AiBrain => aiBrain;
        public ReadOnlyReactiveProperty<int> PlayerId => playerIdSubject;
        public ReadOnlyReactiveProperty<Vector3> Position => positionSubject;
        public ReadOnlyReactiveProperty<Vector3> CenterPosition => centerPositionSubject;
        public ReadOnlyReactiveProperty<Vector3> LookDirection => lookDirectionSubject;
        public ReadOnlyReactiveProperty<Vector3> Velocity => velocitySubject;
        public ReadOnlyReactiveProperty<float> HitPoint => hitPointSubject;
        public ReadOnlyReactiveProperty<float> MaxHitPoint => maxHitPointSubject;
        public ReadOnlyReactiveProperty<float> SpecialPoint => specialPointSubject;
        public ReadOnlyReactiveProperty<float> MaxSpecialPoint => maxSpecialPointSubject;
        public Observable<StrikerInpact> OnInpactGenerated => onInpactGeneratedSubject;
        public Observable<AttentionRequest> OnAtentionRequested => onAttentionRequestedSubject;

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
            UnityEngine.Object.Destroy(this.Rigidbody.gameObject);
        }

        public Observable<Unit> OnDead => onDeadSubject;

        public AliceStrikerHub() {
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

            UpdateEnemyInFrontState();
            NotifyEnemyBehindOnStateChanged();

            stateMachine.CurrentState.OnUpdate(stateMachine);
            NotifyEnemyBehindOnStateChanged();
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
            positionSubject.Dispose();
            centerPositionSubject.Dispose();
            lookDirectionSubject.Dispose();
            velocitySubject.Dispose();
            hitPointSubject.Dispose();
            maxHitPointSubject.Dispose();
            specialPointSubject.Dispose();
            maxSpecialPointSubject.Dispose();
            onInpactGeneratedSubject.Dispose();
            onAttentionRequestedSubject.Dispose();
        }

        public void ApplyDamage(float damage) {
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
            if (stateMachine == null) return;
            onDeadSubject.OnNext(Unit.Default);
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

        public void GenerateInpact(StrikerInpact command) {
            onInpactGeneratedSubject.OnNext(command);
        }

        public void RequestAttention(AttentionRequest request) {
            onAttentionRequestedSubject.OnNext(request);
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