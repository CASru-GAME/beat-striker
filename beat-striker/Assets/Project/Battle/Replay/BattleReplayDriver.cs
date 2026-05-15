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
            IStrikerRegistry strikerRegistry) {
            this.replaySetting = replaySetting;
            this.gamePadRegistry = gamePadRegistry;
            this.musicPlayer = musicPlayer;
            this.strikerRegistry = strikerRegistry;
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
            currentRound = round != null ? new ReplayRoundRuntime(round) : new ReplayRoundRuntime(new ReplayRoundPayload { roundNumber = roundNumber });
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
            if (currentRound == null) {
                return;
            }

            var playbackTime = musicPlayer.CurrentPlaybackTime;
            while (currentRound.NextStateIndex < currentRound.States.Length
                   && currentRound.States[currentRound.NextStateIndex].playbackTime <= playbackTime) {
                ApplyState(currentRound.States[currentRound.NextStateIndex]);
                currentRound.NextStateIndex += 1;
            }

            while (currentRound.NextCommandIndex < currentRound.Commands.Length
                   && currentRound.Commands[currentRound.NextCommandIndex].time <= playbackTime) {
                ApplyCommand(currentRound.Commands[currentRound.NextCommandIndex]);
                currentRound.NextCommandIndex += 1;
            }
        }

        void ApplyCommand(ReplayBeatNotificationPayload command) {
            if (command.playerId < 0 || command.playerId >= replayGamePads.Length || replayGamePads[command.playerId] == null) {
                return;
            }

            var gamePad = replayGamePads[command.playerId];
            gamePad.EmitDirection(new Vector2(command.directionX, command.directionY));
            if ((OnlineBeatNotificationKind)command.kind != OnlineBeatNotificationKind.Command) {
                return;
            }

            var button = (GamePadButton)command.button;
            gamePad.EmitButtonDown(button);
            gamePad.EmitButtonUp(button);
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

            public ReplayRoundRuntime(ReplayRoundPayload payload) {
                Commands = (payload.beatNotifications ?? Array.Empty<ReplayBeatNotificationPayload>())
                    .OrderBy(item => item.time)
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
