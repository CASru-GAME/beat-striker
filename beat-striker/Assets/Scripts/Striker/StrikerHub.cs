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

namespace Core.Striker {
    [RequireComponent(typeof(Life))]
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(CapsuleCollider))]
    [AddComponentMenu(" Striker Hub", 0)]
    public class StrikerHub : MonoBehaviour, IStrikerStateContext, IStrikerNodeContext, IStrikerHit {
        [Header("Striker Settings")]
        [SerializeField] private HitPoint maxHitPoint = new(100);
        [SerializeField] private SpecialPoint maxSpecialPoint = new(100);

        [Header("References")]
        [SerializeField] private StrikerState defaultState;
        [SerializeField] private StrikerState deadState, VictoryState, IntroState;

        private Rigidbody rb;
        [SerializeField] private Animator animator;
        private Vector3 initialPosition;
        private Quaternion initialRotation;
        
        private IBus bus;
        private IStrikerModel model;
        private IPlayerRegistry playerRegistry;
        private IRythmTrackModel rythmTrackModel;

        private bool isInputEnabled = false;

        public Vector2 InputDirection { get; private set; }
        public Rigidbody Rigidbody => rb;

        private IStrikerState currentState;
        private Coroutine currentAnimationCoroutine;

        // Playable API
        private PlayableGraph playableGraph;
        private AnimationMixerPlayable mixer;
        private AnimationClipPlayable currentClipPlayable;
        private AnimationClipPlayable previousClipPlayable;

        private void Awake() {
            rb = GetComponent<Rigidbody>();
            rb.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionZ;

            // PlayableGraphの初期化
            playableGraph = PlayableGraph.Create("StrikerAnimationGraph");
            playableGraph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
            
            // クロスフェード用に2スロット作成
            mixer = AnimationMixerPlayable.Create(playableGraph, 2);
            var output = AnimationPlayableOutput.Create(playableGraph, "Animation", animator);
            output.SetSourcePlayable(mixer);
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
            currentState.OnUpdate(this);
        }

        public void ChangeState(IStrikerState newState) {
            if (newState == null || newState == currentState) return;

            if (currentAnimationCoroutine != null) {
                StopCoroutine(currentAnimationCoroutine);
                currentAnimationCoroutine = null;
            }

            currentState?.OnExit(this);
            currentState = newState;
            currentState.OnEnter(this);
        }

        public void ChangeState() {
            ChangeState(defaultState);
        }

        public void TryTransition(IStrikerNode node) {
            node.OnTryTransition(this);
        }

        public void TryTransition() {
            ChangeState();
        }

        public void PlayAnimation(StrikerAnimationClip animation, Action<IStrikerStateContext> onComplete = null) {
            if (animator == null || animation.clip == null) return;

            if (currentAnimationCoroutine != null) {
                StopCoroutine(currentAnimationCoroutine);
            }

            currentAnimationCoroutine = StartCoroutine(PlayAnimationCoroutine(animation.clip, animation.fadeTime, animation.speed, onComplete));
        }

        private IEnumerator PlayAnimationCoroutine(AnimationClip clip, float fadeTime, float speed, Action<IStrikerStateContext> onComplete) {
            // 前のアニメーションをクリーンアップ
            if (previousClipPlayable.IsValid()) {
                mixer.DisconnectInput(1);
                previousClipPlayable.Destroy();
            }

            // 現在のアニメーションを前のスロットに移動
            if (currentClipPlayable.IsValid()) {
                previousClipPlayable = currentClipPlayable;
                mixer.DisconnectInput(0);
                mixer.ConnectInput(1, previousClipPlayable, 0);
                mixer.SetInputWeight(1, mixer.GetInputWeight(0));
            }

            // 新しいアニメーションを作成してスロット0に接続
            currentClipPlayable = AnimationClipPlayable.Create(playableGraph, clip);
            currentClipPlayable.SetSpeed(speed);
            currentClipPlayable.SetTime(0);
            mixer.ConnectInput(0, currentClipPlayable, 0);
            
            playableGraph.Play();

            if (fadeTime > 0f && previousClipPlayable.IsValid()) {
                // クロスフェード: 両方のウェイトを同時に変化
                float elapsed = 0f;
                float startWeightPrev = mixer.GetInputWeight(1);
                
                while (elapsed < fadeTime) {
                    elapsed += Time.deltaTime;
                    float t = elapsed / fadeTime;
                    mixer.SetInputWeight(0, Mathf.Lerp(0f, 1f, t));      // 新しいアニメーションをフェードイン
                    mixer.SetInputWeight(1, Mathf.Lerp(startWeightPrev, 0f, t)); // 古いアニメーションをフェードアウト
                    yield return null;
                }
                
                // フェード完了後、前のアニメーションを破棄
                mixer.SetInputWeight(0, 1f);
                mixer.SetInputWeight(1, 0f);
                mixer.DisconnectInput(1);
                previousClipPlayable.Destroy();
                previousClipPlayable = default;
            } else {
                // フェードなしの場合は即座に切り替え
                mixer.SetInputWeight(0, 1f);
                if (previousClipPlayable.IsValid()) {
                    mixer.SetInputWeight(1, 0f);
                    mixer.DisconnectInput(1);
                    previousClipPlayable.Destroy();
                    previousClipPlayable = default;
                }
            }

            // ループしないアニメーションの場合は終了を待機して最後のフレームで止める
            if (!clip.isLooping) {
                float clipDuration = clip.length / speed;
                while (currentClipPlayable.IsValid() && currentClipPlayable.GetTime() < clipDuration) {
                    yield return null;
                }
                
                // 最後のフレームで止める
                if (currentClipPlayable.IsValid()) {
                    currentClipPlayable.SetSpeed(0);
                    currentClipPlayable.SetTime(clip.length);
                }
                
                currentAnimationCoroutine = null;
                onComplete?.Invoke(this);
            } else {
                // ループアニメーションの場合はコルーチン終了
                currentAnimationCoroutine = null;
            }
        }

        private void OnDestroy() {
            if (playableGraph.IsValid()) {
                playableGraph.Destroy();
            }
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

            currentState.OnHit(this, status);
        }

        public void ApplyDamage(HitPoint damage) {
            model.TakeDamage(damage);
            if (model.IsDead()) {
                OnDead();
            }
        }

        // Note: For requests, we now need to fetch the state instance.
        public void Dash() {
            currentState.OnDashRequested(this);
        }

        public void Attack() {
            currentState.OnAttackRequested(this);
        }
        // Charge logic: Request charge state. State entry calls Charger.Charge()?
        public void Charge() {
            currentState.OnChargeRequested(this);
        }

        public void Special() {
            if (model.SpecialPoint.value < model.MaxSpecialPoint.value) {
                OnMiss();
                return;
            }
            model.GainSpecial(new SpecialPoint(-model.MaxSpecialPoint.value));
        }

        public void Guard(){
            currentState.OnGuardRequested(this);
        }

        public void OnMiss() { 
            currentState.OnMiss(this);
        }

        public void OnDead() {
            bus.Publish(new BattleMessages.NotifyPlayerDead(model.PlayerId));
            ChangeState(deadState);
        }

        public void OnIntro() {
            ChangeState(IntroState);
        }

        public void OnVictory() {
            ChangeState(VictoryState);
        }

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
