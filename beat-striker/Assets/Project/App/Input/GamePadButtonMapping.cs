using System;
using System.Collections.Generic;
using R3;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;

namespace Alice {
    public sealed class GamePadButtonMapping : IDisposable {
        readonly Subject<Gamepad> changed = new();
        readonly Dictionary<Gamepad, string[]> mappings = new();

        public bool BlocksGamepadButtons { get; set; }
        public Observable<Gamepad> Changed => changed;

        internal static IReadOnlyList<GamepadButton> Positions { get; } = Array.AsReadOnly(new[] {
            GamepadButton.East, GamepadButton.West, GamepadButton.South, GamepadButton.North,
            GamepadButton.Start, GamepadButton.Select,
        });

        public void Save(Gamepad device, IReadOnlyList<ButtonControl> controls) {
            var paths = new string[controls.Count];
            for (var i = 0; i < controls.Count; i++) {
                paths[i] = controls[i].name;
            }
            mappings[device] = paths;
            if (GetStorageKey(device) is { } key) {
                try {
                    PlayerPrefs.SetString(key, string.Join(",", paths));
                    PlayerPrefs.Save();
                }
                catch (PlayerPrefsException error) {
                    Debug.LogWarning($"[GamePadButtonMapping] {error.Message}");
                }
            }
            changed.OnNext(device);
        }

        public void ForgetConnection(Gamepad device) => mappings.Remove(device);

        public void Apply(GameInput.PlayerActions actions, Gamepad device, string bindingGroup) {
            var bindings = new[] { actions.East, actions.West, actions.South, actions.North, actions.Start, actions.Select };
            foreach (var action in bindings) {
                action.RemoveAllBindingOverrides();
            }

            if (device == null || string.IsNullOrEmpty(bindingGroup)) return;
            if (!mappings.TryGetValue(device, out var paths)) {
                var key = GetStorageKey(device);
                if (key == null) return;
                paths = PlayerPrefs.GetString(key).Split(',');
            }
            if (paths.Length != bindings.Length) return;

            var remaining = new HashSet<string>();
            foreach (var position in Positions) remaining.Add(device[position].name);
            foreach (var path in paths) {
                if (!remaining.Remove(path)) return;
            }

            for (var i = 0; i < bindings.Length; i++) {
                bindings[i].ApplyBindingOverride($"<Gamepad>/{paths[i]}", group: bindingGroup);
            }
        }

        static string GetStorageKey(Gamepad device) {
            var description = device.description;
            // The input system can reuse a disconnected device for another pad of the same model.
            if (string.IsNullOrWhiteSpace(description.serial)) return null;
            var identity = Hash128.Compute($"{device.layout}\n{description.interfaceName}\n{description.manufacturer}\n{description.product}\n{description.serial}");
            return $"Input.GamePadButtonMapping.v2.{identity}";
        }

        public void Dispose() => changed.Dispose();
    }
}
