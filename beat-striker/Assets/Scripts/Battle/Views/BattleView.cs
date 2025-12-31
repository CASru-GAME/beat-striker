using Core.App.Types;
using UnityEngine;
using UnityEngine.Audio;

namespace Core.Battle {
    public class BattleView : MonoBehaviour, IBattleView {
        private AudioSource audioSource;
        private AudioSource beatAudioSource;
        private AudioClip audioClip;
        private AudioClip beatClip;
        private IRythmTrackModel rythmTrackModel;
        private float lastBeatTime = -1f;

        void Awake() {

        }

        public void Construct(AudioSource audioSource, AudioSource beatAudioSource, AudioClip audioClip, AudioClip beatClip) {
            this.audioSource = audioSource;
            this.beatAudioSource = beatAudioSource;
            this.audioClip = audioClip;
            this.beatClip = beatClip;
        }

        public void SetRythmTrackModel(IRythmTrackModel model) {
            this.rythmTrackModel = model;
            lastBeatTime = -1f;
        }

        public void SetBattleModel(IBattleModel battleModel) {
            // Subscribe to events
            battleModel.SubscribeBattleStarted(_ => PlayTrack(default));
            battleModel.SubscribeRoundFinished(_ => StopTrack());
            battleModel.SubscribeOutroStarted(_ => StopTrack());
        }

        void Update() {
            if (rythmTrackModel != null && audioSource.isPlaying) {
                // Sync model time with audio time
                rythmTrackModel.SetTime(audioSource.time);

                float nextBeatTime = rythmTrackModel.GetNextBeatTime(new PlayerId(0), 0);
                if (!float.IsNaN(nextBeatTime) && rythmTrackModel.GetTime() >= nextBeatTime && nextBeatTime != lastBeatTime) {
                    if (beatClip != null && beatAudioSource != null) {
                        beatAudioSource.PlayOneShot(beatClip);
                    }
                    lastBeatTime = nextBeatTime;
                }
            }
        }

        public void PlayTrack(TrackId trackId) {
            if (audioClip != null) {
                audioSource.clip = audioClip;
                audioSource.Play();
                lastBeatTime = -1f;
            }
        }

        public void StopTrack() {
            audioSource.Stop();
            lastBeatTime = -1f;
        }

        public bool IsPlaying() {
            return audioSource.isPlaying;
        }

        public float GetAudioTime() {
            return audioSource.time;
        }
    }
}
