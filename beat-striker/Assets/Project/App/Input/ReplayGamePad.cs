using App;
using R3;
using UnityEngine;

namespace Alice {
    public class ReplayGamePad : IGamePad {
        readonly Subject<Vector2> direction = new();
        readonly Subject<Unit> directionCanceled = new();
        readonly Subject<GamePadButton> buttonDown = new();
        readonly Subject<GamePadButton> buttonUp = new();

        public ReplayGamePad(int playerId) {
            DeviceName = $"Replay Player {playerId}";
        }

        public Observable<Vector2> OnDirectionAsObservable => direction;
        public Observable<Unit> OnDirectionCanceledAsObservable => directionCanceled;
        public Observable<GamePadButton> OnButtonDownAsObservable => buttonDown;
        public Observable<GamePadButton> OnButtonUpAsObservable => buttonUp;
        public string DeviceName { get; }

        public void EmitDirection(Vector2 value) {
            if (value.sqrMagnitude <= 0.0001f) {
                directionCanceled.OnNext(Unit.Default);
                return;
            }

            direction.OnNext(value.normalized);
        }

        public void EmitButtonDown(GamePadButton button) {
            buttonDown.OnNext(button);
        }

        public void EmitButtonUp(GamePadButton button) {
            buttonUp.OnNext(button);
        }

        public void DestroyGamePad() {
            direction.Dispose();
            directionCanceled.Dispose();
            buttonDown.Dispose();
            buttonUp.Dispose();
        }
    }
}
