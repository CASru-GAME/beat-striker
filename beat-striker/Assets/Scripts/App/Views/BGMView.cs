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

        void Awake() {
            audioSource = GetComponent<AudioSource>();
            audioSource.loop = true;
            
            bgmClips = new Dictionary<BGMType, AudioClip>();
            foreach (var entry in bgmEntries) {
                if (entry.clip != null) {
                    bgmClips[entry.bgmType] = entry.clip;
                }
            }
        }

        public void PlayBGM(BGMType bgmType) {
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
            audioSource.Stop();
            currentBGMType = null;
        }
    }
}
