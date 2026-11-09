using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using VContainer.Unity;

namespace Alice {
    public interface ILocalControllerConnections {
        void RequestReset();
    }

    public sealed class LocalPlayerJoinPresenter : ILocalControllerConnections, IInitializable, ITickable, IDisposable {
        static readonly string[] Options = { "1Pを上書き", "2Pを上書き", "接続しない" };

        readonly PlayerInputManager inputManager;
        readonly GamePadRegistry registry;
        readonly GamePadMappingPresenter mapping;
        readonly ChoiceDialogView view;
        readonly IInputFocusService focus;
        readonly IScreenRegistry screens;
        readonly ISceneTransitionService transitions;
        readonly InputActionAsset inputActions;
        readonly Dictionary<InputDevice, GamePad> activeDevices = new();
        readonly HashSet<InputDevice> detachedDevices = new();
        readonly HashSet<InputDevice> declinedDevices = new();
        readonly HashSet<InputDevice> waitingForRelease = new();
        readonly List<InputDevice> pendingDevices = new();

        InputDevice choosingDevice;
        ChoiceDialogPresenter dialog;
        Task<int> choice;
        bool choosingDeviceDetached;
        bool resetRequested;

        public LocalPlayerJoinPresenter(PlayerInputManager inputManager, GamePadRegistry registry,
            GamePadMappingPresenter mapping, ChoiceDialogView view, IInputFocusService focus,
            IScreenRegistry screens, ISceneTransitionService transitions) {
            this.inputManager = inputManager;
            this.registry = registry;
            this.mapping = mapping;
            this.view = view;
            this.focus = focus;
            this.screens = screens;
            this.transitions = transitions;
            inputActions = inputManager.playerPrefab.GetComponent<PlayerInput>().actions;
        }

        public void Initialize() {
            InputSystem.onEvent += OnInputEvent;
            InputSystem.onDeviceChange += OnDeviceChange;
        }

        void OnInputEvent(InputEventPtr inputEvent, InputDevice device) {
            if (resetRequested || !IsLocalJoinDevice(device) || waitingForRelease.Contains(device) ||
                declinedDevices.Contains(device) || pendingDevices.Contains(device)) return;
            if (activeDevices.TryGetValue(device, out var player) && player && !detachedDevices.Contains(device)) return;
            if (inputEvent.HasButtonPress()) pendingDevices.Add(device);
        }

        void OnDeviceChange(InputDevice device, InputDeviceChange change) {
            if (change is not (InputDeviceChange.Removed or InputDeviceChange.Disconnected)) return;
            pendingDevices.Remove(device);
            declinedDevices.Remove(device);
            waitingForRelease.Remove(device);
            if (activeDevices.ContainsKey(device)) detachedDevices.Add(device);
            if (choosingDevice == device) choosingDeviceDetached = true;
        }

        public void Tick() {
            if (resetRequested) {
                ResetConnections();
                resetRequested = false;
                return;
            }

            waitingForRelease.RemoveWhere(device => !HasPressedButton(device));
            foreach (var device in detachedDevices) {
                if (activeDevices.Remove(device, out var player) && player) registry.RequestUnregister(player);
            }
            detachedDevices.Clear();
            if (choosingDeviceDetached) CloseChoice();
            if (pendingDevices.Count == 0) return;
            if (!CanJoinHere(out var canShowDialog)) {
                CloseChoice();
                return;
            }
            if (canShowDialog && mapping.HasPendingMapping) return;

            var deviceToJoin = pendingDevices[0];
            if (choice != null) {
                if (!choice.IsCompleted) return;
                var target = choice.IsCompletedSuccessfully ? choice.Result : Options.Length - 1;
                if (target < GamePadRegistry.LocalPlayerLimit && !TryJoin(deviceToJoin, target)) return;
                if (target >= GamePadRegistry.LocalPlayerLimit) declinedDevices.Add(deviceToJoin);
                CloseChoice();
                pendingDevices.RemoveAt(0);
                return;
            }

            var freeSlot = FindFreeSlot();
            if (freeSlot >= 0) {
                if (TryJoin(deviceToJoin, freeSlot)) pendingDevices.RemoveAt(0);
                return;
            }
            if (!canShowDialog) return;

            choosingDevice = deviceToJoin;
            dialog = new ChoiceDialogPresenter(view, registry, focus, modalPriority: 3);
            choice = dialog.AskAsync("プレイヤー枠が満員です",
                $"{deviceToJoin.displayName}\n入れ替えるプレイヤー枠を選んでください", Options);
        }

