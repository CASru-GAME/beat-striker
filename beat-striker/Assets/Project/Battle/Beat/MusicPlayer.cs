
using System;
 
using R3;
using UnityEngine;

namespace Alice {
    public enum BeatJudgeZone {
        Excellent,
        Good,
        Miss,
    }

    public interface IMusicPlayer {
        void Play();
        void Stop();
        void Pause();
        void Resume();
        Observable<BeatSignal> OnGoodZoneEntered { get; }
        Observable<BeatSignal> OnExcellentZoneEntered { get; }
        Observable<BeatSignal> OnBeatTiming { get; }
        Observable<BeatSignal> OnViewBeatTiming { get; }
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
        readonly IMusicRegistry musicRegistry;
        readonly IAudioSetting audioSetting;
        readonly IBattleSelectSetting battleSelectSetting;
        readonly IAISetting aiSetting;
        readonly Subject<IMusicPlayer.BeatSignal> onGoodZoneEntered = new();
        readonly Subject<IMusicPlayer.BeatSignal> onExcellentZoneEntered = new();
        readonly Subject<IMusicPlayer.BeatSignal> onBeatTiming = new();
        readonly Subject<IMusicPlayer.BeatSignal> onViewBeatTiming = new();
        readonly Subject<float[]> onBeatTimelinePrepared = new();
        readonly Subject<float> onViewPlaybackTimeChanged = new();
        IDisposable beatSoundSubscription;
        IDisposable volumeSubscription;
        float[] beats = Array.Empty<float>();
        int goodWindowIndex;
        int excellentWindowIndex;
        int beatTimingIndex;
        int viewBeatTimingIndex;
        float lastPlaybackTime;
        float currentViewPlaybackTime;
        float virtualPlaybackTime;
        bool isVirtualPlaying;

        public Observable<IMusicPlayer.BeatSignal> OnGoodZoneEntered => onGoodZoneEntered;
        public Observable<IMusicPlayer.BeatSignal> OnExcellentZoneEntered => onExcellentZoneEntered;
        public Observable<IMusicPlayer.BeatSignal> OnBeatTiming => onBeatTiming;
        public Observable<IMusicPlayer.BeatSignal> OnViewBeatTiming => onViewBeatTiming;
        public Observable<float[]> OnBeatTimelinePrepared => onBeatTimelinePrepared;
        public Observable<float> OnViewPlaybackTimeChanged => onViewPlaybackTimeChanged;
        public float CurrentPlaybackTime => aiSetting.IsLearning.CurrentValue ? virtualPlaybackTime : audioSource.time;
        public float CurrentViewPlaybackTime => currentViewPlaybackTime;
        public float[] CurrentBeatTimeline => beats;

        public MusicPlayer(AudioSource audioSource, IMusicRegistry musicRegistry, IAudioSetting audioSetting, IBattleSelectSetting battleSelectSetting, IAISetting aiSetting) {
            this.audioSource = audioSource;
            this.musicRegistry = musicRegistry;
            this.audioSetting = audioSetting;
            this.battleSelectSetting = battleSelectSetting;
            this.aiSetting = aiSetting;

            volumeSubscription = this.audioSetting.VolumeBalance.Subscribe(ApplyVolume);
        }

        public void Play() {
            var selectedMusic = musicRegistry.GetById(battleSelectSetting.SelectedMusicId.CurrentValue);
            var clip = selectedMusic.AudioClip;
            audioSource.clip = clip;
            
            if (!aiSetting.IsLearning.CurrentValue) {
                audioSource.Play();
            }

            beatSoundSubscription?.Dispose();
            beats = audioSetting.CalculateBeats(selectedMusic);
            onBeatTimelinePrepared.OnNext(beats);
            goodWindowIndex = 0;
            excellentWindowIndex = 0;
            beatTimingIndex = 0;
            viewBeatTimingIndex = 0;
            lastPlaybackTime = -1f;
            virtualPlaybackTime = 0f;
            isVirtualPlaying = true;
            
            beatSoundSubscription = Observable.EveryUpdate().Subscribe(_ => {
                if (aiSetting.IsLearning.CurrentValue) {
                    if (!isVirtualPlaying) return;
                    virtualPlaybackTime += Time.deltaTime;
                    if (clip != null && clip.length > 0f) {
                        if (audioSource.loop) {
                            while (virtualPlaybackTime >= clip.length) {
                                virtualPlaybackTime -= clip.length;
                            }
                        } else if (virtualPlaybackTime >= clip.length) {
                            virtualPlaybackTime = clip.length;
                            isVirtualPlaying = false;
                        }
                    }
                } else {
                    if (!audioSource.isPlaying) return;
                }

                var currentTime = CurrentPlaybackTime;
                currentViewPlaybackTime = currentTime + audioSetting.ViewTimeOffset.CurrentValue;
                onViewPlaybackTimeChanged.OnNext(currentViewPlaybackTime);
                if (lastPlaybackTime >= 0f && currentTime < lastPlaybackTime) {
                    goodWindowIndex = 0;
                    excellentWindowIndex = 0;
                    beatTimingIndex = 0;
                    viewBeatTimingIndex = 0;
                }

                EmitExcellentZoneEvents();
                EmitGoodZoneEvents();
                EmitBeatTimingEvents();
                EmitViewBeatTimingEvents();

                lastPlaybackTime = currentTime;
            });
        }

