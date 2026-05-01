


using System.Collections.Generic;
using App;
using R3;
using System;
using UnityEngine;

namespace Alice {
    public record PlayerGamePadButtonEvent(int PlayerId, GamePadButton Button);

    public interface IGamePad {
        Observable<Vector2> OnDirectionAsObservable { get; }
        Observable<Unit> OnDirectionCanceledAsObservable { get; }
        Observable<GamePadButton> OnButtonDownAsObservable { get; }
        Observable<GamePadButton> OnButtonUpAsObservable { get; }
        string DeviceName { get; }
        void DestroyGamePad();
    }

    public interface IGamePadRegistry {
        IPlayerGamePad RequestRegister(IGamePad gamePad);
        IPlayerGamePad RequestRegister(int playerId, IGamePad gamePad);
        IPlayerGamePad RequestRegisterLowPriority(int playerId, IGamePad gamePad);
        void RequestUnregister(int playerId);
        void RequestUnregister(IGamePad gamePad);
        void RestoreOfflinePrimaryLayout(int localOnlinePlayerId);
        IPlayerGamePad Get(int playerId);
        void HandlePlayerSlotClick(int sourcePlayerId, int targetPlayerId);
        void RotateFaceButtonWiringClockwise();
        Observable<PlayerGamePadButtonEvent> OnAnyButtonDown { get; }
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
        readonly Subject<PlayerGamePadButtonEvent> onAnyButtonDown = new();
        int faceButtonRotationOffset;

        public Observable<PlayerGamePadButtonEvent> OnAnyButtonDown => onAnyButtonDown;

        public IPlayerGamePad RequestRegister(IGamePad gamePad) {
            for (int i = 0; i < registry.Count; i++) {
                if (!registry[i].HasPrimaryGamePad) {
                    registry[i].SetPrimary(gamePad.ToOption());

                    Debug.Log($"Registered GamePad {gamePad.DeviceName} to Player {i}".ToGreen());
                    return registry[i];
                }
            }

            var playerGamePad = new PlayerGamePad(registry.Count, gamePad.ToOption(), true, HandleButtonDown, HandleButtonUp);
            registry.Add(playerGamePad);

            Debug.Log($"Registered GamePad {gamePad.DeviceName} to Player {playerGamePad.PlayerId}".ToGreen());
            return playerGamePad;
        }

        public IPlayerGamePad RequestRegister(int playerId, IGamePad gamePad) {
            RequestUnregister(playerId);
            var playerGamePad = EnsurePlayerSlot(playerId);
            playerGamePad.SetPrimary(gamePad.ToOption());
            Debug.Log($"Registered GamePad {gamePad.DeviceName} to Player {playerId}".ToGreen());
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
                gamePad.DestroyGamePad();
            }
        }

        public void RequestUnregister(IGamePad gamePad) {
            var playerGamePads = registry.FindAll(p => p.Current.GetValue(null) == gamePad);
            foreach (var playerGamePad in playerGamePads) {
                Debug.Log($"Unregistered GamePad {gamePad.DeviceName} from Player {playerGamePad.PlayerId}".ToOrange());
                playerGamePad.ClearCurrent();
                gamePad.DestroyGamePad();
            }
        }

        public void RestoreOfflinePrimaryLayout(int localOnlinePlayerId) {
            var clampedLocalPlayerId = Mathf.Clamp(localOnlinePlayerId, 0, 1);
            if (clampedLocalPlayerId != 0) {
                var localSlot = EnsurePlayerSlot(clampedLocalPlayerId);
                if (localSlot.HasPrimaryGamePad && localSlot.Current.TryGetValue(out var localGamePad)) {
                    EnsurePlayerSlot(0).SetPrimary(localGamePad.ToOption());
                    localSlot.ClearPrimary();
                }
            }

            RemoveRemotePrimaryIfExists(0);
            RemoveRemotePrimaryIfExists(1);
        }

        public IPlayerGamePad Get(int playerId) {
            return EnsurePlayerSlot(playerId);
        }

        public void HandlePlayerSlotClick(int sourcePlayerId, int targetPlayerId) {
            var sourceSlot = EnsurePlayerSlot(sourcePlayerId);
            var targetSlot = EnsurePlayerSlot(targetPlayerId);

            if (!sourceSlot.HasPrimaryGamePad) {
                return;
            }

            if (sourcePlayerId == targetPlayerId) {
                RequestUnregister(sourcePlayerId);
                return;
            }

            var sourceCurrent = sourceSlot.Current;

            if (targetSlot.HasPrimaryGamePad) {
                var targetCurrent = targetSlot.Current;
                sourceSlot.SetPrimary(targetCurrent);
                targetSlot.SetPrimary(sourceCurrent);
                return;
            }

            targetSlot.SetPrimary(sourceCurrent);
            sourceSlot.ClearPrimary();
        }

