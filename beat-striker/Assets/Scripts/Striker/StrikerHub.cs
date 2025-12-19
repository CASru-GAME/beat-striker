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

namespace Core.Striker
{
    [RequireComponent(typeof(Life))]
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(Life))]
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(Animator))]
    [RequireComponent(typeof(StrikerGroundCheck))]
    [RequireComponent(typeof(StrikerCharger))]
    [AddComponentMenu("Striker/Striker Hub")]
    public class StrikerHub : MonoBehaviour, IStrikerHub, IStrikerHit
    {
        [Serializable]
        public struct StateRegistration
        {
            public TransitionType type;
            public StrikerState state;
        }

        [Header("Striker Settings")]
        [SerializeField] private HitPoint maxHitPoint = new(100);
        [SerializeField] private SpecialPoint maxSpecialPoint = new(100);

        [Header("References")]
        [SerializeField] private CollidenRef[] collidenRefs;
        [SerializeField] private StateRegistration[] stateRegistrations;
        [SerializeField] private StrikerState defaultState;

        [Header("Special spawn settings")]
        [SerializeField] private float specialSpawnHeight = 2.0f;
        [SerializeField] private float specialSpawnForward = 0.8f;

        private Rigidbody rb;
        private Animator anim;
        private Vector3 initialPosition;
        private Quaternion initialRotation;

        // Components (Internal use)
        private StrikerGroundCheck groundCheck;
        private StrikerCharger charger;

        // Presenter Dependencies
        private IBus bus;
        private IStrikerModel model;
        private IPlayerRegistry playerRegistry;
        private IRythmTrackModel rythmTrackModel;

        private bool isEnabled = false;

        public Vector2 Direction { get; private set; }

        private IStrikerState currentState;
        // private Dictionary<TransitionType, IStrikerState> states = new Dictionary<TransitionType, IStrikerState>(); // Removed as requested

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            anim = GetComponent<Animator>();
            groundCheck = GetComponent<StrikerGroundCheck>();
            charger = GetComponent<StrikerCharger>();

            foreach (var reg in stateRegistrations)
            {
                if (reg.state != null)
                {
                    reg.state.Setup(this, rb, anim);
                }
            }

            if (defaultState != null)
            {
                ChangeState(defaultState);
            }
        }

        public IStrikerModelGetter Construct(PlayerId playerId, ScoreRule rule, IRythmTrackModel rythmTrackModel, IPlayerRegistry playerRegistry)
        {
            this.bus = this.GetBus();
            this.rythmTrackModel = rythmTrackModel;
            this.playerRegistry = playerRegistry;
            this.model = new StrikerModel(playerId, maxHitPoint, maxSpecialPoint, rule);
            
            var life = GetComponent<Life>();
            life.Link(OnPresenterEnable, OnPresenterDisable);

            return model;
        }

        private void OnPresenterEnable()
        {
            bus.Subscribe<GamePadMessages.Inputed>(OnGamePadInputed);
            bus.Subscribe<GamePadMessages.DirectionChanged>(OnGamePadDirectionChanged);
            bus.Subscribe<BattleMessages.RequireIntroPose>(OnIntroMessage);
            bus.Subscribe<BattleMessages.RequireVictoryPose>(OnVictoryMessage);
            bus.Subscribe<BattleMessages.OnBattleStarted>(OnRoundStart);
            bus.Subscribe<BattleMessages.OnBattleFinished>(OnRoundEnd);
            bus.Subscribe<BattleMessages.OnBeat>(OnBeatMessage);
        }

        private void OnPresenterDisable()
        {
            bus.Unsubscribe<GamePadMessages.Inputed>(OnGamePadInputed);
            bus.Unsubscribe<GamePadMessages.DirectionChanged>(OnGamePadDirectionChanged);
            bus.Unsubscribe<BattleMessages.RequireIntroPose>(OnIntroMessage);
            bus.Unsubscribe<BattleMessages.RequireVictoryPose>(OnVictoryMessage);
            bus.Unsubscribe<BattleMessages.OnBattleStarted>(OnRoundStart);
            bus.Unsubscribe<BattleMessages.OnBattleFinished>(OnRoundEnd);
            bus.Unsubscribe<BattleMessages.OnBeat>(OnBeatMessage);
        }

        private void Update()
        {
            currentState?.OnUpdate();
            HandleMovementStateTransitions();
        }

        private void HandleMovementStateTransitions()
        {
            // Simple movement logic using request system? 
            // Or keep direct for idle/walk because they are continuous?
            // "StrikerWalkState (Use GroundCheck if needed)"
            
            // For now, keep explicit check but attempt transition via request
            // Note: currentState transition logic depends on type check or state type. 
            // We removed StateType property from interface, but we might know it via internal type or registration?
            // The request system is robust enough.
            if (Direction != Vector2.zero)
            {
               RequestTransition(GetState(TransitionType.Walk), TransitionType.Walk);
            }
            else if (Direction == Vector2.zero)
            {
               RequestTransition(GetState(TransitionType.Idle), TransitionType.Idle);
            }
        }

        public void ChangeState(IStrikerState newState)
        {
            if (newState == null || newState == currentState) return;

            currentState?.Exit();
            currentState = newState;
            currentState.Enter();
        }

        public void RequestTransition(IStrikerState targetState, TransitionType type)
        {
            if (targetState != null)
            {
                var req = new StrikerTransitionRequest(type);
                if (currentState == null || currentState.TryTransition(targetState, req))
                {
                    ChangeState(targetState);
                }
            }
        }
        
        private IStrikerState GetState(TransitionType type)
        {
            foreach (var reg in stateRegistrations)
            {
                if(reg.type == type) return reg.state;
            }
            return null;
        }

        public void PlayAnimation(AnimationClip clip, Action onComplete = null)
        {
            if (anim == null || clip == null) return;
            StartCoroutine(PlayAnimationCoroutine(clip, onComplete));
        }

        private IEnumerator PlayAnimationCoroutine(AnimationClip clip, Action onComplete)
        {
            anim.Play(clip.name);
            yield return new WaitForSeconds(clip.length);
            onComplete?.Invoke();
        }

        // Logic from StrikerPresenter
        private void OnGamePadInputed(GamePadMessages.Inputed msg)
        {
            var player = playerRegistry.ToPlayerId(msg.gamePadId);
            if (isEnabled == false || player == null || model.PlayerId != player || model.IsDead()) return;

            if (msg.action == GamePadAction.Down)
            {
                if (msg.button == GamePadButton.South) { if (Beat()) Dash(); }
                else if (msg.button == GamePadButton.East) { if (Beat()) Attack(); }
                else if (msg.button == GamePadButton.West) { if (Beat()) { Charge(); } } // Charge request
                else if (msg.button == GamePadButton.North)
                {
                    if (Beat())
                    {
                        if (model.SpecialPoint.value < model.MaxSpecialPoint.value)
                        {
                            OnMiss();
                            return;
                        }
                        model.GainSpecial(new SpecialPoint(-model.MaxSpecialPoint.value));
                        Special();
                    }
                }
                else if (msg.button == GamePadButton.LeftTrigger) { if (Beat()) Guard(); }
            }
            else if (msg.action == GamePadAction.Up)
            {
                if (msg.button == GamePadButton.Direction) CancelDirection();
                else if (msg.button == GamePadButton.West && charger.IsCharged)
                {
                    if (Beat()) ChargeEnd();
                }
            }
        }

        private void OnGamePadDirectionChanged(GamePadMessages.DirectionChanged msg)
        {
            var player = playerRegistry.ToPlayerId(msg.gamePadId);
            if (isEnabled == false || player == null || model.PlayerId != player || model.IsDead()) return;

            ChangeDirection(msg.direction);
        }

        private bool Beat()
        {
            var res = rythmTrackModel.Beat(model.PlayerId);
            model.AddBeatResult(res);
            bus.Publish(new BattleMessages.OnBeat(model.PlayerId, res));
            if (res.status != BeatStatus.Miss)
            {
                model.GainSpecial();
                return true;
            }
            else
            {
                OnMiss();
                return false;
            }
        }

        private void OnBeatMessage(BattleMessages.OnBeat msg)
        {
            if (model.PlayerId != msg.playerId || model.IsDead()) return;
            if (msg.result.status == BeatStatus.Miss) OnMiss();
        }

        private void OnRoundStart(BattleMessages.OnBattleStarted msg) => isEnabled = true;
        private void OnRoundEnd(BattleMessages.OnBattleFinished msg) => isEnabled = false;

        private void OnIntroMessage(BattleMessages.RequireIntroPose msg)
        {
            if (model.PlayerId != msg.playerId) return;
            OnIntro();
        }

        private void OnVictoryMessage(BattleMessages.RequireVictoryPose msg)
        {
            if (model.PlayerId != msg.playerId) return;
            OnVictory();
        }

        public void TakeDamage(HitStatus status)
        {
            if (model.IsDead()) return;

            OnHit();
            var damage = CalcHit(status);
            model.TakeDamage(damage);
            if (model.IsDead())
            {
                OnDead();
                bus.Publish(new BattleMessages.NotifyPlayerDead(model.PlayerId));
            }
        }

        // --- IStrikerHub Actions (NOW REQUESTS) ---

        public void ChangeDirection(Vector2 direction) => this.Direction = direction;
        public void CancelDirection() => this.Direction = Vector2.zero;

        // Note: For requests, we now need to fetch the state instance.
        public void Dash() => RequestTransition(GetState(TransitionType.Dash), TransitionType.Dash);
        public void Attack() => RequestTransition(GetState(TransitionType.Attack), TransitionType.Attack);
        // Charge logic: Request charge state. State entry calls Charger.Charge()?
        public void Charge() 
        {
            if (Beat()) return; // Already checked in Input? Redundant?
            // Input checks beat. Here we simply request.
            RequestTransition(GetState(TransitionType.Charge), TransitionType.Charge);
        }
        public void ChargeEnd() 
        {
           RequestTransition(GetState(TransitionType.ChargeEnd), TransitionType.ChargeEnd);
        }
        public void Special() => RequestTransition(GetState(TransitionType.Special), TransitionType.Special);
        public void Guard() => RequestTransition(GetState(TransitionType.Guard), TransitionType.Guard);

        public void OnMiss() { }
        public void OnHit() { }
        public void OnDead() { /* Request Dead state? Or just variable? */ }
        public void OnIntro() { /* Request Intro state? */ }
        public void OnVictory() { /* Request Victory state? */ }
        
        public void OnReset()
        {
            RequestTransition(GetState(TransitionType.Idle), TransitionType.Idle);
        }
        
        public HitPoint CalcHit(HitStatus status)
        {
            // Need to check current state type. Since we don't have StateType prop, we can check instance?
            // Or assume state logic should handle damage? 
            // For now, let's just assume normal damage if we can't easily check Guard.
            // Wait, we can check if currentState == GetState(TransitionType.Guard).
            if (currentState == GetState(TransitionType.Guard)) return new HitPoint(status.damage.value / 2);
            return new HitPoint(status.damage.value);
        }

        public void SavePosition()
        {
            initialPosition = transform.position;
            initialRotation = transform.rotation;
        }

        public void ResetPosition()
        {
            transform.position = initialPosition;
            transform.rotation = initialRotation;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            Direction = Vector2.zero;
            RequestTransition(GetState(TransitionType.Idle), TransitionType.Idle);
        }

        public Vector2 GetForwardDirection()
        {
            Vector3 forward = transform.forward;
            return new Vector2(forward.x, forward.z).normalized;
        }

        public Colliden GetColliden(string key)
        {
            foreach (var collidenRef in collidenRefs)
            {
                if (collidenRef.key == key) return collidenRef.colliden;
            }
            return null;
        }
    }
}
