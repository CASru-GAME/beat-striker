


using System.Collections.Generic;
using App;
using R3;
using System;
using UnityEngine;

namespace Alice {
    public interface IGamePadRegistry {
        IPlayerGamePad RequestRegister(GamePad gamePad);
        void RequestUnregister(int playerId);
        void RequestUnregister(GamePad gamePad);
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
        public Observable<Vector2> OnDirection { get; }
        public Observable<GamePadButton> OnButtonDown { get; }
        public Observable<GamePadButton> OnButtonUp { get; }
    }

    public class GamePadRegistry : IGamePadRegistry {
        readonly List<PlayerGamePad> registry = new();

        public IPlayerGamePad RequestRegister(GamePad gamePad) {
            for (int i = 0; i < registry.Count; i++) {
                if (!registry[i].Current.TryGetValue(out _)) {
                    registry[i].Current = gamePad;

                    Debug.Log($"Registered GamePad {gamePad.DeviceName} to Player {i}".ToGreen());
                    return registry[i];
                }
            }

            var playerGamePad = new PlayerGamePad(registry.Count, gamePad);
            registry.Add(playerGamePad);

            Debug.Log($"Registered GamePad {gamePad.DeviceName} to Player {playerGamePad.PlayerId}".ToGreen());
            return playerGamePad;
        }

        public void RequestUnregister(int playerId) {
            var playerGamePad = registry.Find(p => p.PlayerId == playerId);
            if(playerGamePad != null && playerGamePad.Current.TryGetValue(out var gamePad)) {
                Debug.Log($"Unregistered GamePad {gamePad.DeviceName} from Player {playerId}".ToGreen());
                playerGamePad.Current = null;
            }
        }

        public void RequestUnregister(GamePad gamePad) {
            var playerGamePads = registry.FindAll(p => p.Current == gamePad);
            foreach (var playerGamePad in playerGamePads) {
                Debug.Log($"Unregistered GamePad {gamePad.DeviceName} from Player {playerGamePad.PlayerId}".ToGreen());
                playerGamePad.Current = null;
            }
        }

        public IPlayerGamePad Get(int playerId) {
            return EnsurePlayerSlot(playerId);
        }

        PlayerGamePad EnsurePlayerSlot(int playerId) {
            while (registry.Count <= playerId) {
                registry.Add(new PlayerGamePad(registry.Count, null));
            }
            return registry[playerId];
        }

        class PlayerGamePad : IPlayerGamePad {
            public int PlayerId { get; set; }
            public Option<GamePad> Current {
                get => current;
                set {
                    current = value;
                    SwitchCurrent();
                }
            }

            public Observable<Vector2> OnDirection => onDirection;
            public Observable<GamePadButton> OnButtonDown => onButtonDown;
            public Observable<GamePadButton> OnButtonUp => onButtonUp;

            Option<GamePad> current;
            readonly Subject<Vector2> onDirection = new();
            readonly Subject<GamePadButton> onButtonDown = new();
            readonly Subject<GamePadButton> onButtonUp = new();
            IDisposable directionSubscription;
            IDisposable buttonDownSubscription;
            IDisposable buttonUpSubscription;

            public PlayerGamePad(int playerId, Option<GamePad> current) {
                PlayerId = playerId;
                Current = current;
            }

            void SwitchCurrent() {
                directionSubscription?.Dispose();
                buttonDownSubscription?.Dispose();
                buttonUpSubscription?.Dispose();

                if (current.TryGetValue(out var gamePad)) {
                    directionSubscription = gamePad.OnDirectionAsObservable.Subscribe(onDirection.OnNext);
                    buttonDownSubscription = gamePad.OnButtonDownAsObservable.Subscribe(onButtonDown.OnNext);
                    buttonUpSubscription = gamePad.OnButtonUpAsObservable.Subscribe(onButtonUp.OnNext);
                }
            }
        }
    }
}