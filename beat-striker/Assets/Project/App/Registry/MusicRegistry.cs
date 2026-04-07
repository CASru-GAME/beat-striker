using System.Collections.Generic;
using UnityEngine;

namespace Alice {
    [System.Serializable]
    public class AppMusicEntry {
        public string Id;
        public string DisplayName;
        [TextArea]
        public string Description;
        public AudioClip AudioClip;
        public TextAsset SpectrumData;
        public float Bpm = 120f;
        public float Offset;
    }

    public record MusicInfo(string Id, string DisplayName, string Description, AudioClip AudioClip, TextAsset SpectrumData, float Bpm, float Offset);

    public interface IMusicRegistry {
        MusicInfo Default { get; }
        MusicInfo GetById(string id);
        IReadOnlyList<MusicInfo> GetAll();
    }

    public class MusicRegistry : MonoBehaviour, IMusicRegistry {
        [SerializeField] AppMusicEntry[] musicEntries;

        readonly Dictionary<string, MusicInfo> musicById = new Dictionary<string, MusicInfo>();
        readonly List<MusicInfo> allMusic = new List<MusicInfo>();

        bool isInitialized;
        MusicInfo defaultMusic;

        public MusicInfo Default {
            get {
                EnsureInitialized();
                return defaultMusic;
            }
        }

        public MusicInfo GetById(string id) {
            EnsureInitialized();
            return musicById[id];
        }

        public IReadOnlyList<MusicInfo> GetAll() {
            EnsureInitialized();
            return allMusic;
        }

        void EnsureInitialized() {
            if (isInitialized) {
                return;
            }

            musicById.Clear();
            allMusic.Clear();
            foreach (var entry in musicEntries) {
                var music = new MusicInfo(entry.Id, entry.DisplayName, entry.Description, entry.AudioClip, entry.SpectrumData, entry.Bpm, entry.Offset);
                musicById[music.Id] = music;
                allMusic.Add(music);
            }

            defaultMusic = allMusic[0];
            isInitialized = true;
        }
    }
}
