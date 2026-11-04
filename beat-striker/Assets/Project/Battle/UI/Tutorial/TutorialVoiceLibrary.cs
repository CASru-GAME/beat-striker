using System;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Alice {
    [Serializable]
    public record TutorialVoiceClip {
        public string Id;
        public AssetReferenceT<AudioClip> Audio;
    }

    [Serializable]
    public record TutorialVoiceLine {
        public TutorialUiKey Key;
        public string[] Segments;
    }

    public record TutorialVoiceInput(string East, string South, string West, string North,
        string Start, string Walk, string Direction, string RhythmButtons, string DirectionHoldLabel) {
        public string Resolve(string segment) => segment switch {
            "$East" => East,
            "$South" => South,
            "$West" => West,
            "$North" => North,
            "$Start" => Start,
            "$Walk" => Walk,
            "$Direction" => Direction,
            "$DirectionHold" => Direction == "direction_keyboard" ? "direction_hold_keyboard" : "direction_hold_stick",
            _ => segment,
        };
    }

    [CreateAssetMenu(menuName = "Battle/Tutorial Voice Library")]
    public sealed class TutorialVoiceLibrary : ScriptableObject {
        [SerializeField] TutorialVoiceClip[] clips;
        [SerializeField] TutorialVoiceLine[] lines;

        public string[] GetSegments(TutorialUiKey key) {
            foreach (var line in lines) {
                if (line.Key == key) return line.Segments;
            }
            throw new ArgumentOutOfRangeException(nameof(key), key, "Tutorial narration is missing.");
        }

        public AssetReferenceT<AudioClip> GetClip(string id) {
            foreach (var clip in clips) {
                if (clip.Id == id) return clip.Audio;
            }
            throw new ArgumentException($"Tutorial voice clip is missing: {id}", nameof(id));
        }
    }
}
