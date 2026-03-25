using R3;
using System;
using UnityEngine;
using VContainer;

namespace Alice {
    public abstract class AiBrain : MonoBehaviour, IGamePad {
        [SerializeField] protected Vector2 directionInput = Vector2.up;

        readonly Subject<Vector2> onDirection = new();
        readonly Subject<Unit> onDirectionCanceled = new();
        readonly Subject<GamePadButton> onButtonDown = new();
        readonly Subject<GamePadButton> onButtonUp = new();

        IMusicPlayer musicPlayer;
        IDisposable aiSubscription;
        bool isAiMode;

        public Observable<Vector2> OnDirectionAsObservable => onDirection;
        public Observable<Unit> OnDirectionCanceledAsObservable => onDirectionCanceled;
        public Observable<GamePadButton> OnButtonDownAsObservable => onButtonDown;
        public Observable<GamePadButton> OnButtonUpAsObservable => onButtonUp;
        public string DeviceName => nameof(AiBrain);

        [Inject]
        public void Construct(IMusicPlayer musicPlayer) {
            this.musicPlayer = musicPlayer;
        }

        public void SetAiMode(bool isAiMode) {
            if (this.isAiMode == isAiMode) {
                return;
            }

            this.isAiMode = isAiMode;
            if (isAiMode) {
                OnAiEnabled();
                aiSubscription = musicPlayer.OnGoodZoneEntered.Subscribe(signal => {
                    if (!this.isAiMode) {
                        return;
                    }
                    OnGoodZoneEntered();
                });
            } else {
                OnAiDisabled();
                CancelDirection();
                aiSubscription?.Dispose();
                aiSubscription = null;
            }
        }

        protected void EmitDirection(Vector2 direction) {
            onDirection.OnNext(direction);
        }

        protected void CancelDirection() {
            onDirectionCanceled.OnNext(Unit.Default);
        }

        protected void Press(GamePadButton button) {
            onButtonDown.OnNext(button);
            onButtonUp.OnNext(button);
        }

        protected virtual void OnGoodZoneEntered() { }
        protected virtual void OnAiEnabled() { }
        protected virtual void OnAiDisabled() { }

        void OnDestroy() {
            aiSubscription?.Dispose();
            onDirection.Dispose();
            onDirectionCanceled.Dispose();
            onButtonDown.Dispose();
            onButtonUp.Dispose();
        }
    }
}