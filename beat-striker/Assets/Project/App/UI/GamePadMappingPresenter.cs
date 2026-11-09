using System;
using System.Collections.Generic;
using R3;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using VContainer.Unity;

namespace Alice {
    public sealed class GamePadMappingPresenter : IInitializable, ITickable, IDisposable {
        const string PressPrompt = "光っている位置の\nボタンを押してください";
        static readonly string[] PositionNames = {
            "右のボタン", "左のボタン", "下のボタン", "上のボタン", "スタート", "セレクト",
        };

        enum CapturePhase {
            WaitingForNeutral,
            Ready,
            Pressed,
            Released,
        }

        readonly GamePadMappingDialogView view;
        readonly GamePadButtonMapping mapping;
        readonly IInputFocusService focus;
        readonly IScreenRegistry screens;
        readonly ISceneTransitionService transitions;
        readonly CompositeDisposable subscriptions = new();
        readonly List<Gamepad> pendingDevices = new();
        readonly List<ButtonControl> mappedButtons = new();
        readonly List<ButtonControl> monitoredButtons = new();

        Gamepad device;
        ButtonControl pendingButton;
        CapturePhase phase;
        double? neutralSince;

        internal bool HasPendingMapping => device != null || pendingDevices.Count > 0;

        public GamePadMappingPresenter(GamePadMappingDialogView view, GamePadButtonMapping mapping,
            IInputFocusService focus, IScreenRegistry screens, ISceneTransitionService transitions) {
            this.view = view;
            this.mapping = mapping;
            this.focus = focus;
            this.screens = screens;
            this.transitions = transitions;
        }

        public void Initialize() {
            focus.RegisterModal(view.transform, priority: 3).AddTo(subscriptions);
            view.SkipRequested.Subscribe(_ => Skip()).AddTo(subscriptions);
            transitions.TransitioningChanged.Where(active => active).Subscribe(_ => Suspend()).AddTo(subscriptions);
            InputSystem.onDeviceChange += OnDeviceChange;
            InputSystem.onEvent += OnInputEvent;
        }

        void OnInputEvent(InputEventPtr inputEvent, InputDevice inputDevice) {
            if (device != null && inputDevice == device &&
                (inputEvent.IsA<StateEvent>() || inputEvent.IsA<DeltaStateEvent>())) {
                ObserveGesture(inputEvent);
            }
        }

        internal void Enqueue(Gamepad gamepad) {
            if (device == gamepad || pendingDevices.Contains(gamepad)) return;
            pendingDevices.Add(gamepad);
            if (CanShowDialog()) mapping.BlocksGamepadButtons = true;
        }

        internal void ClearPending() {
            pendingDevices.Clear();
            Close();
            mapping.BlocksGamepadButtons = false;
        }

        void OnDeviceChange(InputDevice inputDevice, InputDeviceChange change) {
            if (inputDevice is not Gamepad gamepad ||
                change is not (InputDeviceChange.Removed or InputDeviceChange.Disconnected)) return;
            pendingDevices.Remove(gamepad);
            mapping.ForgetConnection(gamepad);
            if (device == gamepad) Close();
        }

        public void Tick() {
            if (device == null && pendingDevices.Count == 0) {
                mapping.BlocksGamepadButtons = false;
                return;
            }
            if (!CanShowDialog()) {
                Suspend();
                return;
            }
            mapping.BlocksGamepadButtons = true;
            if (device == null) {
                OpenNext();
                return;
            }
            if (!focus.CanReceive(view.transform)) {
                WaitForNeutral();
                return;
            }
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) {
                Skip();
                return;
            }
            if (phase == CapturePhase.Pressed && !pendingButton.isPressed) WaitForNeutral();
            if (phase is not (CapturePhase.WaitingForNeutral or CapturePhase.Released)) return;
            foreach (var button in monitoredButtons) {
                if (button.isPressed) {
                    neutralSince = null;
                    return;
                }
            }
            neutralSince ??= Time.unscaledTimeAsDouble;
            if (Time.unscaledTimeAsDouble - neutralSince.Value < view.NeutralDuration) return;

