using App;
using R3;
using UnityEngine;

namespace Alice {
    public class RemoteGamePad : IGamePad {
        readonly Subject<Vector2> direction = new();
        readonly Subject<Unit> directionCanceled = new();
        readonly Subject<GamePadButton> buttonDown = new();
        readonly Subject<GamePadButton> buttonUp = new();

        public RemoteGamePad(int playerId) {
            DeviceName = $"Remote Player {playerId}";
        }

        public Observable<Vector2> OnDirectionAsObservable => direction;
        public Observable<Unit> OnDirectionCanceledAsObservable => directionCanceled;
        public Observable<GamePadButton> OnButtonDownAsObservable => buttonDown;
        public Observable<GamePadButton> OnButtonUpAsObservable => buttonUp;
        public string DeviceName { get; }

        public void DestroyGamePad() { }
    }
}
