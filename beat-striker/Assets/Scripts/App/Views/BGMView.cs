using UnityEngine;
using Core.App.Types;
using System.Collections.Generic;

namespace Core.App.Views {
    public interface IBGMView {
        void PlayBGM(BGMType bgmType);
        void StopBGM();
    }

    [System.Serializable]
    public struct BGMEntry {
        public BGMType bgmType;
        public AudioClip clip;
    }

    [RequireComponent(typeof(AudioSource))]
    public class BGMView : MonoBehaviour, IBGMView {
        [SerializeField] private BGMEntry[] bgmEntries;
        private AudioSource audioSource;
        private Dictionary<BGMType, AudioClip> bgmClips;
        private BGMType? currentBGMType;
        private bool initialized = false;

        void Awake() {
            EnsureInitialized();
        }

        public void PlayBGM(BGMType bgmType) {
            if (!EnsureInitialized()) return;
            if (currentBGMType == bgmType && audioSource.isPlaying) {
                return; // 同じBGMが既に再生中なら何もしない
            }

            if (!bgmClips.TryGetValue(bgmType, out var clip)) {
                Debug.LogWarning($"BGM '{bgmType}' not found in BGM entries");
                return;
            }

            audioSource.clip = clip;
            audioSource.Play();
            currentBGMType = bgmType;
        }

        public void StopBGM() {
            if (!EnsureInitialized()) return;
            audioSource.Stop();
            currentBGMType = null;
        }

        private bool EnsureInitialized() {
            if (initialized) return true;

            audioSource = GetComponent<AudioSource>();
            if (audioSource == null) {
                Debug.LogError("BGMView: AudioSource missing; cannot play BGM.");
                return false;
            }

            audioSource.loop = true;

            bgmClips ??= new Dictionary<BGMType, AudioClip>();
            bgmClips.Clear();
            foreach (var entry in bgmEntries) {
                if (entry.clip != null) {
                    bgmClips[entry.bgmType] = entry.clip;
                }
            }

            initialized = true;
            return true;
        }
    }
}
