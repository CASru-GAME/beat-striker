
using System;
using System.Collections.Generic;
using R3;
using TMPro;
using UnityEngine;

namespace Alice {

    public interface IBeatPlayer{
        public Observable<BeatResult> OnBeatCommandRequested { get; }
        public Observable<BeatResult> OnBeatCommandExecuted { get; }

        public record BeatResult(float Time, bool IsSuccess, GamePadButton Button);
    }

    public interface IBeatjudge {
        IBeatPlayer GetBeatPlayer(int playerId);
    }

    public class BeatJudge : IBeatjudge, IDisposable {
        readonly IMusicPlayer musicPlayer;
        readonly List<IDisposable> subscriptions = new();
        BeatPlayer[] beatPlayer = new BeatPlayer[2];

        public BeatJudge(IGamePadRegistry gamePadRegistry, IMusicPlayer musicPlayer) {
            this.musicPlayer = musicPlayer;

            for(int i = 0; i < beatPlayer.Length; i++) {
                beatPlayer[i] = new BeatPlayer();
            }

            for(int i = 0; i < beatPlayer.Length; i++) {
                var playerIndex = i;
                var gamePad = gamePadRegistry.Get(playerIndex);
                var subscription = gamePad.OnButtonDown.Subscribe(button => {
                    var time = musicPlayer.CurrentPlaybackTime;
                    var result = musicPlayer.JudgeTiming(time);
                    var isGood = result.Zone == BeatJudgeZone.Good;
                    if (isGood) {
                        beatPlayer[playerIndex].SavePendingCommand(result.BeatIndex, button);
                    }
                    beatPlayer[playerIndex].onBeat.OnNext(new IBeatPlayer.BeatResult(time, isGood, button));
                });
                subscriptions.Add(subscription);
            }

            subscriptions.Add(musicPlayer.OnBeatTiming.Subscribe(signal => {
                for (var playerIndex = 0; playerIndex < beatPlayer.Length; playerIndex++) {
                    if (!beatPlayer[playerIndex].TryConsumePendingCommand(signal.BeatIndex, out var button)) {
                        continue;
                    }

                    beatPlayer[playerIndex].onBeatResult.OnNext(new IBeatPlayer.BeatResult(signal.BeatTime, true, button));
                }
            }));
        }

        public IBeatPlayer GetBeatPlayer(int playerId) {
            if (playerId < 0 || playerId >= beatPlayer.Length) {
                throw new ArgumentOutOfRangeException(nameof(playerId), $"Player ID must be between 0 and {beatPlayer.Length - 1}");
            }
            return beatPlayer[playerId];
        }

        public void Dispose() {
            foreach (var subscription in subscriptions) {
                subscription.Dispose();
            }
            subscriptions.Clear();
        }

        class BeatPlayer : IBeatPlayer {
            readonly Dictionary<int, GamePadButton> pendingCommands = new Dictionary<int, GamePadButton>();
            public Subject<IBeatPlayer.BeatResult> onBeat = new Subject<IBeatPlayer.BeatResult>();
            public Subject<IBeatPlayer.BeatResult> onBeatResult = new Subject<IBeatPlayer.BeatResult>();

            public Observable<IBeatPlayer.BeatResult> OnBeatCommandRequested => onBeat;
            public Observable<IBeatPlayer.BeatResult> OnBeatCommandExecuted => onBeatResult;

            public void SavePendingCommand(int beatIndex, GamePadButton button) {
                pendingCommands[beatIndex] = button;
            }

            public bool TryConsumePendingCommand(int beatIndex, out GamePadButton button) {
                if (!pendingCommands.TryGetValue(beatIndex, out button)) {
                    return false;
                }

                pendingCommands.Remove(beatIndex);
                return true;
            }
        }
    }
}