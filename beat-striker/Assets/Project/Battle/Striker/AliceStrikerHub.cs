using System;
using System.Collections.Generic;
using Alice;
using R3;
using UnityEngine;

public record StrikerImpact(Vector3 DirectionAndMagnitude);
public record AttentionRequest(float DurationSeconds, string TechniqueText = "", Action OnReturnCompleted = null, float? ZoomSmoothTime = null, Action OnReturnStarted = null, float? ZoomDistance = null, bool ResumeOnReturnStarted = false);

public interface IStrikerContext {
    Rigidbody Rigidbody { get; }
    Vector2 InputDirection { get; }
    Vector2 LocalInputDirection { get; }
    IEnumerable<IObservableStriker> GetAllStrikers();
    IObservableStriker GetSelf();
    IObservableStriker GetOpponent();
    float BeginRecoveringGuard();
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
        void TickPhysics(float deltaTime);
        void RecordRemoteReplicaHistory(float networkTime);
        OnlineStrikerPreBeatStateSnapshot BuildPreBeatStateSnapshot(int applyBeatIndex, float sentNetworkTime);
        void ApplyPreBeatStateDelta(OnlineStrikerPreBeatStateSnapshot snapshot);
        void ApplyReplayAbsoluteState(ReplayPreBeatStatePayload snapshot);
        void ChangeDirection(Vector2 direction);
        void CancelDirection();
        void ConfigureSurvival(bool preventsHitPointDeath, float hitPointRecoveryRatioPerSecond);
        void RestoreHitPoint();
#if UNITY_EDITOR
        void ApplyDamage(float damage);
#endif
        void Default();
        void Dash();
        void Attack();
        void Charge();
        void Special();
        void Guard();
        void CompleteBeat();
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

    public class AliceStrikerHub : IStrikerContext, IStrikerHub, IStrikerTutorialSource, IDisposable {
        const float MaximumGuardSeconds = 1f;
        const float GuardRecoveryPerBeat = .1f;
        const float GuardUseMultiplier = .5f;
        bool requestingGuard;
        bool guardedThisBeat;

        public float NextGuardSeconds { get; private set; } = MaximumGuardSeconds;
        public float ActiveGuardSeconds { get; private set; } = MaximumGuardSeconds;

        const float HISTORY_SECONDS = 1f;
        const float POSITION_SNAP_THRESHOLD = 0.2f;
        const float POSITION_LERP_RATE = 0.5f;
        IStrikerRegistry strikerRegistry;
        IBattleOnlineSync battleOnlineSync;

        float maxHitPoint;
        float maxSpecialPoint;
        float deathHeightY;
        StrikerState defaultState;
        StrikerState deadState;
        StrikerState victoryState;
        StrikerState introState;

        Rigidbody rb;
        GameObject strikerGameObject;
        StrikerHub legacyHub;
        AnimationPlayer animationPlayer;
        StrikerStateMachine stateMachine;
        readonly List<StrikerSyncHistoryFrame> syncHistory = new();
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
        readonly Subject<TutorialCommandContext> onTutorialCommandExecuted = new();
        int tutorialCommandId;
        GamePadButton tutorialCommandButton;
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
        bool preventsHitPointDeath;
        float hitPointRecoveryRatioPerSecond;
        const float HIT_POINT_RECOVERY_DELAY_SECONDS = 0.2f;
        float hitPointRecoveryDelayRemaining;

        bool IsUnableToAct => isDead || (currentHitPoint <= 0f && !preventsHitPointDeath);

        record StrikerSyncHistoryFrame(
            float NetworkTime,
            float HitPoint,
            float SpecialPoint,
            Vector3 Position,
            string StatePathId);

        public Vector2 InputDirection => inputDirection;
        public Rigidbody Rigidbody => rb;
        public IStrikerState ObservedState => stateMachine?.CurrentState;
        public float StateElapsedSeconds => stateMachine?.StateElapsedSeconds ?? 0f;
        public int StateVersion => stateMachine?.StateVersion ?? 0;
        public Bounds BodyBounds => legacyHub.BodyBounds;
        public Bounds? MovementBounds => legacyHub.MovementBounds;
        public Transform ObservationRoot => strikerTransform;
        public IReadOnlyList<TutorialActionLesson> TutorialLessons => legacyHub.TutorialLessons;
        public Observable<TutorialCommandContext> OnTutorialCommandExecuted => onTutorialCommandExecuted;
        public TutorialCommandContext CaptureTutorialCommand() => new(tutorialCommandId, tutorialCommandButton,
            new Vector2(inputDirection.x * Mathf.Sign(strikerTransform.forward.x), inputDirection.y),
            legacyHub.TutorialGroundSource.IsGrounded);

