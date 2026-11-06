using System;
using System.Collections.Generic;
using System.Linq;
using R3;
using UnityEngine;
using VContainer;

namespace Alice {
    public class BattleReplayDriver : IDisposable {
        const int PlayerCount = 2;

        readonly IReplaySetting replaySetting;
        readonly IGamePadRegistry gamePadRegistry;
        readonly IMusicPlayer musicPlayer;
        readonly IBeatjudge beatJudge;
        readonly IStrikerRegistry strikerRegistry;
        readonly ReplayGamePad[] replayGamePads = new ReplayGamePad[PlayerCount];
        readonly List<IDisposable> subscriptions = new();
        ReplayPayload replayPayload;
        ReplayRoundRuntime currentRound;
        bool isActive;

        [Inject]
        public BattleReplayDriver(
            IReplaySetting replaySetting,
            IGamePadRegistry gamePadRegistry,
            IMusicPlayer musicPlayer,
            IStrikerRegistry strikerRegistry,
            IBeatjudge beatJudge) {
            this.replaySetting = replaySetting;
            this.gamePadRegistry = gamePadRegistry;
            this.musicPlayer = musicPlayer;
            this.strikerRegistry = strikerRegistry;
            this.beatJudge = beatJudge;
        }

        public bool IsReplay => replaySetting.TryGetReplay(out replayPayload);

        public void PrepareReplayInputs() {
            if (!IsReplay) {
                return;
            }

            for (var playerId = 0; playerId < PlayerCount; playerId++) {
                replayGamePads[playerId] = new ReplayGamePad(playerId);
                gamePadRegistry.RequestRegisterReplay(playerId, replayGamePads[playerId]);
            }
        }

        public void BeginRound(int roundNumber) {
            if (!IsReplay || replayPayload.rounds == null) {
                return;
            }

            var round = replayPayload.rounds.FirstOrDefault(item => item.roundNumber == roundNumber);
            currentRound = new ReplayRoundRuntime(round ?? new ReplayRoundPayload { roundNumber = roundNumber },
                replayPayload.executesCommandsImmediately);
            if (!isActive) {
                subscriptions.Add(Observable.EveryUpdate().Subscribe(_ => Tick()));
                isActive = true;
            }
        }

        public void FinishReplay() {
            StopRuntime();
            gamePadRegistry.RestoreReplayOverrides();
            for (var i = 0; i < replayGamePads.Length; i++) {
                replayGamePads[i]?.DestroyGamePad();
                replayGamePads[i] = null;
            }
            replaySetting.ClearReplay();
        }

        void Tick() {
            if (currentRound == null || beatJudge.IsPaused || musicPlayer.CurrentBeatTimeline.Length == 0) {
                return;
            }

            var playbackTime = musicPlayer.CurrentPlaybackTime;
            var round = currentRound;
            while (currentRound == round && !beatJudge.IsPaused) {
                var stateTime = round.NextStateIndex < round.States.Length
                    ? round.States[round.NextStateIndex].playbackTime : float.PositiveInfinity;
                var commandTime = round.NextCommandIndex < round.Commands.Length
                    ? GetExecutionTime(round.Commands[round.NextCommandIndex]) : float.PositiveInfinity;
                if (Mathf.Min(stateTime, commandTime) > playbackTime) break;

                // Immediate attacks can precede a pre-beat snapshot, even within the same frame.
                if (stateTime <= commandTime) ApplyState(round.States[round.NextStateIndex++]);
                else ApplyCommand(round.Commands[round.NextCommandIndex++]);
            }
        }

        float GetExecutionTime(ReplayBeatNotificationPayload command) {
            if (replayPayload.executesCommandsImmediately) return command.time;
            var timeline = musicPlayer.CurrentBeatTimeline;
            return command.beatIndex >= 0 && command.beatIndex < timeline.Length
                ? timeline[command.beatIndex]
                : command.time;
        }

        void ApplyCommand(ReplayBeatNotificationPayload command) {
            if (command.playerId < 0 || command.playerId >= replayGamePads.Length || replayGamePads[command.playerId] == null) {
                return;
            }

            var gamePad = replayGamePads[command.playerId];
            gamePad.EmitDirection(new Vector2(command.directionX, command.directionY));
            // Use the recorded judgement; frame delay must not turn a late success into a miss.
            beatJudge.ApplyReplayNotification(command);
        }

        void ApplyState(ReplayPreBeatStatePayload state) {
            if (!strikerRegistry.Get(state.playerId).TryGetValue(out var striker)) {
                return;
            }

            striker.ApplyReplayAbsoluteState(state);
        }

        public void Dispose() {
            StopRuntime();
        }

        void StopRuntime() {
            foreach (var subscription in subscriptions) {
                subscription.Dispose();
            }
            subscriptions.Clear();
            currentRound = null;
            isActive = false;
        }

        class ReplayRoundRuntime {
            public ReplayBeatNotificationPayload[] Commands { get; }
            public ReplayPreBeatStatePayload[] States { get; }
            public int NextCommandIndex { get; set; }
            public int NextStateIndex { get; set; }

            public ReplayRoundRuntime(ReplayRoundPayload payload, bool executesCommandsImmediately) {
                Commands = (payload.beatNotifications ?? Array.Empty<ReplayBeatNotificationPayload>())
                    .OrderBy(item => executesCommandsImmediately ? item.time : item.beatIndex)
                    .ThenBy(item => item.playerId)
                    .ToArray();
                States = (payload.preBeatStates ?? Array.Empty<ReplayPreBeatStatePayload>())
                    .OrderBy(item => item.playbackTime)
                    .ThenBy(item => item.playerId)
                    .ToArray();
            }
        }
    }
}
