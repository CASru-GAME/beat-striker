
using System;
using System.Collections.Generic;
 
using R3;
using UnityEngine;
using VContainer;

namespace Alice {
    public enum BeatJudgeZone {
        Excellent,
        Good,
        Miss,
    }

    public interface IMusicPlayer {
        Awaitable PlayAsync(BattleAddressablePreload preload = null);
        void Stop();
        void Pause();
        void Resume();
        void SyncPlaybackTime(float playbackTime);
        Observable<BeatSignal> OnGoodZoneEntered { get; }
        Observable<BeatSignal> OnExcellentZoneEntered { get; }
        Observable<BeatSignal> OnBeatTiming { get; }
        Observable<BeatSignal> OnViewBeatTiming { get; }
        Observable<float[]> OnBeatTimelinePrepared { get; }
        Observable<float> OnViewPlaybackTimeChanged { get; }
        Observable<Unit> OnPlaybackCompleted { get; }
        BeatJudgeResult JudgeTiming(float playbackTime);
        float CurrentPlaybackTime { get; }
        float CurrentViewPlaybackTime { get; }
        float[] CurrentBeatTimeline { get; }

        public record BeatSignal(int BeatIndex, float BeatTime);
        public record BeatJudgeResult(BeatJudgeZone Zone, int BeatIndex, float BeatTime);
    }

    public class MusicPlayer : IMusicPlayer, IDisposable {
        const string LOG_PREFIX = "[MusicPlayer]";
        enum PlaybackClockMode {
            AudioTimeline,
            AudioLoop,
            VirtualLoop,
        }

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
        readonly Subject<Unit> onPlaybackCompleted = new();
        IDisposable beatSoundSubscription;
        IDisposable volumeSubscription;
        float[] beats = Array.Empty<float>();
        int goodWindowIndex;
        int excellentWindowIndex;
        int beatTimingIndex;
        int viewBeatTimingIndex;
        float lastPlaybackTime;
        float currentViewPlaybackTime;
        float logicalPlaybackTime;
        float playbackClipLengthSeconds;
        float playbackTimelineLengthSeconds;
        PlaybackClockMode playbackClockMode;
        int completedAudioLoopCount;
        float lastRawAudioTime;
        bool isPlaybackClockRunning;
        bool playbackCompleted;
        bool isPlaybackPaused;
        int playVersion;
        LoadedAsset<AudioClip> loadedClipAsset;
        LoadedAsset<TextAsset> loadedBeatDataAsset;
        bool ownsLoadedAssets;

        public Observable<IMusicPlayer.BeatSignal> OnGoodZoneEntered => onGoodZoneEntered;
        public Observable<IMusicPlayer.BeatSignal> OnExcellentZoneEntered => onExcellentZoneEntered;
        public Observable<IMusicPlayer.BeatSignal> OnBeatTiming => onBeatTiming;
        public Observable<IMusicPlayer.BeatSignal> OnViewBeatTiming => onViewBeatTiming;
        public Observable<float[]> OnBeatTimelinePrepared => onBeatTimelinePrepared;
        public Observable<float> OnViewPlaybackTimeChanged => onViewPlaybackTimeChanged;
        public Observable<Unit> OnPlaybackCompleted => onPlaybackCompleted;
        public float CurrentPlaybackTime => GetCurrentPlaybackTime();
        public float CurrentViewPlaybackTime => currentViewPlaybackTime;
        public float[] CurrentBeatTimeline => beats;

        [Inject]
        public MusicPlayer(AudioSource audioSource, IMusicRegistry musicRegistry, IAudioSetting audioSetting, IBattleSelectSetting battleSelectSetting, IAISetting aiSetting) {
            this.audioSource = audioSource;
            this.musicRegistry = musicRegistry;
            this.audioSetting = audioSetting;
            this.battleSelectSetting = battleSelectSetting;
            this.aiSetting = aiSetting;

            volumeSubscription = this.audioSetting.VolumeBalance.Subscribe(ApplyVolume);
        }

