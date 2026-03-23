
using UnityEngine;

namespace Alice {
    public interface IMusicPlayer {
        void Play();
    }

    public class MusicPlayer : IMusicPlayer {
        readonly AudioSource audioSource;
        readonly BeatConfig beatConfig;

        public MusicPlayer(AudioSource audioSource, BeatConfig beatConfig) {
            this.audioSource = audioSource;
            this.beatConfig = beatConfig;
        }

        public void Play() {
            var clip = beatConfig.SelectedTrack.AudioClip;
            audioSource.clip = clip;
            audioSource.Play();
        }
    }
}