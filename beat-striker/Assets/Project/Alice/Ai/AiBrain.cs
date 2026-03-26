using R3;
using System;
using UnityEngine;
using VContainer;

namespace Alice {
    public record AiObservation(
        IReadOnlyBattleEntity Self,
        IReadOnlyBattleEntity Opponent,
        IMusicPlayer.BeatSignal Signal,
        float CurrentPlaybackTime
    );

    public record AiAction(Vector2 Direction, GamePadButton? Button) {
        public static readonly AiAction None = new(Vector2.zero, null);
    }

    public abstract class AiBrain : MonoBehaviour, IGamePad {
        readonly Subject<Vector2> onDirection = new();
        readonly Subject<Unit> onDirectionCanceled = new();
        readonly Subject<GamePadButton> onButtonDown = new();
        readonly Subject<GamePadButton> onButtonUp = new();

        IMusicPlayer musicPlayer;
        IStrikerRegistry strikerRegistry;
        IDisposable goodZoneSubscription;
        IDisposable beatTimingSubscription;
        bool isAiMode;
        IReadOnlyBattleEntity selfStriker;
        AiAction pendingAction = AiAction.None;

        public Observable<Vector2> OnDirectionAsObservable => onDirection;
        public Observable<Unit> OnDirectionCanceledAsObservable => onDirectionCanceled;
        public Observable<GamePadButton> OnButtonDownAsObservable => onButtonDown;
        public Observable<GamePadButton> OnButtonUpAsObservable => onButtonUp;
        public string DeviceName => nameof(AiBrain);

        [Inject]
        public void Construct(IMusicPlayer musicPlayer, IStrikerRegistry strikerRegistry) {
            this.musicPlayer = musicPlayer;
            this.strikerRegistry = strikerRegistry;
        }

        public void EnableAiMode(IReadOnlyBattleEntity self) {
            enabled = true;
            if (this.isAiMode) {
                return;
            }
            this.selfStriker = self;
            this.isAiMode = true;
            pendingAction = AiAction.None;
            OnAiEnabled();

            goodZoneSubscription = musicPlayer.OnGoodZoneEntered.Subscribe(signal => {
                if (!this.isAiMode) {
                    return;
                }

                var opponent = ResolveOpponent(selfStriker);
                if (opponent == null) {
                    pendingAction = AiAction.None;
                    return;
                }

                var observation = new AiObservation(
                    selfStriker,
                    opponent,
                    signal,
                    musicPlayer.CurrentPlaybackTime
                );
                pendingAction = OnGoodWindow(observation);
            });

            beatTimingSubscription = musicPlayer.OnBeatTiming.Subscribe(_ => {
                if (!this.isAiMode) {
                    return;
                }
                ApplyActionAtBeatTiming(pendingAction);
                pendingAction = AiAction.None;
            });
        }

        public void DisableAiMode() {
            enabled = false;
            if (!this.isAiMode) {
                return;
            }
            this.isAiMode = false;
            OnAiDisabled();
            CancelDirection();
            goodZoneSubscription?.Dispose();
            goodZoneSubscription = null;
            beatTimingSubscription?.Dispose();
            beatTimingSubscription = null;
            pendingAction = AiAction.None;
        }

        // Note: legacy compatibility methods removed — callers should use EnableAiMode/DisableAiMode directly.

        void EmitDirection(Vector2 direction) {
            onDirection.OnNext(direction);
        }

        void CancelDirection() {
            onDirectionCanceled.OnNext(Unit.Default);
        }

        void Press(GamePadButton button) {
            onButtonDown.OnNext(button);
            onButtonUp.OnNext(button);
        }

        void ApplyActionAtBeatTiming(AiAction action) {
            if (action.Button.HasValue) {
                Press(action.Button.Value);
            }

            if (action.Direction.sqrMagnitude <= 0.000001f) {
                CancelDirection();
                return;
            }

            EmitDirection(action.Direction.normalized);
        }

        IReadOnlyBattleEntity ResolveOpponent(IReadOnlyBattleEntity self) {
            IReadOnlyBattleEntity nearestOpponent = null;
            var nearestSqrDistance = float.MaxValue;

            foreach (var striker in strikerRegistry.GetAllStrikers()) {
                if (striker.PlayerId == self.PlayerId || striker.HitPoint <= 0f) {
                    continue;
                }

                var sqrDistance = (striker.Position - self.Position).sqrMagnitude;
                if (sqrDistance >= nearestSqrDistance) {
                    continue;
                }

                nearestOpponent = striker;
                nearestSqrDistance = sqrDistance;
            }

            return nearestOpponent;
        }

        protected abstract AiAction OnGoodWindow(AiObservation observation);
        protected virtual void OnAiEnabled() { }
        protected virtual void OnAiDisabled() { }

        void OnDestroy() {
            goodZoneSubscription?.Dispose();
            beatTimingSubscription?.Dispose();
            onDirection.Dispose();
            onDirectionCanceled.Dispose();
            onButtonDown.Dispose();
            onButtonUp.Dispose();
        }
    }
}