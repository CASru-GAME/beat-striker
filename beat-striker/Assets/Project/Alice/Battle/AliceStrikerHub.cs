using System;
using Core.App.Types;
using Core.Battle;
using Core.Striker;
using Core.Striker.Components;
using UnityEngine;
using VContainer;

namespace Alice {
    [RequireComponent(typeof(AnimationPlayer))]
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(CapsuleCollider))]
    public class AliceStrikerHub : MonoBehaviour, IStrikerHit, IStrikerContext {
        [Inject] IBattleFlow battleFlow;

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

        Vector2 inputDirection;
        float currentHitPoint;
        int playerId;
        bool initialized;

        public Vector2 InputDirection => inputDirection;
        public Rigidbody Rigidbody => rb;
        public float CurrentHitPoint => currentHitPoint;
        public float MaxHitPoint => maxHitPoint.value;
        public AiBrain AiBrain => aiBrain;

        void Awake() {
            rb = GetComponent<Rigidbody>();
            rb.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionZ;
            animationPlayer = GetComponent<AnimationPlayer>();
        }

        void Start() {
            if (!initialized) return;
            stateMachine = new StrikerStateMachine(this, defaultState);
        }

        void Update() {
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
        }

        public void ApplyDamage(float damage) {
            currentHitPoint = Mathf.Max(0f, currentHitPoint - damage);
            if (currentHitPoint <= 0f) {
                OnDead();
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
            if (stateMachine != null) {
                stateMachine.Reset(defaultState);
            }
        }

        public void PlayAnimation(StrikerAnimationClip animation, Action<IStrikerStateContext> onComplete = null) {
            animationPlayer.PlayAnimation(animation, () => onComplete?.Invoke(stateMachine));
        }
    }
}