        public void Stop() {
            beatSoundSubscription?.Dispose();
            beatSoundSubscription = null;
            audioSource.Stop();
            isVirtualPlaying = false;
        }

        public void Pause() {
            audioSource.Pause();
            isVirtualPlaying = false;
        }

        public void Resume() {
            if (!aiSetting.IsLearning.CurrentValue) {
                audioSource.UnPause();
            }
            isVirtualPlaying = true;
        }

        public IMusicPlayer.BeatJudgeResult JudgeTiming(float playbackTime) {
            var judgeTime = playbackTime + audioSetting.CommandTimeOffset.CurrentValue;
            

            for (var i = 0; i < beats.Length; i++) {
                var beatTime = beats[i];
                if (judgeTime > beatTime) {
                    continue;
                }

                var goodWindow = Mathf.Max(0f, audioSetting.GoodWindow.CurrentValue);
                var excellentWindow = Mathf.Clamp(audioSetting.ExcellentWindow.CurrentValue, 0f, goodWindow);
                var excellentWindowStart = beatTime - excellentWindow;
                if (judgeTime >= excellentWindowStart) {
                    return new IMusicPlayer.BeatJudgeResult(BeatJudgeZone.Excellent, i, beatTime);
                }

                var goodWindowStart = beatTime - goodWindow;
                if (judgeTime >= goodWindowStart) {
                    return new IMusicPlayer.BeatJudgeResult(BeatJudgeZone.Good, i, beatTime);
                }
                
                return new IMusicPlayer.BeatJudgeResult(BeatJudgeZone.Miss, -1, 0f);
            }

            
            return new IMusicPlayer.BeatJudgeResult(BeatJudgeZone.Miss, -1, 0f);
        }

        void EmitGoodZoneEvents() {
            var judgeTime = CurrentPlaybackTime + audioSetting.CommandTimeOffset.CurrentValue;
            while (goodWindowIndex < beats.Length) {
                var beatTime = beats[goodWindowIndex];
                var windowStart = beatTime - Mathf.Max(0f, audioSetting.GoodWindow.CurrentValue);
                if (judgeTime < windowStart) {
                    return;
                }
                onGoodZoneEntered.OnNext(new IMusicPlayer.BeatSignal(goodWindowIndex, beatTime));
                goodWindowIndex += 1;
            }
        }

        void EmitExcellentZoneEvents() {
            var judgeTime = CurrentPlaybackTime + audioSetting.CommandTimeOffset.CurrentValue;
            while (excellentWindowIndex < beats.Length) {
                var beatTime = beats[excellentWindowIndex];
                var goodWindow = Mathf.Max(0f, audioSetting.GoodWindow.CurrentValue);
                var excellentWindow = Mathf.Clamp(audioSetting.ExcellentWindow.CurrentValue, 0f, goodWindow);
                var windowStart = beatTime - excellentWindow;
                if (judgeTime < windowStart) {
                    return;
                }

                onExcellentZoneEntered.OnNext(new IMusicPlayer.BeatSignal(excellentWindowIndex, beatTime));
                excellentWindowIndex += 1;
            }
        }

        void EmitBeatTimingEvents() {
            while (beatTimingIndex < beats.Length && CurrentPlaybackTime >= beats[beatTimingIndex]) {
                onBeatTiming.OnNext(new IMusicPlayer.BeatSignal(beatTimingIndex, beats[beatTimingIndex]));
                beatTimingIndex += 1;
            }
        }

        void EmitViewBeatTimingEvents() {
            while (viewBeatTimingIndex < beats.Length && currentViewPlaybackTime >= beats[viewBeatTimingIndex]) {
                onViewBeatTiming.OnNext(new IMusicPlayer.BeatSignal(viewBeatTimingIndex, beats[viewBeatTimingIndex]));
                viewBeatTimingIndex += 1;
            }
        }

        public void Dispose() {
            beatSoundSubscription?.Dispose();
            volumeSubscription?.Dispose();
            onGoodZoneEntered.Dispose();
            onExcellentZoneEntered.Dispose();
            onBeatTiming.Dispose();
            onViewBeatTiming.Dispose();
            onBeatTimelinePrepared.Dispose();
            onViewPlaybackTimeChanged.Dispose();
        }

        void ApplyVolume(VolumeBalance volumeBalance) {
            audioSource.volume = Mathf.Clamp01(volumeBalance.MasterVolume * volumeBalance.BgmVolume);
        }

        
    }
}