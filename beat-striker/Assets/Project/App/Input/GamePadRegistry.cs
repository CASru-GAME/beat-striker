


using System.Collections.Generic;
using App;
using R3;
using System;
using UnityEngine;
using VContainer;

namespace Alice {
    public record PlayerGamePadButtonEvent(int PlayerId, GamePadButton Button);

    // Simulation data must continue independently of local UI focus.
    public interface IGamePadDataSource { }

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
        IPlayerGamePad RequestRegisterReplay(int playerId, IGamePad gamePad);
        void RequestRegisterBattleOverride(int playerId, IGamePad gamePad);
        void ClearBattleOverride(int playerId);
        IPlayerGamePad RequestRegisterLowPriority(int playerId, IGamePad gamePad);
        void RestoreReplayOverrides();
        void RequestUnregister(int playerId);
        void RequestUnregister(IGamePad gamePad);
        void RestoreOfflinePrimaryLayout(int localOnlinePlayerId);
        IPlayerGamePad Get(int playerId);
        void HandlePlayerSlotClick(int sourcePlayerId, int targetPlayerId);
        GamePadButton GetPhysicalFaceButton(GamePadButton button);
        Observable<PlayerGamePadButtonEvent> OnAnyButtonDown { get; }
    }

    public interface IPlayerGamePad {
        public int PlayerId { get; }
        public string DeviceName { get; }
        public ReadOnlyReactiveProperty<bool> HasGamePad { get; }
        public Observable<Vector2> OnDirection { get; }
        public Observable<Vector2> OnCursorDirection { get; }
        public Observable<Unit> OnDirectionCanceled { get; }
        public Observable<GamePadButton> OnButtonDown { get; }
        public Observable<GamePadButton> OnButtonUp { get; }
    }

    public class GamePadRegistry : IGamePadRegistry, IDisposable {
        readonly IInputFocusService focus;
        readonly IDisposable focusSubscription;
        readonly List<PlayerGamePad> registry = new();
        readonly Subject<PlayerGamePadButtonEvent> onAnyButtonDown = new();
        readonly Dictionary<int, Option<IGamePad>> replayOverridePreviousPrimaryByPlayerId = new();
        readonly Dictionary<int, IGamePad> replayViewerGamePadsByPlayerId = new();

        public Observable<PlayerGamePadButtonEvent> OnAnyButtonDown => onAnyButtonDown;

        [Inject]
        public GamePadRegistry(IInputFocusService focus) {
            this.focus = focus;
            focusSubscription = focus.Changed.Subscribe(_ => { foreach (var player in registry) player.ClearLocalInput(); });
        }

        public void Dispose() { focusSubscription?.Dispose(); }

        public IPlayerGamePad RequestRegister(IGamePad gamePad) {
            for (int i = 0; i < registry.Count; i++) {
                if (!registry[i].HasPrimaryGamePad) {
                    registry[i].SetPrimary(gamePad.ToOption());

                    Debug.Log($"Registered GamePad {gamePad.DeviceName} to Player {i}".ToGreen());
                    return registry[i];
                }
            }

            var playerGamePad = new PlayerGamePad(registry.Count, gamePad.ToOption(), true, HandleButtonDown, HandleButtonUp, focus);
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

        public IPlayerGamePad RequestRegisterReplay(int playerId, IGamePad gamePad) {
            var playerGamePad = EnsurePlayerSlot(playerId);
            if (!replayOverridePreviousPrimaryByPlayerId.ContainsKey(playerId)) {
                replayOverridePreviousPrimaryByPlayerId[playerId] = playerGamePad.Current;
                RegisterReplayViewerIfNeeded(playerGamePad.Current);
            }
            playerGamePad.SetPrimary(gamePad.ToOption());
            Debug.Log($"Registered Replay GamePad {gamePad.DeviceName} to Player {playerId}".ToGreen());
            return playerGamePad;
        }

        public void RequestRegisterBattleOverride(int playerId, IGamePad gamePad) {
            EnsurePlayerSlot(playerId).SetBattleOverride(gamePad.ToOption());
        }
        public void ClearBattleOverride(int playerId) => EnsurePlayerSlot(playerId).SetBattleOverride(null);

        public void RestoreReplayOverrides() {
            foreach (var pair in replayOverridePreviousPrimaryByPlayerId) {
                var playerGamePad = EnsurePlayerSlot(pair.Key);
                playerGamePad.SetPrimary(pair.Value);
            }

            replayOverridePreviousPrimaryByPlayerId.Clear();
            foreach (var pair in replayViewerGamePadsByPlayerId) {
                var playerGamePad = EnsurePlayerSlot(pair.Key);
                if (playerGamePad.Current.GetValue(null) == pair.Value) {
                    playerGamePad.ClearCurrent();
                }
            }
            replayViewerGamePadsByPlayerId.Clear();
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

        PlayerGamePad EnsurePlayerSlot(int playerId) {
            while (registry.Count <= playerId) {
                registry.Add(new PlayerGamePad(registry.Count, null, false, HandleButtonDown, HandleButtonUp, focus));
            }
            return registry[playerId];
        }

        void RegisterReplayViewerIfNeeded(Option<IGamePad> gamePadOption) {
            if (!gamePadOption.TryGetValue(out var gamePad)) {
                return;
            }

            foreach (var pair in replayViewerGamePadsByPlayerId) {
                if (pair.Value == gamePad) {
                    return;
                }
            }

            var playerGamePad = FindReplayViewerSlot();
            playerGamePad.SetPrimary(gamePad.ToOption());
            replayViewerGamePadsByPlayerId[playerGamePad.PlayerId] = gamePad;
            Debug.Log($"Registered Replay Viewer GamePad {gamePad.DeviceName} to Player {playerGamePad.PlayerId}".ToGreen());
        }

        PlayerGamePad FindReplayViewerSlot() {
            const int firstReplayViewerPlayerId = 2;
            var playerId = firstReplayViewerPlayerId;
            while (true) {
                var playerGamePad = EnsurePlayerSlot(playerId);
                if (!playerGamePad.Current.TryGetValue(out _)) {
                    return playerGamePad;
                }
                playerId += 1;
            }
        }

        void HandleButtonDown(int playerId, GamePadButton button) {
            var player = EnsurePlayerSlot(playerId);
            if (player.IsSynthetic) { player.EmitButtonDown(button); return; }
            if (!player.PressedButtons.Add(button)) return;
            var version = focus.Version;
            if (focus.HasTextFocus && player.DeviceName == "Keybord") return;
            if (button == GamePadButton.East && focus.TryClick(playerId)) return;
            onAnyButtonDown.OnNext(new PlayerGamePadButtonEvent(playerId, button));
            if (version == focus.Version && focus.CanReceive(null)) player.EmitButtonDown(button);
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
            player.PressedButtons.Remove(button);
            player.EmitButtonUp(button);
        }

        // Guides use the fixed Switch face-button positions.
        public GamePadButton GetPhysicalFaceButton(GamePadButton button) => button;

        class PlayerGamePad : IPlayerGamePad {
            public int PlayerId { get; set; }
            public Option<IGamePad> Current {
                get => current;
                private set {
                    current = value;
                    SwitchCurrent();
                }
            }

            public HashSet<GamePadButton> PressedButtons { get; } = new();
            public bool HasPrimaryGamePad => isPrimaryInput;
            public bool IsSynthetic => (battleOverride.TryGetValue(out var injected) ? injected : current.GetValue(null)) is IGamePadDataSource;
            public string DeviceName => current.TryGetValue(out var gamePad) ? gamePad.DeviceName : null;

            public ReadOnlyReactiveProperty<bool> HasGamePad => hasGamePad;
            public Observable<Vector2> OnDirection => onDirection;
            public Observable<Vector2> OnCursorDirection => onCursorDirection;
            public Observable<Unit> OnDirectionCanceled => onDirectionCanceled;
            public Observable<GamePadButton> OnButtonDown => onButtonDown;
            public Observable<GamePadButton> OnButtonUp => onButtonUp;

            Option<IGamePad> current;
            Option<IGamePad> battleOverride;
            bool isPrimaryInput;
            readonly Subject<Vector2> onDirection = new();
            readonly Subject<Vector2> onCursorDirection = new();
            Vector2 lastDirection;
            bool directionNeedsNeutral;
            readonly HashSet<GamePadButton> deliveredButtons = new();
            readonly IInputFocusService focus;
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
                Action<int, GamePadButton> buttonUpHandler, IInputFocusService focus) {
                this.focus = focus;
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
            public void SetBattleOverride(Option<IGamePad> gamePad) { battleOverride = gamePad; SwitchCurrent(); }

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
                deliveredButtons.Add(button);
                onButtonDown.OnNext(button);
            }

            public void EmitButtonUp(GamePadButton button) {
                if (deliveredButtons.Remove(button)) onButtonUp.OnNext(button);
            }

            public void ClearLocalInput() {
                if (IsSynthetic) return;
                directionNeedsNeutral = lastDirection != Vector2.zero;
                onDirection.OnNext(Vector2.zero);
                onDirectionCanceled.OnNext(Unit.Default);
                var released = new List<GamePadButton>(deliveredButtons);
                deliveredButtons.Clear();
                foreach (var button in released) onButtonUp.OnNext(button);
            }

            void SwitchCurrent() {
                PressedButtons.Clear();
                ClearLocalInput();
                lastDirection = Vector2.zero; directionNeedsNeutral = false;
                onCursorDirection.OnNext(Vector2.zero);
                directionSubscription?.Dispose();
                directionCanceledSubscription?.Dispose();
                buttonDownSubscription?.Dispose();
                buttonUpSubscription?.Dispose();

                var effective = battleOverride.TryGetValue(out _) ? battleOverride : current;
                if (effective.TryGetValue(out var activeGamePad)) {
                    directionSubscription = activeGamePad.OnDirectionAsObservable.Subscribe(direction => {
                        lastDirection = direction;
                        onCursorDirection.OnNext(direction);
                        if (direction == Vector2.zero) directionNeedsNeutral = false;
                        if (IsSynthetic || !directionNeedsNeutral && focus.CanReceive(null)) onDirection.OnNext(direction);
                    });
                    directionCanceledSubscription = activeGamePad.OnDirectionCanceledAsObservable.Subscribe(_ => {
                        lastDirection = Vector2.zero;
                        directionNeedsNeutral = false;
                        onCursorDirection.OnNext(Vector2.zero);
                        onDirection.OnNext(Vector2.zero);
                        onDirectionCanceled.OnNext(Unit.Default);
                    });
                    buttonDownSubscription = activeGamePad.OnButtonDownAsObservable.Subscribe(button => buttonDownHandler(PlayerId, button));
                    buttonUpSubscription = activeGamePad.OnButtonUpAsObservable.Subscribe(button => buttonUpHandler(PlayerId, button));
                }
            }
        }
    }
}
