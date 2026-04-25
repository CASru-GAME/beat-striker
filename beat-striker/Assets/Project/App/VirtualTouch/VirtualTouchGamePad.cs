using System;
using R3;
using UnityEngine;

namespace Alice {
    public class VirtualTouchGamePad : IGamePad, IDisposable {
        readonly Subject<Vector2> onDirection = new();
        readonly Subject<Unit> onDirectionCanceled = new();
        readonly Subject<GamePadButton> onButtonDown = new();
        readonly Subject<GamePadButton> onButtonUp = new();
        readonly Action destroyed;

        public Observable<Vector2> OnDirectionAsObservable => onDirection;
        public Observable<Unit> OnDirectionCanceledAsObservable => onDirectionCanceled;
        public Observable<GamePadButton> OnButtonDownAsObservable => onButtonDown;
        public Observable<GamePadButton> OnButtonUpAsObservable => onButtonUp;
        public string DeviceName => "VirtualTouch";

        public VirtualTouchGamePad(Action destroyed) {
            this.destroyed = destroyed;
        }

        public void EmitDirection(Vector2 direction) {
            onDirection.OnNext(direction);
        }

        public void CancelDirection() {
            onDirectionCanceled.OnNext(Unit.Default);
        }

        public void EmitButtonDown(GamePadButton button) {
            onButtonDown.OnNext(button);
        }

        public void EmitButtonUp(GamePadButton button) {
            onButtonUp.OnNext(button);
        }

        public void DestroyGamePad() {
            destroyed.Invoke();
        }

        public void Dispose() {
            onDirection.Dispose();
            onDirectionCanceled.Dispose();
            onButtonDown.Dispose();
            onButtonUp.Dispose();
        }
    }
}