        public async Awaitable PlayAsync(BattleAddressablePreload preload = null) {
            ReleaseLoadedAssets();
            var version = playVersion;
            var selectedMusic = musicRegistry.GetById(battleSelectSetting.SelectedMusicId.CurrentValue);
            var usesPreloadedAssets = preload != null && preload.HasMusic(selectedMusic.Id);
            LoadedAsset<AudioClip> clipAsset;
            if (usesPreloadedAssets) {
                clipAsset = preload.MusicClipAsset;
            }
            else {
                clipAsset = await musicRegistry.LoadAudioClipAsync(selectedMusic.Id);
            }
            if (version != playVersion) {
                DisposeIfOwned(clipAsset, !usesPreloadedAssets);
                return;
            }

            LoadedAsset<TextAsset> beatDataAsset;
            if (usesPreloadedAssets) {
                beatDataAsset = preload.BeatDataAsset;
            }
            else {
                beatDataAsset = await musicRegistry.LoadBeatDataAsync(selectedMusic.Id);
            }
            if (version != playVersion) {
                DisposeIfOwned(clipAsset, !usesPreloadedAssets);
                DisposeIfOwned(beatDataAsset, !usesPreloadedAssets);
                return;
            }

            ownsLoadedAssets = !usesPreloadedAssets;
            loadedClipAsset = clipAsset;
            loadedBeatDataAsset = beatDataAsset;
            var clip = loadedClipAsset.Asset;
            var beatData = loadedBeatDataAsset.Asset;
            var beatOffset = audioSetting.BeatTimeOffset.CurrentValue;
            Debug.Log($"{LOG_PREFIX} Play start. musicId={selectedMusic.Id}, beatOffset={beatOffset:0.###}, commandOffset={audioSetting.CommandTimeOffset.CurrentValue:0.###}, viewOffset={audioSetting.ViewTimeOffset.CurrentValue:0.###}");
            audioSource.clip = clip;
            playbackClipLengthSeconds = clip != null ? Mathf.Max(0f, clip.length) : 0f;
            playbackClockMode = ResolvePlaybackClockMode();
            playbackTimelineLengthSeconds = ResolvePlaybackTimelineLengthSeconds(playbackClipLengthSeconds, playbackClockMode);
            audioSource.loop = ShouldLoopAudioPlayback(playbackClockMode);

            beatSoundSubscription?.Dispose();
            beats = BuildPlaybackTimelineBeats(beatData, playbackClipLengthSeconds, playbackTimelineLengthSeconds);
            onBeatTimelinePrepared.OnNext(beats);
            ResetBeatEventIndexes();
            lastPlaybackTime = -1f;
            logicalPlaybackTime = 0f;
            completedAudioLoopCount = 0;
            lastRawAudioTime = -1f;
            isPlaybackClockRunning = true;
            playbackCompleted = false;
            isPlaybackPaused = false;

            if (clip == null && playbackClockMode != PlaybackClockMode.VirtualLoop) {
                Debug.LogWarning($"{LOG_PREFIX} Selected music clip was not loaded. musicId={selectedMusic.Id}");
                CompletePlayback();
                return;
            }

            if (playbackClockMode != PlaybackClockMode.VirtualLoop) {
                audioSource.Play();
            }
            
            beatSoundSubscription = Observable.EveryUpdate().Subscribe(_ => {
                if (!AdvancePlaybackClock(clip)) {
                    return;
                }

                var currentTime = CurrentPlaybackTime;
                if (HasReachedAudioTimelineEnd(currentTime)) {
                    StopAudioPlayback();
                    CompletePlayback();
                    return;
                }
                currentViewPlaybackTime = currentTime + audioSetting.ViewTimeOffset.CurrentValue;
                onViewPlaybackTimeChanged.OnNext(currentViewPlaybackTime);
                if (lastPlaybackTime >= 0f && currentTime < lastPlaybackTime) {
                    ResetBeatEventIndexes();
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
            audioSource.clip = null;
            ReleaseLoadedAssets();
            isPlaybackClockRunning = false;
            playbackCompleted = true;
            isPlaybackPaused = false;
        }

        public void Pause() {
            audioSource.Pause();
            isPlaybackClockRunning = false;
            isPlaybackPaused = true;
        }

        public void Resume() {
            if (playbackClockMode != PlaybackClockMode.VirtualLoop) {
                audioSource.UnPause();
            }
            isPlaybackClockRunning = true;
            isPlaybackPaused = false;
        }

        public void SyncPlaybackTime(float playbackTime) {
            var clampedTime = Mathf.Max(0f, playbackTime);
            if (playbackTimelineLengthSeconds > 0f) {
                if (playbackClockMode == PlaybackClockMode.AudioLoop) {
                    clampedTime %= playbackTimelineLengthSeconds;
                }
                else {
                    clampedTime = Mathf.Min(clampedTime, playbackTimelineLengthSeconds);
                }
            }

            logicalPlaybackTime = clampedTime;
            if (playbackClockMode != PlaybackClockMode.VirtualLoop && audioSource.clip != null) {
                var clipLength = Mathf.Max(0f, audioSource.clip.length);
                if (clipLength > 0f) {
                    if (ShouldLoopAudioPlayback(playbackClockMode)) {
                        completedAudioLoopCount = Mathf.FloorToInt(clampedTime / clipLength);
                        var audioTime = clampedTime % clipLength;
                        audioSource.time = audioTime;
                        lastRawAudioTime = audioTime;
                    }
                    else {
                        var audioTime = Mathf.Min(clampedTime, clipLength);
                        audioSource.time = audioTime;
                        lastRawAudioTime = audioTime;
                        completedAudioLoopCount = 0;
                    }
                }
            }

            currentViewPlaybackTime = clampedTime + audioSetting.ViewTimeOffset.CurrentValue;
            onViewPlaybackTimeChanged.OnNext(currentViewPlaybackTime);
            ResetBeatIndexesForTime(clampedTime);
            lastPlaybackTime = clampedTime;
        }

        void CompletePlayback() {
            if (playbackCompleted) {
                return;
            }

            playbackCompleted = true;
            onPlaybackCompleted.OnNext(Unit.Default);
        }

        void ResetBeatEventIndexes() {
            goodWindowIndex = 0;
            excellentWindowIndex = 0;
            beatTimingIndex = 0;
            viewBeatTimingIndex = 0;
        }

        void ResetBeatIndexesForTime(float playbackTime) {
            goodWindowIndex = ResolveNextGoodWindowIndex(playbackTime);
            excellentWindowIndex = ResolveNextExcellentWindowIndex(playbackTime);
            beatTimingIndex = ResolveNextBeatTimingIndex(playbackTime);
            viewBeatTimingIndex = ResolveNextViewBeatTimingIndex(playbackTime);
        }

        int ResolveNextGoodWindowIndex(float playbackTime) {
            var judgeTime = playbackTime + audioSetting.CommandTimeOffset.CurrentValue;
            var window = Mathf.Max(0f, audioSetting.GoodWindow.CurrentValue);
            var index = 0;
            while (index < beats.Length && beats[index] - window <= judgeTime) {
                index += 1;
            }

            return index;
        }

        int ResolveNextExcellentWindowIndex(float playbackTime) {
            var judgeTime = playbackTime + audioSetting.CommandTimeOffset.CurrentValue;
            var goodWindow = Mathf.Max(0f, audioSetting.GoodWindow.CurrentValue);
            var excellentWindow = Mathf.Clamp(audioSetting.ExcellentWindow.CurrentValue, 0f, goodWindow);
            var index = 0;
            while (index < beats.Length && beats[index] - excellentWindow <= judgeTime) {
                index += 1;
            }

            return index;
        }

        int ResolveNextBeatTimingIndex(float playbackTime) {
            var index = 0;
            while (index < beats.Length && beats[index] <= playbackTime) {
                index += 1;
            }

            return index;
        }

        int ResolveNextViewBeatTimingIndex(float playbackTime) {
            var viewTime = playbackTime + audioSetting.ViewTimeOffset.CurrentValue;
            var index = 0;
            while (index < beats.Length && beats[index] <= viewTime) {
                index += 1;
            }

            return index;
        }

        PlaybackClockMode ResolvePlaybackClockMode() {
            if (aiSetting.UsesVirtualPlaybackClock) {
                return PlaybackClockMode.VirtualLoop;
            }

            if (aiSetting.IsInfiniteRoundMode) {
                return PlaybackClockMode.AudioLoop;
            }

            return PlaybackClockMode.AudioTimeline;
        }

        bool AdvancePlaybackClock(AudioClip clip) {
            if (playbackClockMode == PlaybackClockMode.VirtualLoop) {
                if (!isPlaybackClockRunning) {
                    return false;
                }

                AdvanceVirtualLoopClock();
                return true;
            }

            if (!audioSource.isPlaying) {
                if (!isPlaybackPaused && clip != null && !audioSource.loop && lastPlaybackTime >= clip.length - 0.05f) {
                    CompletePlayback();
                }

                return false;
            }

            UpdateAudioLoopProgress();
            return true;
        }

        float GetCurrentPlaybackTime() {
            return playbackClockMode switch {
                PlaybackClockMode.VirtualLoop => logicalPlaybackTime,
                PlaybackClockMode.AudioLoop => GetLoopedAudioTime(),
                PlaybackClockMode.AudioTimeline => GetContinuousAudioTimelineTime(),
                _ => 0f,
            };
        }

        float GetLoopedAudioTime() {
            return Mathf.Max(0f, audioSource.time);
        }

        float GetContinuousAudioTimelineTime() {
            if (playbackClipLengthSeconds <= 0f) {
                return GetLoopedAudioTime();
            }

            var playbackTime = completedAudioLoopCount * playbackClipLengthSeconds + GetLoopedAudioTime();
            if (playbackTimelineLengthSeconds > 0f) {
                playbackTime = Mathf.Min(playbackTime, playbackTimelineLengthSeconds);
            }

            return playbackTime;
        }

        void UpdateAudioLoopProgress() {
            if (playbackClipLengthSeconds <= 0f || !audioSource.loop) {
                lastRawAudioTime = GetLoopedAudioTime();
                return;
            }

            var rawAudioTime = GetLoopedAudioTime();
            if (lastRawAudioTime >= 0f && rawAudioTime + 0.001f < lastRawAudioTime) {
                completedAudioLoopCount += 1;
            }

            lastRawAudioTime = rawAudioTime;
        }

        void AdvanceVirtualLoopClock() {
            logicalPlaybackTime += Time.deltaTime;
            if (playbackTimelineLengthSeconds <= 0f) {
                return;
            }

            while (logicalPlaybackTime >= playbackTimelineLengthSeconds) {
                logicalPlaybackTime -= playbackTimelineLengthSeconds;
            }
        }

        bool HasReachedAudioTimelineEnd(float playbackTime) {
            return playbackClockMode == PlaybackClockMode.AudioTimeline
                && playbackTimelineLengthSeconds > 0f
                && playbackTime >= playbackTimelineLengthSeconds;
        }

        void StopAudioPlayback() {
            if (playbackClockMode != PlaybackClockMode.VirtualLoop) {
                audioSource.Stop();
            }

            isPlaybackClockRunning = false;
        }

        float ResolvePlaybackTimelineLengthSeconds(float clipLength, PlaybackClockMode clockMode) {
            if (clipLength <= 0f) {
                return clipLength;
            }

            if (clockMode == PlaybackClockMode.AudioLoop) {
                return clipLength;
            }

            var minimumPlaybackSeconds = Mathf.Max(0f, audioSetting.MinimumPlaybackSeconds);
            if (minimumPlaybackSeconds <= 0f || clipLength >= minimumPlaybackSeconds) {
                return clipLength;
            }

            var loopCount = Mathf.CeilToInt(minimumPlaybackSeconds / clipLength);
            return loopCount * clipLength;
        }

        float[] BuildPlaybackTimelineBeats(TextAsset beatData, float clipLength, float targetPlaybackLengthSeconds) {
            var oneLoopBeats = LoadSingleLoopBeats(beatData, clipLength);
            if (clipLength <= 0f || targetPlaybackLengthSeconds <= clipLength) {
                return oneLoopBeats;
            }

            var loopCount = Mathf.Max(1, Mathf.CeilToInt(targetPlaybackLengthSeconds / clipLength));
            var timeline = new List<float>(oneLoopBeats.Length * loopCount);
            for (var loopIndex = 0; loopIndex < loopCount; loopIndex++) {
                var loopStartTime = loopIndex * clipLength;
                for (var beatIndex = 0; beatIndex < oneLoopBeats.Length; beatIndex++) {
                    var beatTime = loopStartTime + oneLoopBeats[beatIndex];
                    if (beatTime >= targetPlaybackLengthSeconds) {
                        break;
                    }

                    timeline.Add(beatTime);
                }
            }

            return timeline.ToArray();
        }

        bool ShouldLoopAudioPlayback(PlaybackClockMode clockMode) {
            if (playbackClipLengthSeconds <= 0f) {
                return false;
            }

            return clockMode == PlaybackClockMode.AudioLoop
                || (clockMode == PlaybackClockMode.AudioTimeline && playbackTimelineLengthSeconds > playbackClipLengthSeconds);
        }

        float[] LoadSingleLoopBeats(TextAsset beatData, float clipLength) {
            if (clipLength <= 0f || beatData == null) {
                return Array.Empty<float>();
            }

            var timeline = new List<float>();
            var beatOffset = audioSetting.BeatTimeOffset.CurrentValue;
            foreach (var beatTimeRaw in BeatDataParser.ParseBeatTimes(beatData)) {
                var beatTime = beatTimeRaw + beatOffset;
                if (beatTime < 0f || beatTime >= clipLength) {
                    continue;
                }

                timeline.Add(beatTime);
            }

            timeline.Sort();
            return timeline.ToArray();
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
            Stop();
            beatSoundSubscription?.Dispose();
            volumeSubscription?.Dispose();
            onGoodZoneEntered.Dispose();
            onExcellentZoneEntered.Dispose();
            onBeatTiming.Dispose();
            onViewBeatTiming.Dispose();
            onBeatTimelinePrepared.Dispose();
            onViewPlaybackTimeChanged.Dispose();
            onPlaybackCompleted.Dispose();
        }

        void ApplyVolume(VolumeBalance volumeBalance) {
            audioSource.volume = Mathf.Clamp01(volumeBalance.MasterVolume * volumeBalance.BgmVolume);
        }

        void ReleaseLoadedAssets() {
            playVersion++;
            audioSource.clip = null;
            if (ownsLoadedAssets) {
                loadedClipAsset?.Dispose();
                loadedBeatDataAsset?.Dispose();
            }

            loadedClipAsset = null;
            loadedBeatDataAsset = null;
            ownsLoadedAssets = false;
        }

        static void DisposeIfOwned<T>(LoadedAsset<T> asset, bool ownsAsset) where T : UnityEngine.Object {
            if (ownsAsset) {
                asset?.Dispose();
            }
        }

        
    }
}