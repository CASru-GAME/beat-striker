using R3;
using System;
using UnityEngine;
using VContainer;

namespace Alice {
    public record AiObservation(
        IObservableStriker Self,
        IObservableStriker Opponent,
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
        bool isAiMode;
        IObservableStriker selfStriker;

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

        public void EnableAiMode(IObservableStriker self) {
            enabled = true;
            if (this.isAiMode) {
                return;
            }
            this.selfStriker = self;
            this.isAiMode = true;
            OnAiEnabled();

            goodZoneSubscription = musicPlayer.OnGoodZoneEntered.Subscribe(signal => {
                if (!this.isAiMode) {
                    return;
                }

                var opponent = ResolveOpponent(selfStriker);
                if (opponent == null) {
                    CancelDirection();
                    return;
                }

                var observation = new AiObservation(
                    selfStriker,
                    opponent,
                    signal,
                    musicPlayer.CurrentPlaybackTime
                );
                var action = OnGoodWindow(observation);
                ApplyActionAtGoodWindow(action);
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

        void ApplyActionAtGoodWindow(AiAction action) {
            if (action.Button.HasValue) {
                Press(action.Button.Value);
            }

            if (action.Direction.sqrMagnitude <= 0.000001f) {
                CancelDirection();
                return;
            }

            EmitDirection(action.Direction.normalized);
        }

        IObservableStriker ResolveOpponent(IObservableStriker self) {
            IObservableStriker nearestOpponent = null;
            var nearestSqrDistance = float.MaxValue;

            foreach (var striker in strikerRegistry.GetAllStrikers()) {
                if (striker.PlayerId.CurrentValue == self.PlayerId.CurrentValue || striker.HitPoint.CurrentValue <= 0f) {
                    continue;
                }

                var sqrDistance = (striker.Position.CurrentValue - self.Position.CurrentValue).sqrMagnitude;
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
            onDirection.Dispose();
            onDirectionCanceled.Dispose();
            onButtonDown.Dispose();
            onButtonUp.Dispose();
        }
    }
}