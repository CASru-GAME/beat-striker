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

namespace Core.Striker {
    [RequireComponent(typeof(Life))]
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(Animator))]
    [AddComponentMenu(" Striker Hub", 0)]
    public class StrikerHub : MonoBehaviour, IStrikerHub, IStrikerHit {
        [Header("Striker Settings")]
        [SerializeField] private HitPoint maxHitPoint = new(100);
        [SerializeField] private SpecialPoint maxSpecialPoint = new(100);

        [Header("References")]
        [SerializeField] private StrikerState defaultState;

        [Header("Special spawn settings")]
        [SerializeField] private float specialSpawnHeight = 2.0f;
        [SerializeField] private float specialSpawnForward = 0.8f;

        private Rigidbody rb;
        private Animator anim;
        private Vector3 initialPosition;
        private Quaternion initialRotation;

        private IBus bus;
        private IStrikerModel model;
        private IPlayerRegistry playerRegistry;
        private IRythmTrackModel rythmTrackModel;

        private bool isInputEnabled = false;

        public Vector2 InputDirection { get; private set; }

        private IStrikerState currentState;
        private StrikerStateContext stateContext;
        private Coroutine currentAnimationCoroutine;

        private void Awake() {
            rb = GetComponent<Rigidbody>();
            anim = GetComponent<Animator>();
            stateContext = new StrikerStateContext(this, rb, anim);

            ChangeState(defaultState);
        }

        public IStrikerModelGetter Construct(PlayerId playerId, ScoreRule rule, IRythmTrackModel rythmTrackModel, IPlayerRegistry playerRegistry) {
            this.bus = this.GetBus();
            this.rythmTrackModel = rythmTrackModel;
            this.playerRegistry = playerRegistry;
            this.model = new StrikerModel(playerId, maxHitPoint, maxSpecialPoint, rule);

            var life = GetComponent<Life>();
            life.Link(OnPresenterEnable, OnPresenterDisable);

            return model;
        }

        private void OnPresenterEnable() {
            bus.Subscribe<GamePadMessages.Inputed>(OnGamePadInputed);
            bus.Subscribe<GamePadMessages.DirectionChanged>(OnGamePadDirectionChanged);
            bus.Subscribe<BattleMessages.RequireIntroPose>(OnIntroMessage);
            bus.Subscribe<BattleMessages.RequireVictoryPose>(OnVictoryMessage);
            bus.Subscribe<BattleMessages.OnBattleStarted>(OnRoundStart);
            bus.Subscribe<BattleMessages.OnBattleFinished>(OnRoundEnd);
            bus.Subscribe<BattleMessages.OnBeat>(OnBeatMessage);
        }

        private void OnPresenterDisable() {
            bus.Unsubscribe<GamePadMessages.Inputed>(OnGamePadInputed);
            bus.Unsubscribe<GamePadMessages.DirectionChanged>(OnGamePadDirectionChanged);
            bus.Unsubscribe<BattleMessages.RequireIntroPose>(OnIntroMessage);
            bus.Unsubscribe<BattleMessages.RequireVictoryPose>(OnVictoryMessage);
            bus.Unsubscribe<BattleMessages.OnBattleStarted>(OnRoundStart);
            bus.Unsubscribe<BattleMessages.OnBattleFinished>(OnRoundEnd);
            bus.Unsubscribe<BattleMessages.OnBeat>(OnBeatMessage);
        }

        private void Update() {
            currentState.OnUpdate(stateContext);
        }

        public void ChangeState(IStrikerState newState) {
            if (newState == currentState) return;

            // 実行中のアニメーションコルーチンを停止
            if (currentAnimationCoroutine != null) {
                StopCoroutine(currentAnimationCoroutine);
                currentAnimationCoroutine = null;
            }

            currentState?.Exit();
            currentState = newState;
            currentState.Enter(stateContext);
        }

        public void PlayAnimation(AnimationClip clip, Action onComplete = null) {
            if (anim == null || clip == null) return;

            if (currentAnimationCoroutine != null) {
                StopCoroutine(currentAnimationCoroutine);
            }

            currentAnimationCoroutine = StartCoroutine(PlayAnimationCoroutine(clip, onComplete));
        }

        private IEnumerator PlayAnimationCoroutine(AnimationClip clip, Action onComplete) {
            anim.Play(clip.name);
            yield return new WaitForSeconds(clip.length);
            currentAnimationCoroutine = null;
            onComplete?.Invoke();
        }

        // Logic from StrikerPresenter
        private void OnGamePadInputed(GamePadMessages.Inputed msg) {
            var player = playerRegistry.ToPlayerId(msg.gamePadId);
            if (isInputEnabled == false || player == null || model.PlayerId != player || model.IsDead()) return;

            if (msg.action == GamePadAction.Down) {
                if (msg.button == GamePadButton.South) { if (Beat()) Dash(); }
                else if (msg.button == GamePadButton.East) { if (Beat()) Attack(); }
                else if (msg.button == GamePadButton.West) { if (Beat()) { Charge(); } } // Charge request
                else if (msg.button == GamePadButton.North) {
                    if (Beat()) {
                        Special();
                    }
                }
                else if (msg.button == GamePadButton.LeftTrigger) { if (Beat()) Guard(); }
            }

            if (msg.action == GamePadAction.Up && msg.button == GamePadButton.Direction) {
                CancelDirection();
            }
        }

        private void OnGamePadDirectionChanged(GamePadMessages.DirectionChanged msg) {
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
            if (model.IsDead()) return;

            currentState.OnHit(stateContext, status);
        }

        public void ApplyDamage(HitPoint damage) {
            model.TakeDamage(damage);
            if (model.IsDead()) {
                OnDead();
            }
        }

        // Note: For requests, we now need to fetch the state instance.
        public void Dash() {
            currentState.OnAttackRequested(stateContext);
        }

        public void Attack() {
            currentState.OnDashRequested(stateContext);
        }
        // Charge logic: Request charge state. State entry calls Charger.Charge()?
        public void Charge() {
            currentState.OnChargeRequested(stateContext);
        }

        public void Special() {
            if (model.SpecialPoint.value < model.MaxSpecialPoint.value) {
                OnMiss();
                return;
            }
            model.GainSpecial(new SpecialPoint(-model.MaxSpecialPoint.value));
        }

        public void Guard(){
            currentState.OnGuardRequested(stateContext);
        }

        public void OnMiss() { 
            currentState.OnMiss(stateContext);
        }

        public void OnDead() {
            bus.Publish(new BattleMessages.NotifyPlayerDead(model.PlayerId));
        }

        public void OnIntro() { /* Request Intro state? */ }
        public void OnVictory() { /* Request Victory state? */ }

        public void OnReset() {
            ChangeState(defaultState);
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
}