        public void RequestReset() => resetRequested = true;

        void ResetConnections() {
            CloseChoice();
            mapping.ClearPending();
            pendingDevices.Clear();
            declinedDevices.Clear();
            detachedDevices.Clear();
            foreach (var player in activeDevices.Values) {
                if (player) registry.RequestUnregister(player);
            }
            activeDevices.Clear();
            for (var slot = 0; slot < GamePadRegistry.LocalPlayerLimit; slot++) {
                if (registry.GetPrimaryInput(slot) is VirtualTouchGamePad touch) registry.RequestUnregister(touch);
            }

            waitingForRelease.Clear();
            foreach (var device in InputSystem.devices) {
                if (IsLocalJoinDevice(device) && HasPressedButton(device)) waitingForRelease.Add(device);
            }
        }

        static bool IsLocalJoinDevice(InputDevice device) =>
            device is Gamepad { native: true, remote: false } or Keyboard { remote: false };

        static bool HasPressedButton(InputDevice device) {
            foreach (var control in device.allControls) {
                if (control is ButtonControl { isPressed: true }) return true;
            }
            return false;
        }

        int FindFreeSlot() {
            for (var slot = 0; slot < GamePadRegistry.LocalPlayerLimit; slot++) {
                if (registry.GetPrimaryInput(slot) == null) return slot;
            }
            return -1;
        }

        bool CanJoinHere(out bool canShowDialog) {
            canShowDialog = false;
            if (transitions.IsTransitioning || registry.HasReplayOverrides ||
                !screens.TryGetBySceneName(SceneManager.GetActiveScene().name, out var screen) ||
                screen.Scene is AppScene.Live or AppScene.Street) return false;
            for (var slot = 0; slot < GamePadRegistry.LocalPlayerLimit; slot++) {
                if (registry.GetPrimaryInput(slot) is RemoteGamePad) return false;
            }
            canShowDialog = screen.Scene != AppScene.Boot;
            return true;
        }

        // False means a previously removed Unity player is still being destroyed this frame.
        bool TryJoin(InputDevice device, int slot) {
            var scheme = InputControlScheme.FindControlSchemeForDevice(device, inputActions.controlSchemes);
            if (!device.added || scheme == null) return true;

            if (registry.GetPrimaryInput(slot) is GamePad player) {
                var previousDevice = GetDeviceForPlayer(player);
                player.SwitchDevice(scheme.Value.name, device);
                if (previousDevice != null) {
                    activeDevices.Remove(previousDevice);
                    declinedDevices.Add(previousDevice);
                }
            }
            else {
                if (inputManager.playerCount >= GamePadRegistry.LocalPlayerLimit) return false;
                var playerInput = inputManager.JoinPlayer(controlScheme: scheme.Value.name, pairWithDevice: device);
                if (!playerInput) return true;
                player = playerInput.GetComponent<GamePad>();
                player.Register(slot);
            }

            activeDevices[device] = player;
            if (device is Gamepad gamepad) mapping.Enqueue(gamepad);
            return true;
        }

        InputDevice GetDeviceForPlayer(GamePad player) {
            foreach (var pair in activeDevices) {
                if (pair.Value == player) return pair.Key;
            }
            return null;
        }

        void CloseChoice() {
            dialog?.Dispose();
            dialog = null;
            choice = null;
            choosingDevice = null;
            choosingDeviceDetached = false;
        }

        public void Dispose() {
            InputSystem.onEvent -= OnInputEvent;
            InputSystem.onDeviceChange -= OnDeviceChange;
            CloseChoice();
        }
    }
}
