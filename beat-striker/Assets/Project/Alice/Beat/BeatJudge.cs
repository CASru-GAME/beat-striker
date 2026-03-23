
using System;
using System.Collections.Generic;
using R3;
using TMPro;
using UnityEngine;

namespace Alice {

    public interface IBeatPlayer{
        public Observable<BeatResult> OnBeat { get; }
        public Observable<BeatResult> OnBeatExecuted { get; }

        public record BeatResult(float Time, bool IsSuccess);
    }

    public interface IBeatjudge {
        IBeatPlayer GetBeatPlayer(int playerId);
    }

    public class BeatJudge : IBeatjudge, IDisposable {
        BeatConfig beatConfig;
        Track track;
        AudioSource audioSource;
        readonly List<IDisposable> subscriptions = new();
        BeatPlayer[] beatPlayer = new BeatPlayer[2];

        public BeatJudge(BeatConfig beatConfig, IGamePadRegistry gamePadRegistry, AudioSource audioSource) {
            this.beatConfig = beatConfig;
            this.track = beatConfig.SelectedTrack;
            this.audioSource = audioSource;

            for(int i = 0; i < beatPlayer.Length; i++) {
                beatPlayer[i] = new BeatPlayer();
            }

            for(int i = 0; i < beatPlayer.Length; i++) {
                var gamePad = gamePadRegistry.Get(i);
                var subscription = gamePad.OnButtonDown.Subscribe(button => {
                    var time = audioSource.time;
                    var res = TryJudge(time, out var beatTime);
                    if(res) {
                        ScheduleSuccessLog(beatTime, i);
                    }
                    beatPlayer[i].onBeat.OnNext(new IBeatPlayer.BeatResult(time, res));
                });
                subscriptions.Add(subscription);
            }
        }

        public IBeatPlayer GetBeatPlayer(int playerId) {
            if (playerId < 0 || playerId >= beatPlayer.Length) {
                throw new ArgumentOutOfRangeException(nameof(playerId), $"Player ID must be between 0 and {beatPlayer.Length - 1}");
            }
            return beatPlayer[playerId];
        }

        bool TryJudge(float playbackTime, out float successBeatTime) {
            var judgeTime = playbackTime + beatConfig.CommandTimeOffset;
            var beats = track.beats;

            for (int i = 0; i < beats.Length; i++) {
                var beatTime = beats[i];
                var windowStart = beatTime - beatConfig.PerfectWindow;

                if (judgeTime < windowStart) {
                    successBeatTime = 0f;
                    return false;
                }

                if (judgeTime <= beatTime) {
                    successBeatTime = beatTime;
                    return true;
                }
            }

            successBeatTime = 0f;
            return false;
        }

        void ScheduleSuccessLog(float beatTime, int playerId) {
            IDisposable logSubscription = null;
            logSubscription = Observable.EveryUpdate().Subscribe(_ => {
                if (audioSource.time < beatTime) return;

                Debug.Log($"Player {playerId} hit beat at {beatTime:F3}s");
                beatPlayer[playerId].onBeatResult.OnNext(new IBeatPlayer.BeatResult(beatTime, true));
                logSubscription.Dispose();
            });

            subscriptions.Add(logSubscription);
        }

        public void Dispose() {
            foreach (var subscription in subscriptions) {
                subscription.Dispose();
            }
            subscriptions.Clear();
        }

        class BeatPlayer : IBeatPlayer {
            public Subject<IBeatPlayer.BeatResult> onBeat = new Subject<IBeatPlayer.BeatResult>();
            public Subject<IBeatPlayer.BeatResult> onBeatResult = new Subject<IBeatPlayer.BeatResult>();

            public Observable<IBeatPlayer.BeatResult> OnBeat => onBeat;
            public Observable<IBeatPlayer.BeatResult> OnBeatExecuted => onBeatResult;
        }
    }
}