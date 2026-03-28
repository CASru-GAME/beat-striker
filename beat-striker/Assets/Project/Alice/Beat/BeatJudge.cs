
using System;
using System.Collections.Generic;
using System.Diagnostics;
using R3;
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
        float lastCommandPlaybackTime = -1f;

        public BeatJudge(IGamePadRegistry gamePadRegistry, IMusicPlayer musicPlayer) {
            this.musicPlayer = musicPlayer;
            Log($"Construct players={beatPlayer.Length}");

            for(int i = 0; i < beatPlayer.Length; i++) {
                beatPlayer[i] = new BeatPlayer(i);
            }

            for(int i = 0; i < beatPlayer.Length; i++) {
                var playerIndex = i;
                var gamePad = gamePadRegistry.Get(playerIndex);
                var subscription = gamePad.OnButtonDown.Subscribe(button => {
                    var player = beatPlayer[playerIndex];
                    var time = musicPlayer.CurrentPlaybackTime;
                    Log($"Input player={playerIndex} button={button} time={time:F4} last={lastCommandPlaybackTime:F4}");
                    if (lastCommandPlaybackTime >= 0f && time < lastCommandPlaybackTime) {
                        Log($"LoopDetected time={time:F4} last={lastCommandPlaybackTime:F4} -> reset players");
                        for (var j = 0; j < beatPlayer.Length; j++) {
                            beatPlayer[j].ResetForLoop();
                        }
                    }
                    lastCommandPlaybackTime = time;

                    var result = musicPlayer.JudgeTiming(time);
                    var isTimingGood = result.Zone == BeatJudgeZone.Good && time < result.BeatTime;
                    Log($"JudgeResult player={playerIndex} zone={result.Zone} beatIndex={result.BeatIndex} beatTime={result.BeatTime:F4} isTimingGood={isTimingGood}");
                    var isGood = isTimingGood;
                    if (isGood) {
                        player.SavePendingCommand(result.BeatIndex, button);
                    }
                    Log($"RequestResult player={playerIndex} button={button} success={isGood}");
                    player.onBeat.OnNext(new IBeatPlayer.BeatResult(time, isGood, button));
                });
                subscriptions.Add(subscription);
            }

            subscriptions.Add(musicPlayer.OnBeatTiming.Subscribe(signal => {
                Log($"OnBeatTiming beatIndex={signal.BeatIndex} beatTime={signal.BeatTime:F4}");
                for (var playerIndex = 0; playerIndex < beatPlayer.Length; playerIndex++) {
                    if (!beatPlayer[playerIndex].TryConsumePendingCommand(signal.BeatIndex, out var button)) {
                        Log($"ExecuteMiss player={playerIndex} beatIndex={signal.BeatIndex} reason=no pending command");
                        continue;
                    }

                    Log($"ExecuteSuccess player={playerIndex} beatIndex={signal.BeatIndex} button={button}");
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
            Log("Dispose subscriptions");
            foreach (var subscription in subscriptions) {
                subscription.Dispose();
            }
            subscriptions.Clear();
        }

        [Conditional("UNITY_EDITOR")]
        [Conditional("DEVELOPMENT_BUILD")]
        static void Log(string message) {
            UnityEngine.Debug.Log($"[BeatJudge] {message}");
        }

        class BeatPlayer : IBeatPlayer {
            readonly int playerIndex;
            readonly Dictionary<int, GamePadButton> pendingCommands = new Dictionary<int, GamePadButton>();
            public Subject<IBeatPlayer.BeatResult> onBeat = new Subject<IBeatPlayer.BeatResult>();
            public Subject<IBeatPlayer.BeatResult> onBeatResult = new Subject<IBeatPlayer.BeatResult>();

            public BeatPlayer(int playerIndex) {
                this.playerIndex = playerIndex;
            }

            public Observable<IBeatPlayer.BeatResult> OnBeatCommandRequested => onBeat;
            public Observable<IBeatPlayer.BeatResult> OnBeatCommandExecuted => onBeatResult;

            public void SavePendingCommand(int beatIndex, GamePadButton button) {
                pendingCommands[beatIndex] = button;
                Log($"SavePending player={playerIndex} beatIndex={beatIndex} button={button} pendingCount={pendingCommands.Count}");
            }

            public bool TryConsumePendingCommand(int beatIndex, out GamePadButton button) {
                if (!pendingCommands.TryGetValue(beatIndex, out button)) {
                    Log($"ConsumePendingFail player={playerIndex} beatIndex={beatIndex} pendingCount={pendingCommands.Count}");
                    return false;
                }

                pendingCommands.Remove(beatIndex);
                Log($"ConsumePendingSuccess player={playerIndex} beatIndex={beatIndex} button={button} pendingCount={pendingCommands.Count}");
                return true;
            }

            public void ResetForLoop() {
                pendingCommands.Clear();
                Log($"ResetForLoop player={playerIndex}");
            }

            [Conditional("UNITY_EDITOR")]
            [Conditional("DEVELOPMENT_BUILD")]
            static void Log(string message) {
                UnityEngine.Debug.Log($"[BeatPlayer] {message}");
            }
        }
    }
}