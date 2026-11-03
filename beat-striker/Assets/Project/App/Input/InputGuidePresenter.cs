using System.Collections.Generic;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer.Unity;

namespace Alice {
    // Keeps the authored controller text as the source, so new input layouts can
    // be added without duplicating every tutorial panel and prefab override.
    public sealed class InputGuidePresenter : ITickable {
        readonly IGamePadRegistry registry;
        readonly IAppNetworkSetting networkSetting;
        readonly Dictionary<TMP_Text, Guide> guides = new();
        readonly List<TMP_Text> removed = new();
        float nextRefresh;

        sealed class Guide {
            public string Source;
            public string Displayed;
            public Image ControllerIcon;
            public bool ControllerIconEnabled;
        }

        public InputGuidePresenter(IGamePadRegistry registry, IAppNetworkSetting networkSetting) {
            this.registry = registry;
            this.networkSetting = networkSetting;
        }

        public void Tick() {
            if (Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + 0.25f;

            var deviceName = registry.Get(networkSetting.LocalOnlinePlayerId).DeviceName;
            var keyboard = deviceName == "Keybord";
            foreach (var text in UnityEngine.Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None)) {
                if (!guides.TryGetValue(text, out var guide)) {
                    var converted = FormatKeyboard(text.text);
                    if (converted == text.text) continue;
                    guide = new Guide { Source = text.text, Displayed = text.text };
                    if (guide.Source.Contains("セレクトボタン") && text.transform.parent != null) {
                        foreach (var image in text.transform.parent.GetComponentsInChildren<Image>(true)) {
                            if (image.sprite != null && image.sprite.name == "Select") {
                                guide.ControllerIcon = image;
                                guide.ControllerIconEnabled = image.enabled;
                                break;
                            }
                        }
                    }
                    guides.Add(text, guide);
                }
                else if (text.text != guide.Displayed) {
                    // Respect text changed by another presenter while the guide is visible.
                    guide.Source = text.text;
                }

                var display = keyboard ? FormatKeyboard(guide.Source) : guide.Source;
                if (text.text != display) text.text = display;
                guide.Displayed = display;
                if (guide.ControllerIcon != null) {
                    guide.ControllerIcon.enabled = !keyboard && guide.ControllerIconEnabled;
                }
            }

            removed.Clear();
            foreach (var entry in guides) {
                if (entry.Key == null) removed.Add(entry.Key);
            }
            foreach (var text in removed) guides.Remove(text);
        }

        string Key(GamePadButton button) {
            return registry.GetPhysicalFaceButton(button) switch {
                GamePadButton.North => "Iキー",
                GamePadButton.West => "Jキー",
                GamePadButton.South => "Kキー",
                GamePadButton.East => "Lキー",
                _ => "",
            };
        }

        string FormatKeyboard(string source) {
            if (string.IsNullOrEmpty(source)) return source;
            // The boot screen already describes both layouts; leave it intact.
            if (source.Contains("W/A/S/D")) return source;

            var result = source;
            result = ReplaceButton(result, "East", "A", Key(GamePadButton.East));
            result = ReplaceButton(result, "South", "B", Key(GamePadButton.South));
            result = ReplaceButton(result, "West", "Y", Key(GamePadButton.West));
            result = ReplaceButton(result, "North", "X", Key(GamePadButton.North));
            result = Regex.Replace(result, @"<sprite\s*name=""Start"">\s*スタートボタン|スタートボタン", "Uキー");
            result = result.Replace("セレクトボタン", "Eキー");
            result = result.Replace("左スティックを傾けて", "W/A/S/Dキーで");
            result = result.Replace("左ステックを移動したい方向(左右)に傾けて", "A/Dキーで");
            result = result.Replace("進みたい方向にステックを倒しながら", "W/A/S/Dキーで進みたい方向を押しながら");
            result = result.Replace("ステックを傾けて", "W/A/S/Dキーを押しながら");
            result = Regex.Replace(result, @"(?i)Pless A", "Press " + Key(GamePadButton.East));
            return result;
        }

        static string ReplaceButton(string text, string sprite, string label, string key) {
            // Some existing tutorial overrides contain the typo <spritename=...>.
            var pattern = @"(?:<sprite\s*name=""" + sprite + @""">\s*)?" + label + "ボタン";
            return Regex.Replace(text, pattern, key);
        }
    }
}