        public void RotateFaceButtonWiringClockwise() {
            faceButtonRotationOffset = (faceButtonRotationOffset + 1) % 4;
            Debug.Log($"Rotated face button wiring clockwise. offset={faceButtonRotationOffset}".ToCyan());
        }

        PlayerGamePad EnsurePlayerSlot(int playerId) {
            while (registry.Count <= playerId) {
                registry.Add(new PlayerGamePad(registry.Count, null, false, HandleButtonDown, HandleButtonUp));
            }
            return registry[playerId];
        }

        void HandleButtonDown(int playerId, GamePadButton button) {
            var player = EnsurePlayerSlot(playerId);
            var mappedButton = MapFaceButton(button);
            onAnyButtonDown.OnNext(new PlayerGamePadButtonEvent(playerId, mappedButton));
            player.EmitButtonDown(mappedButton);
        }

        void RemoveRemotePrimaryIfExists(int playerId) {
            var slot = EnsurePlayerSlot(playerId);
            if (!slot.HasPrimaryGamePad) {
                return;
            }

            if (!slot.Current.TryGetValue(out var gamePad) || gamePad is not RemoteGamePad) {
                return;
            }

            RequestUnregister(gamePad);
        }

        void HandleButtonUp(int playerId, GamePadButton button) {
            var player = EnsurePlayerSlot(playerId);
            player.EmitButtonUp(MapFaceButton(button));
        }

        GamePadButton MapFaceButton(GamePadButton button) {
            if (faceButtonRotationOffset == 0) {
                return button;
            }

            return button switch {
                GamePadButton.North => RotateFaceButton(GamePadButton.North),
                GamePadButton.East => RotateFaceButton(GamePadButton.East),
                GamePadButton.South => RotateFaceButton(GamePadButton.South),
                GamePadButton.West => RotateFaceButton(GamePadButton.West),
                _ => button,
            };
        }

        GamePadButton RotateFaceButton(GamePadButton button) {
            var index = button switch {
                GamePadButton.North => 0,
                GamePadButton.East => 1,
                GamePadButton.South => 2,
                GamePadButton.West => 3,
                _ => 0,
            };

            var rotatedIndex = (index + faceButtonRotationOffset) % 4;
            return rotatedIndex switch {
                0 => GamePadButton.North,
                1 => GamePadButton.East,
                2 => GamePadButton.South,
                _ => GamePadButton.West,
            };
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
            readonly Action<int, GamePadButton> buttonDownHandler;
            readonly Action<int, GamePadButton> buttonUpHandler;

            public PlayerGamePad(
                int playerId,
                Option<IGamePad> current,
                bool isPrimaryInput,
                Action<int, GamePadButton> buttonDownHandler,
                Action<int, GamePadButton> buttonUpHandler) {
                PlayerId = playerId;
                this.isPrimaryInput = isPrimaryInput;
                this.buttonDownHandler = buttonDownHandler;
                this.buttonUpHandler = buttonUpHandler;
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

            public void ClearCurrent() {
                if (isPrimaryInput) {
                    ClearPrimary();
                    return;
                }

                Current = null;
            }

            public void EmitButtonDown(GamePadButton button) {
                onButtonDown.OnNext(button);
            }

            public void EmitButtonUp(GamePadButton button) {
                onButtonUp.OnNext(button);
            }

            void SwitchCurrent() {
                directionSubscription?.Dispose();
                directionCanceledSubscription?.Dispose();
                buttonDownSubscription?.Dispose();
                buttonUpSubscription?.Dispose();

                if (current.TryGetValue(out var activeGamePad)) {
                    directionSubscription = activeGamePad.OnDirectionAsObservable.Subscribe(onDirection.OnNext);
                    directionCanceledSubscription = activeGamePad.OnDirectionCanceledAsObservable.Subscribe(onDirectionCanceled.OnNext);
                    buttonDownSubscription = activeGamePad.OnButtonDownAsObservable.Subscribe(button => buttonDownHandler(PlayerId, button));
                    buttonUpSubscription = activeGamePad.OnButtonUpAsObservable.Subscribe(button => buttonUpHandler(PlayerId, button));
                }
            }
        }
    }
}