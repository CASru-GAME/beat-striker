
using System;
 
using R3;
using UnityEngine;

namespace Alice {
    public enum BeatJudgeZone {
        Good,
        Miss,
    }

    public interface IMusicPlayer {
        void Play();
        void Stop();
        Observable<BeatSignal> OnGoodZoneEntered { get; }
        Observable<BeatSignal> OnBeatTiming { get; }
        Observable<float[]> OnBeatTimelinePrepared { get; }
        Observable<float> OnViewPlaybackTimeChanged { get; }
        BeatJudgeResult JudgeTiming(float playbackTime);
        float CurrentPlaybackTime { get; }
        float CurrentViewPlaybackTime { get; }
        float[] CurrentBeatTimeline { get; }

        public record BeatSignal(int BeatIndex, float BeatTime);
        public record BeatJudgeResult(BeatJudgeZone Zone, int BeatIndex, float BeatTime);
    }

    public class MusicPlayer : IMusicPlayer, IDisposable {
        readonly AudioSource audioSource;
        readonly IAudioSetting audioSetting;
        readonly IBattleSelectSetting battleSelectSetting;
        readonly Subject<IMusicPlayer.BeatSignal> onGoodZoneEntered = new();
        readonly Subject<IMusicPlayer.BeatSignal> onBeatTiming = new();
        readonly Subject<float[]> onBeatTimelinePrepared = new();
        readonly Subject<float> onViewPlaybackTimeChanged = new();
        IDisposable beatSoundSubscription;
        float[] beats = Array.Empty<float>();
        int beatSoundIndex;
        int goodWindowIndex;
        int beatTimingIndex;
        float lastPlaybackTime;
        float currentViewPlaybackTime;

        public Observable<IMusicPlayer.BeatSignal> OnGoodZoneEntered => onGoodZoneEntered;
        public Observable<IMusicPlayer.BeatSignal> OnBeatTiming => onBeatTiming;
        public Observable<float[]> OnBeatTimelinePrepared => onBeatTimelinePrepared;
        public Observable<float> OnViewPlaybackTimeChanged => onViewPlaybackTimeChanged;
        public float CurrentPlaybackTime => audioSource.time;
        public float CurrentViewPlaybackTime => currentViewPlaybackTime;
        public float[] CurrentBeatTimeline => beats;

        public MusicPlayer(AudioSource audioSource, IAudioSetting audioSetting, IBattleSelectSetting battleSelectSetting) {
            this.audioSource = audioSource;
            this.audioSetting = audioSetting;
            this.battleSelectSetting = battleSelectSetting;
        }

        public void Play() {
            var selectedTrack = audioSetting.GetTrack(battleSelectSetting.SelectedMusicId.CurrentValue);
            var clip = selectedTrack.AudioClip;
            audioSource.clip = clip;
            audioSource.Play();

            beatSoundSubscription?.Dispose();
            beats = selectedTrack.beats;
            onBeatTimelinePrepared.OnNext(beats);
            beatSoundIndex = 0;
            goodWindowIndex = 0;
            beatTimingIndex = 0;
            lastPlaybackTime = -1f;
            
            beatSoundSubscription = Observable.EveryUpdate().Subscribe(_ => {
                if (!audioSource.isPlaying) return;
                var currentTime = audioSource.time;
                currentViewPlaybackTime = currentTime + audioSetting.ViewTimeOffset.CurrentValue;
                onViewPlaybackTimeChanged.OnNext(currentViewPlaybackTime);
                if (lastPlaybackTime >= 0f && currentTime < lastPlaybackTime) {
                    beatSoundIndex = 0;
                    goodWindowIndex = 0;
                    beatTimingIndex = 0;
                }

                EmitGoodZoneEvents();
                EmitBeatTimingEvents();

                while (beatSoundIndex < beats.Length && audioSource.time >= beats[beatSoundIndex]) {
                    AudioSource.PlayClipAtPoint(selectedTrack.beatSound, Vector3.zero);
                    beatSoundIndex += 1;
                }

                lastPlaybackTime = currentTime;
            });
        }

        public void Stop() {
            beatSoundSubscription?.Dispose();
            beatSoundSubscription = null;
            audioSource.Stop();
        }

        public IMusicPlayer.BeatJudgeResult JudgeTiming(float playbackTime) {
            var judgeTime = playbackTime + audioSetting.CommandTimeOffset.CurrentValue;
            

            for (var i = 0; i < beats.Length; i++) {
                var beatTime = beats[i];
                if (judgeTime > beatTime) {
                    continue;
                }

                var windowStart = beatTime - audioSetting.PerfectWindow.CurrentValue;
                if (judgeTime >= windowStart) {
                    return new IMusicPlayer.BeatJudgeResult(BeatJudgeZone.Good, i, beatTime);
                }
                
                return new IMusicPlayer.BeatJudgeResult(BeatJudgeZone.Miss, -1, 0f);
            }

            
            return new IMusicPlayer.BeatJudgeResult(BeatJudgeZone.Miss, -1, 0f);
        }

        void EmitGoodZoneEvents() {
            var judgeTime = audioSource.time + audioSetting.CommandTimeOffset.CurrentValue;
            while (goodWindowIndex < beats.Length) {
                var beatTime = beats[goodWindowIndex];
                var windowStart = beatTime - audioSetting.PerfectWindow.CurrentValue;
                if (judgeTime < windowStart) {
                    return;
                }
                onGoodZoneEntered.OnNext(new IMusicPlayer.BeatSignal(goodWindowIndex, beatTime));
                goodWindowIndex += 1;
            }
        }

        void EmitBeatTimingEvents() {
            while (beatTimingIndex < beats.Length && audioSource.time >= beats[beatTimingIndex]) {
                onBeatTiming.OnNext(new IMusicPlayer.BeatSignal(beatTimingIndex, beats[beatTimingIndex]));
                beatTimingIndex += 1;
            }
        }

        public void Dispose() {
            beatSoundSubscription?.Dispose();
            onGoodZoneEntered.Dispose();
            onBeatTiming.Dispose();
            onBeatTimelinePrepared.Dispose();
            onViewPlaybackTimeChanged.Dispose();
        }

        
    }
}