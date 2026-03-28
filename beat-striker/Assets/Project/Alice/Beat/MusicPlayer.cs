
using System;
using System.Diagnostics;
using R3;
using UnityEngine;

namespace Alice {
    public enum BeatJudgeZone {
        Good,
        Miss,
    }

    public interface IMusicPlayer {
        void Play();
        Observable<BeatSignal> OnGoodZoneEntered { get; }
        Observable<BeatSignal> OnBeatTiming { get; }
        BeatJudgeResult JudgeTiming(float playbackTime);
        float CurrentPlaybackTime { get; }

        public record BeatSignal(int BeatIndex, float BeatTime);
        public record BeatJudgeResult(BeatJudgeZone Zone, int BeatIndex, float BeatTime);
    }

    public class MusicPlayer : IMusicPlayer, IDisposable {
        readonly AudioSource audioSource;
        readonly BeatConfig beatConfig;
        readonly Subject<IMusicPlayer.BeatSignal> onGoodZoneEntered = new();
        readonly Subject<IMusicPlayer.BeatSignal> onBeatTiming = new();
        IDisposable beatSoundSubscription;
        float[] beats = Array.Empty<float>();
        int beatSoundIndex;
        int goodWindowIndex;
        int beatTimingIndex;
        float lastPlaybackTime;

        public Observable<IMusicPlayer.BeatSignal> OnGoodZoneEntered => onGoodZoneEntered;
        public Observable<IMusicPlayer.BeatSignal> OnBeatTiming => onBeatTiming;
        public float CurrentPlaybackTime => audioSource.time;

        public MusicPlayer(AudioSource audioSource, BeatConfig beatConfig) {
            this.audioSource = audioSource;
            this.beatConfig = beatConfig;
        }

        public void Play() {
            var selectedTrack = beatConfig.SelectedTrack;
            var clip = selectedTrack.AudioClip;
            audioSource.clip = clip;
            audioSource.Play();
            Log($"Play track={selectedTrack.Name} bpm={selectedTrack.bpm:F2} offset={selectedTrack.offset:F4} commandOffset={beatConfig.CommandTimeOffset:F4} perfectWindow={beatConfig.PerfectWindow:F4}");

            beatSoundSubscription?.Dispose();
            beats = selectedTrack.beats;
            beatSoundIndex = 0;
            goodWindowIndex = 0;
            beatTimingIndex = 0;
            lastPlaybackTime = -1f;
            Log($"BeatSequence initialized beatCount={beats.Length}");
            beatSoundSubscription = Observable.EveryUpdate().Subscribe(_ => {
                if (!audioSource.isPlaying) return;
                var currentTime = audioSource.time;
                if (lastPlaybackTime >= 0f && currentTime < lastPlaybackTime) {
                    Log($"LoopDetected currentTime={currentTime:F4} lastPlaybackTime={lastPlaybackTime:F4} -> reset indices");
                    beatSoundIndex = 0;
                    goodWindowIndex = 0;
                    beatTimingIndex = 0;
                }

                EmitGoodZoneEvents();
                EmitBeatTimingEvents();

                while (beatSoundIndex < beats.Length && audioSource.time >= beats[beatSoundIndex]) {
                    Log($"BeatSound beatIndex={beatSoundIndex} beatTime={beats[beatSoundIndex]:F4} audioTime={audioSource.time:F4}");
                    AudioSource.PlayClipAtPoint(selectedTrack.beatSound, Vector3.zero);
                    beatSoundIndex += 1;
                }

                lastPlaybackTime = currentTime;
            });
        }

        public IMusicPlayer.BeatJudgeResult JudgeTiming(float playbackTime) {
            var judgeTime = playbackTime + beatConfig.CommandTimeOffset;
            Log($"JudgeTiming playbackTime={playbackTime:F4} judgeTime={judgeTime:F4}");

            for (var i = 0; i < beats.Length; i++) {
                var beatTime = beats[i];
                if (judgeTime > beatTime) {
                    continue;
                }

                var windowStart = beatTime - beatConfig.PerfectWindow;
                if (judgeTime >= windowStart) {
                    Log($"JudgeTimingResult zone=Good beatIndex={i} beatTime={beatTime:F4} windowStart={windowStart:F4}");
                    return new IMusicPlayer.BeatJudgeResult(BeatJudgeZone.Good, i, beatTime);
                }

                Log($"JudgeTimingResult zone=Miss reason=before window beatIndex={i} beatTime={beatTime:F4} windowStart={windowStart:F4}");
                return new IMusicPlayer.BeatJudgeResult(BeatJudgeZone.Miss, -1, 0f);
            }

            Log("JudgeTimingResult zone=Miss reason=no upcoming beat");
            return new IMusicPlayer.BeatJudgeResult(BeatJudgeZone.Miss, -1, 0f);
        }

        void EmitGoodZoneEvents() {
            var judgeTime = audioSource.time + beatConfig.CommandTimeOffset;
            while (goodWindowIndex < beats.Length) {
                var beatTime = beats[goodWindowIndex];
                var windowStart = beatTime - beatConfig.PerfectWindow;
                if (judgeTime < windowStart) {
                    return;
                }

                Log($"OnGoodZoneEntered beatIndex={goodWindowIndex} beatTime={beatTime:F4} judgeTime={judgeTime:F4} windowStart={windowStart:F4}");
                onGoodZoneEntered.OnNext(new IMusicPlayer.BeatSignal(goodWindowIndex, beatTime));
                goodWindowIndex += 1;
            }
        }

        void EmitBeatTimingEvents() {
            while (beatTimingIndex < beats.Length && audioSource.time >= beats[beatTimingIndex]) {
                Log($"OnBeatTiming beatIndex={beatTimingIndex} beatTime={beats[beatTimingIndex]:F4} audioTime={audioSource.time:F4}");
                onBeatTiming.OnNext(new IMusicPlayer.BeatSignal(beatTimingIndex, beats[beatTimingIndex]));
                beatTimingIndex += 1;
            }
        }

        public void Dispose() {
            beatSoundSubscription?.Dispose();
            onGoodZoneEntered.Dispose();
            onBeatTiming.Dispose();
        }

        [Conditional("UNITY_EDITOR")]
        [Conditional("DEVELOPMENT_BUILD")]
        static void Log(string message) {
            UnityEngine.Debug.Log($"[MusicPlayer] {message}");
        }
    }
}