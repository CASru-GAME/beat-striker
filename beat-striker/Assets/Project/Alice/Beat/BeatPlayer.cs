
using System;
using R3;
using UnityEngine;

namespace Alice {
    public interface IMusicPlayer {
        void Play();
    }

    public class MusicPlayer : IMusicPlayer, IDisposable {
        readonly AudioSource audioSource;
        readonly BeatConfig beatConfig;
        IDisposable beatSoundSubscription;

        public MusicPlayer(AudioSource audioSource, BeatConfig beatConfig) {
            this.audioSource = audioSource;
            this.beatConfig = beatConfig;
        }

        public void Play() {
            var selectedTrack = beatConfig.SelectedTrack;
            var clip = selectedTrack.AudioClip;
            audioSource.clip = clip;
            audioSource.Play();

            beatSoundSubscription?.Dispose();
            var beats = selectedTrack.beats;
            var beatIndex = 0;
            beatSoundSubscription = Observable.EveryUpdate().Subscribe(_ => {
                if (!audioSource.isPlaying) return;
                if (beatIndex >= beats.Length) return;

                if (audioSource.time < beats[beatIndex]) return;

                AudioSource.PlayClipAtPoint(selectedTrack.beatSound, Vector3.zero);
                beatIndex += 1;
            });
        }

        public void Dispose() {
            beatSoundSubscription?.Dispose();
        }
    }
}