        TutorialCommandContext StartTutorialCommand(GamePadButton button) {
            if (TutorialLessons.Count == 0) return null;
            tutorialCommandId++;
            tutorialCommandButton = button;
            return CaptureTutorialCommand();
        }

        void CompleteTutorialCommand(TutorialCommandContext command) {
            if (command != null) onTutorialCommandExecuted.OnNext(command);
        }

        public void ResetTutorialPractice(TutorialReset reset) {
            Default();
            CancelDirection();
            rb.linearVelocity = Vector3.zero;
            legacyHub.ResetTutorialResources(reset);
        }
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

        public void InitializeRuntimeDependencies(IStrikerRegistry strikerRegistry, IBattleOnlineSync battleOnlineSync) {
            this.strikerRegistry = strikerRegistry;
            this.battleOnlineSync = battleOnlineSync;
        }

        public void ConfigureSurvival(bool preventsHitPointDeath, float hitPointRecoveryRatioPerSecond) {
            this.preventsHitPointDeath = preventsHitPointDeath;
            this.hitPointRecoveryRatioPerSecond = Mathf.Max(0f, hitPointRecoveryRatioPerSecond);
        }

        public void RestoreHitPoint() {
            if (isDead) return;
            currentHitPoint = maxHitPoint;
            hitPointSubject.OnNext(currentHitPoint);
        }

        public void Tick(float deltaTime) {
            if (!initialized) return;

            legacyHub.ConstrainToStage();

            var recoveryDeltaTime = Mathf.Max(0f, deltaTime - hitPointRecoveryDelayRemaining);
            hitPointRecoveryDelayRemaining = Mathf.Max(0f, hitPointRecoveryDelayRemaining - Mathf.Max(0f, deltaTime));
            if (!isDead && currentHitPoint < maxHitPoint && hitPointRecoveryRatioPerSecond > 0f && recoveryDeltaTime > 0f) {
                currentHitPoint = Mathf.MoveTowards(currentHitPoint, maxHitPoint, maxHitPoint * hitPointRecoveryRatioPerSecond * recoveryDeltaTime);
                hitPointSubject.OnNext(currentHitPoint);
            }

            if (stateMachine == null) {
                stateMachine = new StrikerStateMachine(this, defaultState);
            }

            var currentPosition = rb.position;
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
            legacyHub.ConstrainToStage();
            NotifyEnemyBehindOnStateChanged();
            currentStateCategorySubject.OnNext(stateMachine.CurrentState.Category);
            if (battleOnlineSync != null && battleOnlineSync.IsReady) {
                RecordRemoteReplicaHistory(battleOnlineSync.NetworkTime);
            }
        }

        public void TickPhysics(float deltaTime) {
            if (!initialized) return;

            var currentPosition = rb.position;
            frameVelocity = deltaTime > 0f ? (currentPosition - previousFramePosition) / deltaTime : Vector3.zero;
            previousFramePosition = currentPosition;
            velocitySubject.OnNext(frameVelocity);
        }

