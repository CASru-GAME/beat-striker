using System;
using System.Collections;
using System.Collections.Generic;
using Core.App.Interfaces;
using Core.App.Models;
using Core.App.Types;
using Core.Utils;
using Core.Battle;
using Core.GamePad.Types;
using Core.Striker.Components;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Animations;
using R3;

namespace Core.Striker {

    [RequireComponent(typeof(AnimationPlayer))]
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(CapsuleCollider))]
    [AddComponentMenu(" 🟠Striker Hub", 0)]
    public class StrikerHub : MonoBehaviour, IStrikerView, IStrikerHit, IStrikerContext, Alice.IReadOnlyBattleEntity {

        [Header("Striker Settings")]
        [SerializeField] private HitPoint maxHitPoint = new(100);
        [SerializeField] private SpecialPoint maxSpecialPoint = new(100);

        [Header("References")]
        [SerializeField] private StrikerState defaultState;
        [SerializeField] private StrikerState deadState, VictoryState, IntroState;
        [SerializeField] private Alice.AiBrain aiBrain;

        private Rigidbody rb;
        private Vector3 initialPosition;
        private Quaternion initialRotation;

        private IBus bus;
        private IStrikerModel model;
        private IPlayerRegistry playerRegistry;
        private IRythmTrackModel rythmTrackModel;

        private bool isInputEnabled = false;
        private float currentHitPoint;

        public Vector2 InputDirection { get; private set; }
        public Rigidbody Rigidbody => rb;
        public float CurrentHitPoint => currentHitPoint;
        public float MaxHitPoint => maxHitPoint.value;
        public HitPoint InspectorMaxHitPoint => maxHitPoint;
        public SpecialPoint InspectorMaxSpecialPoint => maxSpecialPoint;
        public StrikerState InspectorDefaultState => defaultState;
        public StrikerState InspectorDeadState => deadState;
        public StrikerState InspectorVictoryState => VictoryState;
        public StrikerState InspectorIntroState => IntroState;
        public Alice.AiBrain InspectorAiBrain => aiBrain;
        public int PlayerId => model.PlayerId.value;

        private StrikerStateMachine stateMachine;

        private AnimationPlayer animationPlayer;
        public IEnumerable<Alice.IReadOnlyBattleEntity> GetAllStrikers() {
            var runtime = EnsureAliceRuntimeHub();
            if (runtime != null) return runtime.GetAllStrikers();
            Debug.LogError($"Getting all strikers for player {model.PlayerId}, but legacy AllStrikers is not implemented. Returning empty list.");
            return new List<Alice.IReadOnlyBattleEntity>();
        }
        public Vector3 Position => Rigidbody.position;
        public Vector3 Velocity => Rigidbody.linearVelocity;
        public float HitPoint => currentHitPoint;

        Observable<Unit> Alice.IReadOnlyBattleEntity.OnHit => throw new NotImplementedException();

        public IReadOnlyList<Alice.BattleCommandLog> CommandHistory => throw new NotImplementedException();

        private void Awake() {
            if (FindAnyObjectByType<Alice.AliceScope>() != null) {
                EnsureAliceRuntimeHub();
                enabled = false;
                return;
            }

            rb = GetComponent<Rigidbody>();
            rb.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionZ;

            animationPlayer = GetComponent<AnimationPlayer>();
            model = new StrikerModel(new PlayerId(-1), maxHitPoint, maxSpecialPoint, new ScoreRule(0, 0, 0));
            currentHitPoint = maxHitPoint.value;
        }

        public Alice.AliceStrikerHub EnsureAliceRuntimeHub() {
            var runtime = GetComponent<Alice.AliceStrikerHub>();
            if (runtime == null) {
                runtime = gameObject.AddComponent<Alice.AliceStrikerHub>();
            }
            runtime.InitializeFromLegacy(this);
            return runtime;
        }

        private void Start() {
            stateMachine = new StrikerStateMachine(this, defaultState);
        }

        private void Update() {
            stateMachine.CurrentState.OnUpdate(stateMachine);
        }

        public IStrikerModelGetter Construct(PlayerId playerId, ScoreRule rule, IRythmTrackModel rythmTrackModel, IPlayerRegistry playerRegistry) {
            this.bus = this.GetBus();
            this.rythmTrackModel = rythmTrackModel;
            this.playerRegistry = playerRegistry;
            this.model = new StrikerModel(playerId, maxHitPoint, maxSpecialPoint, rule);
            this.currentHitPoint = model.HitPoint.value;

            bus.Subscribe<GamePadMessages.Inputed>(OnGamePadInputed);
            bus.Subscribe<GamePadMessages.DirectionChanged>(OnGamePadDirectionChanged);
            bus.Subscribe<BattleMessages.RequireIntroPose>(OnIntroMessage);
            bus.Subscribe<BattleMessages.RequireVictoryPose>(OnVictoryMessage);
            bus.Subscribe<BattleMessages.OnBattleStarted>(OnRoundStart);
            bus.Subscribe<BattleMessages.OnBattleFinished>(OnRoundEnd);
            bus.Subscribe<BattleMessages.OnBeat>(OnBeatMessage);

            return model;
        }

        void OnDestroy() {
            if (bus == null) return;

            bus.Unsubscribe<GamePadMessages.Inputed>(OnGamePadInputed);
            bus.Unsubscribe<GamePadMessages.DirectionChanged>(OnGamePadDirectionChanged);
            bus.Unsubscribe<BattleMessages.RequireIntroPose>(OnIntroMessage);
            bus.Unsubscribe<BattleMessages.RequireVictoryPose>(OnVictoryMessage);
            bus.Unsubscribe<BattleMessages.OnBattleStarted>(OnRoundStart);
            bus.Unsubscribe<BattleMessages.OnBattleFinished>(OnRoundEnd);
            bus.Unsubscribe<BattleMessages.OnBeat>(OnBeatMessage);
        }

        // Logic from StrikerPresenter
        private void OnGamePadInputed(GamePadMessages.Inputed msg) {
            if (playerRegistry == null || model == null || rythmTrackModel == null) return;
            var player = playerRegistry.ToPlayerId(msg.gamePadId);
            if (isInputEnabled == false || player == null || model.PlayerId != player || model.IsDead()) return;

            if (msg.action == GamePadAction.Down) {
                if (msg.button == GamePadButton.South) { if (Beat()) Dash(); }
                else if (msg.button == GamePadButton.East) { if (Beat()) Attack(); }
                else if (msg.button == GamePadButton.West) { if (Beat()) { Charge(); } } // Charge request
                else if (msg.button == GamePadButton.LeftTrigger) {
                    if (Beat()) {
                        Special();
                    }
                }
                else if (msg.button == GamePadButton.North) { if (Beat()) Guard(); }
            }

            if (msg.action == GamePadAction.Up && msg.button == GamePadButton.Direction) {
                CancelDirection();
            }
        }

        private void OnGamePadDirectionChanged(GamePadMessages.DirectionChanged msg) {
            if (playerRegistry == null || model == null) return;
            var player = playerRegistry.ToPlayerId(msg.gamePadId);
            if (isInputEnabled == false || player == null || model.PlayerId != player || model.IsDead()) return;

            ChangeDirection(msg.direction);
        }

        private bool Beat() {
            var res = rythmTrackModel.Beat(model.PlayerId);
            model.AddBeatResult(res);
            bus.Publish(new BattleMessages.OnBeat(model.PlayerId, res));
            if (res.status != BeatStatus.Miss) {
                model.GainSpecial();
                return true;
            }
            else {
                OnMiss();
                return false;
            }
        }

        private void OnBeatMessage(BattleMessages.OnBeat msg) {
            if (model.PlayerId != msg.playerId || model.IsDead()) return;
            if (msg.result.status == BeatStatus.Miss) OnMiss();
        }

        private void OnRoundStart(BattleMessages.OnBattleStarted msg) => isInputEnabled = true;

        private void OnRoundEnd(BattleMessages.OnBattleFinished msg) => isInputEnabled = false;

        private void OnIntroMessage(BattleMessages.RequireIntroPose msg) {
            if (model.PlayerId != msg.playerId) return;
            OnIntro();
        }

        private void OnVictoryMessage(BattleMessages.RequireVictoryPose msg) {
            if (model.PlayerId != msg.playerId) return;
            OnVictory();
        }

        public void GiveHit(HitStatus status) {
            if (model == null || stateMachine == null) return;
            if (model.IsDead()) return;

            stateMachine.CurrentState.OnHit(stateMachine, status);
        }

        public void ApplyDamage(float damage) {
            currentHitPoint = Mathf.Max(0f, currentHitPoint - damage);

            if (model != null) {
                model.TakeDamage(new HitPoint(damage));
            }

            if (currentHitPoint <= 0f) {
                OnDead();
            }
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
            if (model == null || stateMachine == null || model.IsDead()) return;
            if (model.SpecialPoint.value < model.MaxSpecialPoint.value) {
                OnMiss();
                return;
            }
            model.GainSpecial(new SpecialPoint(-model.MaxSpecialPoint.value));
        }

        public void Guard() {
            if (stateMachine == null || currentHitPoint <= 0f) return;
            stateMachine.CurrentState.OnGuardRequested(stateMachine);
        }

        public void OnMiss() {
            if (stateMachine == null || currentHitPoint <= 0f) return;
            stateMachine.CurrentState.OnMiss(stateMachine);
        }

        public void OnDead() {
            if (bus != null && model != null) {
                bus.Publish(new BattleMessages.NotifyPlayerDead(model.PlayerId));
            }
            if (stateMachine == null) return;
            stateMachine.ChangeState(deadState);
        }

        public void OnIntro() {
            stateMachine.ChangeState(IntroState);
        }

        public void OnVictory() {
            stateMachine.ChangeState(VictoryState);
        }

        public void OnReset() {
            currentHitPoint = maxHitPoint.value;
            model.Reset();
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
            InputDirection = Vector2.zero;
        }

        public Vector2 GetForwardDirection() {
            Vector3 forward = transform.forward;
            return new Vector2(forward.x, forward.z).normalized;
        }

        public void ChangeDirection(Vector2 direction) {
            InputDirection = direction;
        }

        public void CancelDirection() {
            InputDirection = Vector2.zero;
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
        public IEnumerable<Alice.IReadOnlyBattleEntity> GetAllStrikers() => context.GetAllStrikers();

        public void ApplyDamage(float damage) {
            context.ApplyDamage(damage);
        }

        public void PlayAnimation(StrikerAnimationClip animation, Action<IStrikerStateContext> onComplete = null) {
            context.PlayAnimation(animation, onComplete);
        }

        public StrikerStateMachine(IStrikerContext context, IStrikerState defaultState = default)
            : base(context, defaultState) { }
    }
}
