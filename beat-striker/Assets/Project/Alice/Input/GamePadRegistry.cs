


using System.Collections.Generic;
using App;
using R3;
using System;
using UnityEngine;

namespace Alice {
    public interface IGamePad {
        Observable<Vector2> OnDirectionAsObservable { get; }
        Observable<Unit> OnDirectionCanceledAsObservable { get; }
        Observable<GamePadButton> OnButtonDownAsObservable { get; }
        Observable<GamePadButton> OnButtonUpAsObservable { get; }
        string DeviceName { get; }
    }

    public interface IGamePadRegistry {
        IPlayerGamePad RequestRegister(IGamePad gamePad);
        IPlayerGamePad RequestRegisterLowPriority(int playerId, IGamePad gamePad);
        void RequestUnregister(int playerId);
        void RequestUnregister(IGamePad gamePad);
        IPlayerGamePad Get(int playerId);
    }

    public enum GamePadButton {
        North,
        South,
        West,
        East,
        Right,
        Left,
    }

    public interface IPlayerGamePad {
        public int PlayerId { get; }
        public ReadOnlyReactiveProperty<bool> HasGamePad { get; }
        public Observable<Vector2> OnDirection { get; }
        public Observable<Unit> OnDirectionCanceled { get; }
        public Observable<GamePadButton> OnButtonDown { get; }
        public Observable<GamePadButton> OnButtonUp { get; }
    }

    public class GamePadRegistry : IGamePadRegistry {
        readonly List<PlayerGamePad> registry = new();

        public IPlayerGamePad RequestRegister(IGamePad gamePad) {
            for (int i = 0; i < registry.Count; i++) {
                if (!registry[i].HasPrimaryGamePad) {
                    registry[i].SetPrimary(gamePad.ToOption());

                    Debug.Log($"Registered GamePad {gamePad.DeviceName} to Player {i}".ToGreen());
                    return registry[i];
                }
            }

            var playerGamePad = new PlayerGamePad(registry.Count, gamePad.ToOption(), true);
            registry.Add(playerGamePad);

            Debug.Log($"Registered GamePad {gamePad.DeviceName} to Player {playerGamePad.PlayerId}".ToGreen());
            return playerGamePad;
        }

        public IPlayerGamePad RequestRegisterLowPriority(int playerId, IGamePad gamePad) {
            var playerGamePad = EnsurePlayerSlot(playerId);
            if (!playerGamePad.HasPrimaryGamePad) {
                playerGamePad.SetLowPriority(gamePad.ToOption());
                Debug.Log($"Registered LowPriority GamePad {gamePad.DeviceName} to Player {playerGamePad.PlayerId}".ToCyan());
            }
            return playerGamePad;
        }

        public void RequestUnregister(int playerId) {
            var playerGamePad = registry.Find(p => p.PlayerId == playerId);
            if(playerGamePad != null && playerGamePad.Current.TryGetValue(out var gamePad) && playerGamePad.HasPrimaryGamePad) {
                Debug.Log($"Unregistered GamePad {gamePad.DeviceName} from Player {playerId}".ToOrange());
                playerGamePad.ClearPrimary();
            }
        }

        public void RequestUnregister(IGamePad gamePad) {
            var playerGamePads = registry.FindAll(p => p.HasPrimaryGamePad && p.Current.GetValue(null) == gamePad);
            foreach (var playerGamePad in playerGamePads) {
                Debug.Log($"Unregistered GamePad {gamePad.DeviceName} from Player {playerGamePad.PlayerId}".ToOrange());
                playerGamePad.ClearPrimary();
            }
        }

        public IPlayerGamePad Get(int playerId) {
            return EnsurePlayerSlot(playerId);
        }

        PlayerGamePad EnsurePlayerSlot(int playerId) {
            while (registry.Count <= playerId) {
                registry.Add(new PlayerGamePad(registry.Count, null, false));
            }
            return registry[playerId];
        }

        class PlayerGamePad : IPlayerGamePad {
            public int PlayerId { get; set; }
            public Option<IGamePad> Current {
                get => current;
                private set {
                    current = value;
                    SwitchCurrent();
                }
            }

            public bool HasPrimaryGamePad => isPrimaryInput;

            public ReadOnlyReactiveProperty<bool> HasGamePad => hasGamePad;
            public Observable<Vector2> OnDirection => onDirection;
            public Observable<Unit> OnDirectionCanceled => onDirectionCanceled;
            public Observable<GamePadButton> OnButtonDown => onButtonDown;
            public Observable<GamePadButton> OnButtonUp => onButtonUp;

            Option<IGamePad> current;
            bool isPrimaryInput;
            readonly Subject<Vector2> onDirection = new();
            readonly Subject<Unit> onDirectionCanceled = new();
            readonly Subject<GamePadButton> onButtonDown = new();
            readonly Subject<GamePadButton> onButtonUp = new();
            readonly ReactiveProperty<bool> hasGamePad = new(false);
            IDisposable directionSubscription;
            IDisposable directionCanceledSubscription;
            IDisposable buttonDownSubscription;
            IDisposable buttonUpSubscription;

            public PlayerGamePad(int playerId, Option<IGamePad> current, bool isPrimaryInput) {
                PlayerId = playerId;
                this.isPrimaryInput = isPrimaryInput;
                hasGamePad.OnNext(this.isPrimaryInput && current.TryGetValue(out _));
                Current = current;
            }

            public void SetPrimary(Option<IGamePad> gamePad) {
                isPrimaryInput = gamePad.TryGetValue(out _);
                hasGamePad.OnNext(isPrimaryInput);
                Current = gamePad;
            }

            public void SetLowPriority(Option<IGamePad> gamePad) {
                if (isPrimaryInput) {
                    return;
                }
                Current = gamePad;
            }

            public void ClearPrimary() {
                if (!isPrimaryInput) {
                    return;
                }
                isPrimaryInput = false;
                hasGamePad.OnNext(false);
                Current = null;
            }

            void SwitchCurrent() {
                directionSubscription?.Dispose();
                directionCanceledSubscription?.Dispose();
                buttonDownSubscription?.Dispose();
                buttonUpSubscription?.Dispose();

                if (current.TryGetValue(out var activeGamePad)) {
                    directionSubscription = activeGamePad.OnDirectionAsObservable.Subscribe(onDirection.OnNext);
                    directionCanceledSubscription = activeGamePad.OnDirectionCanceledAsObservable.Subscribe(onDirectionCanceled.OnNext);
                    buttonDownSubscription = activeGamePad.OnButtonDownAsObservable.Subscribe(onButtonDown.OnNext);
                    buttonUpSubscription = activeGamePad.OnButtonUpAsObservable.Subscribe(onButtonUp.OnNext);
                }
            }
        }
    }
}