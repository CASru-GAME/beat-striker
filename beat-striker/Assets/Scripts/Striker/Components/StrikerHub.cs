using System;
using System.Collections;
using System.Collections.Generic;
using Core.App.Interfaces;
using Core.App.Types;
using Core.Utils;
using Core.Battle;
using Core.GamePad;
using Core.GamePad.Types;
using Core.Striker.Components;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Animations;

namespace Core.Striker {

    [RequireComponent(typeof(AnimationPlayer))]
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(CapsuleCollider))]
    [AddComponentMenu(" Striker Hub", 0)]
    public class StrikerHub : MonoBehaviour, IStrikerViewWithEvents, IStrikerHit, IStrikerContext {

        [Header("Striker Settings")]
        [SerializeField] private HitPoint maxHitPoint = new(100);
        [SerializeField] private SpecialPoint maxSpecialPoint = new(100);

        [Header("References")]
        [SerializeField] private StrikerState defaultState;
        [SerializeField] private StrikerState deadState, VictoryState, IntroState;

        private Rigidbody rb;
        private Vector3 initialPosition;
        private Quaternion initialRotation;

        // Observable model and events
        private IStrikerModel model;
        private IPlayerRegistry playerRegistry;
        private IRythmTrackModel rythmTrackModel;
        private IBattleModel battleModel;

        // Subscriptions
        private CompositeDisposable subscriptions;
        private bool isInputEnabled = false;

        public Vector2 InputDirection => model?.InputDirection ?? Vector2.zero;
        public Rigidbody Rigidbody => rb;

        private StrikerStateMachine stateMachine;

        private AnimationPlayer animationPlayer;

        private void Awake() {
            rb = GetComponent<Rigidbody>();
            rb.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionZ;

            animationPlayer = GetComponent<AnimationPlayer>();
            stateMachine = new StrikerStateMachine(this, defaultState);
        }

        private void Update() {
            stateMachine.CurrentState.OnUpdate(stateMachine);
        }

        /// <summary>
        /// Construct with observable model and events - no event bus needed
        /// </summary>
        public IStrikerModelGetter Construct(PlayerId playerId, ScoreRule rule, IRythmTrackModel rythmTrackModel, IPlayerRegistry playerRegistry, IBattleModel battleModel) {
            this.rythmTrackModel = rythmTrackModel;
            this.playerRegistry = playerRegistry;
            this.battleModel = battleModel;
            this.model = new StrikerModel(playerId, maxHitPoint, maxSpecialPoint, rule, rythmTrackModel);

            subscriptions = new CompositeDisposable();

            // Subscribe to model events (observable pattern)
            subscriptions.Add(model.SubscribeDied(OnModelDied));
            subscriptions.Add(model.SubscribeBeatResult(OnBeatResult));

            // Subscribe to action events from Model
            subscriptions.Add(model.SubscribeAttack(() => Attack()));
            subscriptions.Add(model.SubscribeDash(() => Dash()));
            subscriptions.Add(model.SubscribeCharge(() => Charge()));
            subscriptions.Add(model.SubscribeGuard(() => Guard()));
            subscriptions.Add(model.SubscribeMiss(() => OnMiss()));
            // Special event from Model assumes successful execution logic in Model
            subscriptions.Add(model.SubscribeSpecial(() => {
                // Trigger Special Animation/State if needed. 
            }));

            // Subscribe to battle events (replaces event bus)
            subscriptions.Add(battleModel.SubscribeRequireIntroPose(OnIntroPoseRequested));
            subscriptions.Add(battleModel.SubscribeRequireVictoryPose(OnVictoryPoseRequested));
            subscriptions.Add(battleModel.SubscribeBattleStarted(_ => OnRoundStart()));
            subscriptions.Add(battleModel.SubscribeRoundFinished(_ => OnRoundEnd()));
            subscriptions.Add(battleModel.SubscribeOutroStarted(_ => OnRoundEnd()));
            subscriptions.Add(rythmTrackModel.SubscribeMissedBeat(OnMissedBeat));

            return model;
        }

        /// <summary>
        /// Legacy construct interface (throws if used without BattleEvents)
        /// </summary>
        public IStrikerModelGetter Construct(PlayerId playerId, ScoreRule rule, IRythmTrackModel rythmTrackModel, IPlayerRegistry playerRegistry) {
            throw new NotSupportedException("StrikerHub requires BattleEvents. Use Construct with BattleEvents parameter.");
        }

        void OnDestroy() {
            subscriptions?.Dispose();
        }

        // GamePad input handling (called from external GamePad system)
        public void HandleGamePadInput(GamePadInput input) {
            var player = playerRegistry.ToPlayerId(input.gamePadId);
            if (isInputEnabled == false || player == null || model.PlayerId != player || model.IsDead()) return;

            model.HandleInput(input);
        }

        public void HandleDirectionChanged(DirectionChange change) {
            var player = playerRegistry.ToPlayerId(change.gamePadId);
            if (isInputEnabled == false || player == null || model.PlayerId != player || model.IsDead()) return;

            model.HandleDirection(change.direction);
        }

        private void OnBeatResult(BeatResult result) {
            // React to beat result if needed
        }

        private void OnMissedBeat(PlayerId playerId) {
            if (model.PlayerId != playerId || model.IsDead()) return;
            OnMiss();
        }

        private void OnRoundStart() {
            isInputEnabled = true;
            model.SetInputEnabled(true);
        }

        private void OnRoundEnd() {
            isInputEnabled = false;
            model.SetInputEnabled(false);
        }

        private void OnIntroPoseRequested(PlayerId playerId) {
            if (model.PlayerId != playerId) return;
            OnIntro();
        }

        private void OnVictoryPoseRequested(PlayerId playerId) {
            if (model.PlayerId != playerId) return;
            OnVictory();
        }

        public void GiveHit(HitStatus status) {
            if (model.IsDead()) return;

            stateMachine.CurrentState.OnHit(stateMachine, status);
        }

        public void ApplyDamage(HitPoint damage) {
            model.TakeDamage(damage);
        }

        private void OnModelDied() {
            stateMachine.ChangeState(deadState);
        }

        public void Dash() {
            Debug.Log("[StrikerHub] Dash Requested");
            stateMachine.CurrentState.OnDashRequested(stateMachine);
        }

        public void Attack() {
            stateMachine.CurrentState.OnAttackRequested(stateMachine);
        }

        public void Charge() {
            stateMachine.CurrentState.OnChargeRequested(stateMachine);
        }

        public void Special() {
            // Logic delegated to Model.
        }

        public void Guard() {
            stateMachine.CurrentState.OnGuardRequested(stateMachine);
        }

        public void OnMiss() {
            stateMachine.CurrentState.OnMiss(stateMachine);
        }

        public void OnDead() {
            stateMachine.ChangeState(deadState);
        }

        public void OnIntro() {
            stateMachine.ChangeState(IntroState);
        }

        public void OnVictory() {
            stateMachine.ChangeState(VictoryState);
        }

        public void OnReset() {
            stateMachine.Reset(defaultState);
        }

        public void SavePosition() {
            initialPosition = transform.position;
            initialRotation = transform.rotation;
        }

        public void ResetPosition() {
            transform.SetPositionAndRotation(initialPosition, initialRotation);
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            model.HandleDirection(Vector2.zero);
        }

        public Vector2 GetForwardDirection() {
            Vector3 forward = transform.forward;
            return new Vector2(forward.x, forward.z).normalized;
        }

        public void ChangeDirection(Vector2 direction) {
            // Legacy - now handled by Model via HandleDirection
        }

        public void CancelDirection() {
            model.HandleDirection(Vector2.zero);
        }

        // IStrikerContext の実装
        public void PlayAnimation(StrikerAnimationClip animation, Action<IStrikerStateContext> onComplete = null) {
            animationPlayer.PlayAnimation(animation, () => onComplete?.Invoke(stateMachine));
        }

        public void ChargeEnd() {
            throw new NotImplementedException();
        }

        public void OnHit() {
            throw new NotImplementedException();
        }

        public HitPoint CalcHit(HitStatus status) {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Striker専用ステートマシン
    /// 汎用StateMachineを継承し、IStrikerStateContext/IStrikerNodeContextの追加プロパティを実装
    /// </summary>
    public class StrikerStateMachine :
        StateMachine<IStrikerNode, IStrikerState, IStrikerContext, StrikerStateMachine>,
        IStrikerStateContext, IStrikerNodeContext {
        public Rigidbody Rigidbody => context.Rigidbody;
        public Vector2 InputDirection => context.InputDirection;

        public StrikerStateMachine(IStrikerContext context, IStrikerState defaultState = default)
            : base(context, defaultState) { }
    }
}
