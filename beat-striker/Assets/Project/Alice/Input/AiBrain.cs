using R3;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using VContainer;

namespace Alice {
    public abstract class AiBrain : MonoBehaviour, IGamePad {
        readonly Subject<Vector2> onDirection = new();
        readonly Subject<Unit> onDirectionCanceled = new();
        readonly Subject<GamePadButton> onButtonDown = new();
        readonly Subject<GamePadButton> onButtonUp = new();
        readonly HashSet<GamePadButton> holdingButtons = new();

        IMusicPlayer musicPlayer;
        IStrikerRegistry strikerRegistry;
        IDisposable aiSubscription;
        bool isAiMode;
        protected IReadOnlyBattleEntity SelfStriker { get; private set; }

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

        protected IEnumerable<IReadOnlyBattleEntity> GetAllStrikers() {
            return strikerRegistry.GetAllStrikers();
        }

        protected IEnumerable<IReadOnlyBattleEntity> GetOpponentStrikers() {
            var opponents = new List<IReadOnlyBattleEntity>();
            foreach (var striker in strikerRegistry.GetAllStrikers()) {
                if (striker.PlayerId == SelfStriker.PlayerId) {
                    continue;
                }
                opponents.Add(striker);
            }
            return opponents;
        }

        public void EnableAiMode(IReadOnlyBattleEntity self) {
            enabled = true;
            if (this.isAiMode) {
                return;
            }
            this.SelfStriker = self;
            this.isAiMode = true;
            OnAiEnabled();
            aiSubscription = musicPlayer.OnGoodZoneEntered.Subscribe(_ => {
                if (!this.isAiMode) {
                    return;
                }
                OnGoodZoneEntered();
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
            aiSubscription?.Dispose();
            aiSubscription = null;
            this.SelfStriker = null;
        }

        // Note: legacy compatibility methods removed — callers should use EnableAiMode/DisableAiMode directly.

        bool isEmittingDirectionFor = false;

        protected void EmitDirection(Vector2 direction) {
            if (isEmittingDirectionFor) {
                return;
            }
            onDirection.OnNext(direction);
        }

        protected void CancelDirection() {
            onDirectionCanceled.OnNext(Unit.Default);
        }

        protected void Press(GamePadButton button) {
            onButtonDown.OnNext(button);
            onButtonUp.OnNext(button);
        }

        protected void ButtonDown(GamePadButton button) {
            if (holdingButtons.Add(button)) {
                onButtonDown.OnNext(button);
            }
        }

        protected void ButtonUp(GamePadButton button) {
            if (holdingButtons.Remove(button)) {
                onButtonUp.OnNext(button);
            }
        }

        protected void PressFor(GamePadButton button, float duration) {
            if (holdingButtons.Contains(button)) {
                return;
            }
            StartCoroutine(PressForCoroutine(button, duration));
        }

        IEnumerator PressForCoroutine(GamePadButton button, float duration) {
            ButtonDown(button);
            yield return new WaitForSeconds(duration);
            ButtonUp(button);
        }

        protected void EmitDirectionFor(Vector2 direction, float duration) {
            if (isEmittingDirectionFor) {
                return;
            }
            StartCoroutine(EmitDirectionForCoroutine(direction, duration));
        }

        IEnumerator EmitDirectionForCoroutine(Vector2 direction, float duration) {
            isEmittingDirectionFor = true;
            var elapsed = 0f;
            while (elapsed < duration) {
                onDirection.OnNext(direction);
                elapsed += Time.deltaTime;
                yield return null;
            }
            isEmittingDirectionFor = false;
            onDirectionCanceled.OnNext(Unit.Default);
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