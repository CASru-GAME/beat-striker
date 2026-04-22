using R3;
using UnityEngine;

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

        bool isAiMode;

        public Observable<Vector2> OnDirectionAsObservable => onDirection;
        public Observable<Unit> OnDirectionCanceledAsObservable => onDirectionCanceled;
        public Observable<GamePadButton> OnButtonDownAsObservable => onButtonDown;
        public Observable<GamePadButton> OnButtonUpAsObservable => onButtonUp;
        public string DeviceName => nameof(AiBrain);

        public void DestroyGamePad() {
            DisableAiMode();
        }

        public void ApplyLearningMode(bool isLearning) {
            OnLearningModeChanged(isLearning);
        }

        public void EnableAiMode() {
            enabled = true;
            if (this.isAiMode) {
                return;
            }
            this.isAiMode = true;
            OnAiEnabled();
        }

        public void RequestActionOnExcellentWindow(IObservableStriker self, IObservableStriker opponent, IMusicPlayer.BeatSignal signal, float currentPlaybackTime) {
            if (!this.isAiMode) {
                return;
            }

            if (opponent == null) {
                CancelDirection();
                return;
            }

            var observation = new AiObservation(
                self,
                opponent,
                signal,
                currentPlaybackTime
            );
            var action = OnGoodWindow(observation);
            ApplyActionAtGoodWindow(action);
        }

        public void DisableAiMode() {
            enabled = false;
            if (!this.isAiMode) {
                return;
            }
            this.isAiMode = false;
            OnAiDisabled();
            CancelDirection();
        }

        public virtual void EndRoundEpisode() {
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

        protected abstract AiAction OnGoodWindow(AiObservation observation);
        protected virtual void OnAiEnabled() { }
        protected virtual void OnAiDisabled() { }
        protected virtual void OnLearningModeChanged(bool isLearning) { }

        protected virtual void OnDestroy() {
            onDirection.Dispose();
            onDirectionCanceled.Dispose();
            onButtonDown.Dispose();
            onButtonUp.Dispose();
        }
    }
}