        public void InitializeFromLegacy(StrikerHub legacy) {
            legacyHub = legacy;
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
            hitPointRecoveryDelayRemaining = 0f;
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

        public void RecordRemoteReplicaHistory(float networkTime) {
            if (!initialized || stateMachine == null) {
                return;
            }

            syncHistory.Add(new StrikerSyncHistoryFrame(
                networkTime,
                currentHitPoint,
                currentSpecialPoint,
                rb.position,
                GetCurrentStatePathId()));

            var expireBefore = networkTime - HISTORY_SECONDS;
            while (syncHistory.Count > 0 && syncHistory[0].NetworkTime < expireBefore) {
                syncHistory.RemoveAt(0);
            }
        }

        public OnlineStrikerPreBeatStateSnapshot BuildPreBeatStateSnapshot(int applyBeatIndex, float sentNetworkTime) {
            return new OnlineStrikerPreBeatStateSnapshot(
                0,
                applyBeatIndex,
                playerId,
                currentHitPoint,
                currentSpecialPoint,
                rb.position,
                GetCurrentStatePathId(),
                sentNetworkTime);
        }

        public void ApplyPreBeatStateDelta(OnlineStrikerPreBeatStateSnapshot snapshot) {
            if (!initialized || stateMachine == null || !TryGetNearestHistory(snapshot.SentNetworkTime, out var history)) {
                return;
            }

            ApplyHitPointDelta(snapshot.HitPoint - history.HitPoint);
            ApplySpecialPointDelta(snapshot.SpecialPoint - history.SpecialPoint);
            ApplyPositionDelta(snapshot.Position - history.Position);
            // State correction must use the local state near sent time, not "current now".
            // In this game, state can change within a single frame; comparing with current state
            // causes false mismatches and unnecessary state rewinds.
            if (!string.IsNullOrEmpty(snapshot.StatePathId)) {
                ApplyStateCorrectionIfNeeded(snapshot.StatePathId, history.StatePathId);
            }
        }

        public void ApplyReplayAbsoluteState(ReplayPreBeatStatePayload snapshot) {
            if (!initialized || stateMachine == null) {
                return;
            }

            if (snapshot.hitPoint < currentHitPoint) {
                hitPointRecoveryDelayRemaining = HIT_POINT_RECOVERY_DELAY_SECONDS;
            }
            currentHitPoint = Mathf.Clamp(snapshot.hitPoint, 0f, maxHitPoint);
            currentSpecialPoint = Mathf.Clamp(snapshot.specialPoint, 0f, maxSpecialPoint);
            rb.position = snapshot.position;
            legacyHub.ConstrainToStage();
            previousFramePosition = rb.position;
            frameVelocity = Vector3.zero;
            hitPointSubject.OnNext(currentHitPoint);
            specialPointSubject.OnNext(currentSpecialPoint);
            positionSubject.OnNext(rb.position);
            centerPositionSubject.OnNext(centerPositionTransform.position);
            velocitySubject.OnNext(frameVelocity);
            ApplyStateCorrectionIfNeeded(snapshot.statePathId);
            if (currentHitPoint <= 0f && !preventsHitPointDeath) {
                Die();
            }
        }

        public void SetPlayerId(int playerId) {
            this.playerId = playerId;
            playerIdSubject.OnNext(playerId);
        }

        public void GiveHit(HitStatus status) {
            if (stateMachine == null || IsUnableToAct) return;
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
            onTutorialCommandExecuted.Dispose();
        }

        public void ApplyDamage(float damage) {
            if (isDead) {
                return;
            }

            if (damage > 0f) {
                hitPointRecoveryDelayRemaining = HIT_POINT_RECOVERY_DELAY_SECONDS;
            }
            currentHitPoint = Mathf.Max(0f, currentHitPoint - damage);
            hitPointSubject.OnNext(currentHitPoint);
            if (currentHitPoint <= 0f && !preventsHitPointDeath) {
                Die();
            }
        }

        bool TryGetNearestHistory(float networkTime, out StrikerSyncHistoryFrame nearest) {
            nearest = null;
            if (syncHistory.Count == 0) {
                return false;
            }

            var nearestIndex = 0;
            var nearestDistance = Mathf.Abs(syncHistory[0].NetworkTime - networkTime);
            for (var i = 1; i < syncHistory.Count; i++) {
                var distance = Mathf.Abs(syncHistory[i].NetworkTime - networkTime);
                if (distance >= nearestDistance) {
                    continue;
                }

                nearestIndex = i;
                nearestDistance = distance;
            }

            nearest = syncHistory[nearestIndex];
            return true;
        }

        void ApplyHitPointDelta(float delta) {
            if (Mathf.Abs(delta) <= 0.0001f) {
                return;
            }

            if (delta < 0f) {
                hitPointRecoveryDelayRemaining = HIT_POINT_RECOVERY_DELAY_SECONDS;
            }
            currentHitPoint = Mathf.Clamp(currentHitPoint + delta, 0f, maxHitPoint);
            hitPointSubject.OnNext(currentHitPoint);
            if (currentHitPoint <= 0f && !preventsHitPointDeath) {
                Die();
            }
        }

        void ApplySpecialPointDelta(float delta) {
            if (Mathf.Abs(delta) <= 0.0001f) {
                return;
            }

            currentSpecialPoint = Mathf.Clamp(currentSpecialPoint + delta, 0f, maxSpecialPoint);
            specialPointSubject.OnNext(currentSpecialPoint);
        }

        void ApplyPositionDelta(Vector3 delta) {
            if (delta.sqrMagnitude <= 0.000001f) {
                return;
            }

            var targetPosition = rb.position + delta;
            rb.position = delta.magnitude >= POSITION_SNAP_THRESHOLD
                ? targetPosition
                : Vector3.Lerp(rb.position, targetPosition, POSITION_LERP_RATE);
            legacyHub.ConstrainToStage();
            positionSubject.OnNext(rb.position);
            centerPositionSubject.OnNext(centerPositionTransform.position);
        }

        void ApplyStateCorrectionIfNeeded(string ownerStatePathId, string localHistoricalStatePathId = null) {
            // Pre-beat delta sync should compare against the local state near sent time.
            // If historical states already match, skip correction even when current state differs.
            if (localHistoricalStatePathId != null && localHistoricalStatePathId == ownerStatePathId) {
                return;
            }

            if (GetCurrentStatePathId() == ownerStatePathId) {
                return;
            }

            if (legacyHub.TryGetStateByPathId(ownerStatePathId, out var targetState)) {
                stateMachine.ChangeState(targetState, true);
                currentStateCategorySubject.OnNext(stateMachine.CurrentState.Category);
            }
        }

        string GetCurrentStatePathId() {
            if (stateMachine == null || stateMachine.CurrentState == null) {
                return string.Empty;
            }

            return legacyHub.TryGetStatePathId(stateMachine.CurrentState, out var pathId)
                ? pathId
                : string.Empty;
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
            if (stateMachine == null || IsUnableToAct) return;
            var command = StartTutorialCommand(GamePadButton.West);
            stateMachine.CurrentState.OnDashRequested(stateMachine);
            CompleteTutorialCommand(command);
        }

        public void Attack() {
            if (stateMachine == null || IsUnableToAct) return;
            var command = StartTutorialCommand(GamePadButton.East);
            stateMachine.CurrentState.OnAttackRequested(stateMachine);
            CompleteTutorialCommand(command);
        }

        public void Charge() {
            if (stateMachine == null || IsUnableToAct) return;
            var command = StartTutorialCommand(GamePadButton.South);
            stateMachine.CurrentState.OnChargeRequested(stateMachine);
            CompleteTutorialCommand(command);
        }

        public void Special() {
            if (stateMachine == null || IsUnableToAct) return;

            if (!CanUseSpecial()) {
                onSpecialRequestFailedSubject.OnNext(Unit.Default);
                return;
            }

            var command = StartTutorialCommand(GamePadButton.Start);
            currentSpecialPoint = 0f;
            specialPointSubject.OnNext(currentSpecialPoint);
            stateMachine.CurrentState.OnSpecialRequested(stateMachine);
            CompleteTutorialCommand(command);
        }

        public void Guard() {
            if (stateMachine == null || IsUnableToAct) return;
            var command = StartTutorialCommand(GamePadButton.North);
            requestingGuard = true;
            try {
                stateMachine.CurrentState.OnGuardRequested(stateMachine);
            } finally {
                requestingGuard = false;
            }
            CompleteTutorialCommand(command);
        }

        public float BeginRecoveringGuard() {
            // State correction can restore a guard without executing a new command.
            if (requestingGuard) {
                ActiveGuardSeconds = NextGuardSeconds;
                NextGuardSeconds *= GuardUseMultiplier;
                guardedThisBeat = true;
            }
            return ActiveGuardSeconds;
        }

        public void CompleteBeat() {
            if (!guardedThisBeat && strikerSubject.CurrentValue is Alice.Striker.Warrior or Alice.Striker.DemonKing) {
                NextGuardSeconds = Mathf.Min(MaximumGuardSeconds, NextGuardSeconds + GuardRecoveryPerBeat);
            }
            guardedThisBeat = false;
            legacyHub.CompleteBeat();
        }

        public void AddSpecialPoint(float value) {
            if (value <= 0f) return;

            currentSpecialPoint = Mathf.Clamp(currentSpecialPoint + value, 0f, maxSpecialPoint);
            specialPointSubject.OnNext(currentSpecialPoint);
        }

        public void Default() {
            if (!initialized || IsUnableToAct) return;
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
            if (stateMachine == null || rb == null) return;
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