            if (phase == CapturePhase.WaitingForNeutral) {
                phase = CapturePhase.Ready;
                view.SetStatus(PressPrompt);
                return;
            }

            mappedButtons.Add(pendingButton);
            pendingButton = null;
            if (mappedButtons.Count == GamePadButtonMapping.Positions.Count) {
                mapping.Save(device, mappedButtons);
                Close();
            }
            else {
                phase = CapturePhase.Ready;
                ShowStep();
            }
        }

        bool CanShowDialog() => !transitions.IsTransitioning
            && screens.TryGetBySceneName(SceneManager.GetActiveScene().name, out var screen)
            && screen.Scene is not (AppScene.Boot or AppScene.Live or AppScene.Street);

        void OpenNext() {
            device = pendingDevices[0];
            pendingDevices.RemoveAt(0);
            foreach (var position in GamePadButtonMapping.Positions) monitoredButtons.Add(device[position]);
            monitoredButtons.AddRange(new[] {
                device.leftShoulder, device.rightShoulder, device.leftTrigger, device.rightTrigger,
                device.leftStickButton, device.rightStickButton,
                device.dpad.up, device.dpad.down, device.dpad.left, device.dpad.right,
            });
            var index = Gamepad.all.IndexOf(gamepad => gamepad == device);
            view.SetDevice($"GAMEPAD {index + 1:00}  /  {device.displayName}");
            ShowStep();
            WaitForNeutral();
            view.SetVisible(true);
        }

        void ObserveGesture(InputEventPtr inputEvent) {
            // Read each event before it is applied so short chords and rolling presses
            // are still visible when Unity processes several reports in one frame.
            ButtonControl pressed = null;
            for (var i = 0; i < monitoredButtons.Count; i++) {
                var button = monitoredButtons[i];
                var isPressed = button.ReadValueFromEvent(inputEvent, out var value)
                    ? button.IsValueConsideredPressed(value)
                    : button.isPressed;
                if (!isPressed) continue;
                if (phase == CapturePhase.WaitingForNeutral) {
                    neutralSince = null;
                    return;
                }
                if (pressed != null || i >= GamePadButtonMapping.Positions.Count) {
                    WaitForNeutral("ボタンは1つずつ\n押してください");
                    return;
                }
                pressed = button;
            }
            switch (phase) {
                case CapturePhase.Ready when pressed != null:
                    if (mappedButtons.Contains(pressed)) {
                        WaitForNeutral("登録済みです\n別のボタンを押してください");
                        return;
                    }
                    pendingButton = pressed;
                    phase = CapturePhase.Pressed;
                    view.SetStatus("ボタンを離してください");
                    break;
                case CapturePhase.Pressed when pressed == null:
                    phase = CapturePhase.Released;
                    neutralSince = null;
                    break;
                case CapturePhase.Pressed when pressed != pendingButton:
                case CapturePhase.Released when pressed != null:
                    WaitForNeutral("ボタンは1つずつ\n押してください");
                    break;
            }
        }

        void WaitForNeutral(string message = "ボタンをすべて\n離してください") {
            phase = CapturePhase.WaitingForNeutral;
            neutralSince = null;
            pendingButton = null;
            view.SetStatus(message);
        }

        void ShowStep() {
            view.ShowStep(mappedButtons.Count, PositionNames[mappedButtons.Count]);
            view.SetStatus(PressPrompt);
        }

        void Skip() {
            if (device != null && focus.CanReceive(view.transform)) Close();
        }

        void Suspend() {
            mapping.BlocksGamepadButtons = false;
            if (device == null) return;
            pendingDevices.Insert(0, device);
            Close();
        }

        void Close() {
            device = null;
            pendingButton = null;
            mappedButtons.Clear();
            monitoredButtons.Clear();
            if (view) view.SetVisible(false);
        }

        public void Dispose() {
            InputSystem.onEvent -= OnInputEvent;
            InputSystem.onDeviceChange -= OnDeviceChange;
            Close();
            mapping.BlocksGamepadButtons = false;
            subscriptions.Dispose();
        }
    }
}
