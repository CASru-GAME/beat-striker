using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Fusion;
using Fusion.Sockets;
using R3;
using UnityEngine;
using VContainer;

namespace Alice {
    public enum BattleOutcomeKind {
        RoundResolved = 1,
        BattleFinished = 2,
    }

    public record BattleFlowPhaseSnapshot(ulong Sequence, BattleFlowState State, int Round);

    public record BattleOutcomeSnapshot(
        ulong Sequence,
        BattleOutcomeKind Kind,
        int FinishedRound,
        int DeadPlayerId,
        int RoundWinnerPlayerId,
        bool ContinueBattle,
        int FinalWinnerPlayerId,
        bool StopMusic,
        int[] PlayerIds,
        int[] RoundWinCounts);

    public record OnlineBeatCommandSnapshot(
        ulong Sequence,
        int PlayerId,
        int BeatIndex,
        float Time,
        bool IsSuccess,
        BeatJudgeZone Zone,
        GamePadButton Button,
        Vector2 Direction);

    public record OnlineStrikerPreCommandSnapshot(
        ulong Sequence,
        int ApplyBeatIndex,
        int PlayerId,
        float HitPoint,
        float SpecialPoint,
        Vector3 Position,
        string StatePathId,
        float SentNetworkTime);

    public record OnlineRoundStartSnapshot(ulong Sequence, int Round, float StartNetworkTime);

    public record OnlineBeatSyncResumeSnapshot(ulong Sequence, int BeatIndex, float ResumeNetworkTime, float HostPlaybackTime);

    public interface IBattleOnlineSync {
        bool IsSessionHost { get; }
        bool IsReady { get; }
        float NetworkTime { get; }
        Observable<BattleFlowPhaseSnapshot> OnPhaseReceived { get; }
        Observable<BattleOutcomeSnapshot> OnOutcomeReceived { get; }
        Observable<OnlineBeatCommandSnapshot> OnBeatCommandReceived { get; }
        Observable<OnlineStrikerPreCommandSnapshot> OnStrikerPreCommandSnapshotReceived { get; }
        Observable<Unit> OnPauseRequested { get; }
        Observable<Unit> OnResumeRequested { get; }
        Observable<Unit> OnSuspendFinishRequested { get; }
        Observable<int> OnRoundResolutionRequested { get; }
        Observable<Unit> OnDisconnected { get; }
        void PublishPhase(BattleFlowState state, int round);
        void RequestPause();
        void RequestResume();
        void RequestSuspendFinish();
        void RequestRoundResolution(int deadPlayerId);
        void PublishOutcome(BattleOutcomeSnapshot snapshot);
        void PublishBeatCommand(OnlineBeatCommandSnapshot snapshot);
        void PublishStrikerPreCommandSnapshot(OnlineStrikerPreCommandSnapshot snapshot);
        bool TryGetLatestStrikerPreCommandSnapshot(int applyBeatIndex, int playerId, out OnlineStrikerPreCommandSnapshot snapshot);
        Task<OnlineStrikerPreCommandSnapshot> WaitForStrikerPreCommandSnapshotAsync(int applyBeatIndex, int playerId, float waitTimeoutSeconds);
        void ClearStrikerPreCommandSnapshotsBefore(int beatIndex);
        void RequestRoundStartReady(int round);
        void PublishRoundStartSchedule(int round, float startNetworkTime);
        void PublishBeatSyncResume(int beatIndex, float resumeNetworkTime, float hostPlaybackTime);
        Task WaitForPhaseAtLeastAsync(BattleFlowState state, int round);
        Task<BattleOutcomeSnapshot> WaitForOutcomeAsync(BattleOutcomeKind kind, int finishedRound);
        Task WaitForRoundStartReadyAsync(int round);
        Task<OnlineRoundStartSnapshot> WaitForRoundStartScheduleAsync(int round);
        Task<OnlineBeatSyncResumeSnapshot> WaitForBeatSyncResumeAsync(int beatIndex);
    }

    public class BattleOnlineSync : IBattleOnlineSync, INetworkRunnerCallbacks, IDisposable {
        const string LOG_PREFIX = "[BattleOnlineSync]";

        readonly INetworkRunnerProvider runnerProvider;
        readonly IAppNetworkSetting appNetworkSetting;
        readonly ReactiveProperty<BattleFlowPhaseSnapshot> latestPhase = new(new BattleFlowPhaseSnapshot(0, BattleFlowState.NotStarted, 0));
        readonly ReactiveProperty<BattleOutcomeSnapshot> latestOutcome = new(new BattleOutcomeSnapshot(0, 0, 0, -1, -1, false, -1, false, Array.Empty<int>(), Array.Empty<int>()));
        readonly ReactiveProperty<OnlineRoundStartSnapshot> latestRoundStart = new(new OnlineRoundStartSnapshot(0, 0, 0));
        readonly ReactiveProperty<OnlineBeatSyncResumeSnapshot> latestBeatSyncResume = new(new OnlineBeatSyncResumeSnapshot(0, -1, 0, 0));
        readonly Subject<OnlineBeatCommandSnapshot> beatCommandReceivedSubject = new();
        readonly Subject<OnlineStrikerPreCommandSnapshot> strikerPreCommandSnapshotReceivedSubject = new();
        readonly Dictionary<(int ApplyBeatIndex, int PlayerId), OnlineStrikerPreCommandSnapshot> latestStrikerPreCommandSnapshots = new();
        readonly Subject<Unit> pauseRequestedSubject = new();
        readonly Subject<Unit> resumeRequestedSubject = new();
        readonly Subject<Unit> suspendFinishRequestedSubject = new();
        readonly Subject<int> roundResolutionRequestedSubject = new();
        readonly Subject<Unit> disconnectedSubject = new();
        NetworkRunner runner;
        ulong phaseSequence;
        ulong outcomeSequence;
        ulong beatCommandSequence;
        ulong strikerPreCommandSnapshotSequence;
        ulong roundStartSequence;
        ulong beatSyncResumeSequence;
        int latestRoundStartReadyRound;
        bool callbacksRegistered;
        bool disconnected;

        [Inject]
        public BattleOnlineSync(INetworkRunnerProvider runnerProvider, IAppNetworkSetting appNetworkSetting) {
            this.runnerProvider = runnerProvider;
            this.appNetworkSetting = appNetworkSetting;
            TryRegisterCallbacks();
        }

        public bool IsReady => IsOnline() && TryRegisterCallbacks();
        public bool IsSessionHost => IsReady && !runner.IsServer && appNetworkSetting.LocalOnlinePlayerId == 0;
        public float NetworkTime => IsReady ? runner.SimulationTime : Time.realtimeSinceStartup;
        public Observable<BattleFlowPhaseSnapshot> OnPhaseReceived => latestPhase.Where(snapshot => snapshot.Sequence > 0);
        public Observable<BattleOutcomeSnapshot> OnOutcomeReceived => latestOutcome.Where(snapshot => snapshot.Sequence > 0);
        public Observable<OnlineBeatCommandSnapshot> OnBeatCommandReceived => beatCommandReceivedSubject;
        public Observable<OnlineStrikerPreCommandSnapshot> OnStrikerPreCommandSnapshotReceived => strikerPreCommandSnapshotReceivedSubject;
        public Observable<Unit> OnPauseRequested => pauseRequestedSubject;
        public Observable<Unit> OnResumeRequested => resumeRequestedSubject;
        public Observable<Unit> OnSuspendFinishRequested => suspendFinishRequestedSubject;
        public Observable<int> OnRoundResolutionRequested => roundResolutionRequestedSubject;
        public Observable<Unit> OnDisconnected => disconnectedSubject;

        public void PublishPhase(BattleFlowState state, int round) {
            if (!IsSessionHost) {
                return;
            }

            phaseSequence += 1;
            var snapshot = new BattleFlowPhaseSnapshot(phaseSequence, state, round);
            latestPhase.Value = snapshot;
            var payload = new PhasePayload {
                sequence = (long)snapshot.Sequence,
                phase = (int)snapshot.State,
                round = snapshot.Round,
            };
            Broadcast(OnlineBattleProtocol.PhaseKey, payload);
            Debug.Log($"{LOG_PREFIX} Published phase. sequence={snapshot.Sequence}, state={state}, round={round}");
        }

        public void RequestPause() {
            SendRequest(OnlineBattleProtocol.PauseRequestKey, new EmptyPayload());
        }

        public void RequestResume() {
            SendRequest(OnlineBattleProtocol.ResumeRequestKey, new EmptyPayload());
        }

        public void RequestSuspendFinish() {
            SendRequest(OnlineBattleProtocol.SuspendFinishRequestKey, new EmptyPayload());
        }

        public void RequestRoundResolution(int deadPlayerId) {
            SendRequest(OnlineBattleProtocol.RoundResolutionRequestKey, new RoundResolutionRequestPayload {
                deadPlayerId = deadPlayerId,
            });
        }

        public void PublishOutcome(BattleOutcomeSnapshot snapshot) {
            if (!IsSessionHost) {
                return;
            }

            outcomeSequence += 1;
            var sequencedSnapshot = snapshot with {
                Sequence = outcomeSequence,
            };
            latestOutcome.Value = sequencedSnapshot;
            var payload = new OutcomePayload {
                sequence = (long)sequencedSnapshot.Sequence,
                kind = (int)sequencedSnapshot.Kind,
                finishedRound = sequencedSnapshot.FinishedRound,
                deadPlayerId = sequencedSnapshot.DeadPlayerId,
                roundWinnerPlayerId = sequencedSnapshot.RoundWinnerPlayerId,
                continueBattle = sequencedSnapshot.ContinueBattle,
                finalWinnerPlayerId = sequencedSnapshot.FinalWinnerPlayerId,
                stopMusic = sequencedSnapshot.StopMusic,
                playerIds = sequencedSnapshot.PlayerIds,
                roundWinCounts = sequencedSnapshot.RoundWinCounts,
            };
            Broadcast(OnlineBattleProtocol.OutcomeKey, payload);
            Debug.Log($"{LOG_PREFIX} Published outcome. sequence={sequencedSnapshot.Sequence}, kind={sequencedSnapshot.Kind}, round={sequencedSnapshot.FinishedRound}, winner={sequencedSnapshot.RoundWinnerPlayerId}, finalWinner={sequencedSnapshot.FinalWinnerPlayerId}");
        }

        public void PublishBeatCommand(OnlineBeatCommandSnapshot snapshot) {
            if (!IsReady) {
                return;
            }

            beatCommandSequence += 1;
            var sequencedSnapshot = snapshot with {
                Sequence = beatCommandSequence,
            };
            var payload = BuildBeatCommandPayload(sequencedSnapshot);
            if (runner.IsServer) {
                Broadcast(OnlineBattleProtocol.BeatCommandKey, payload);
            }
            else {
                runner.SendReliableDataToServer(OnlineBattleProtocol.BeatCommandKey, Encoding.UTF8.GetBytes(JsonUtility.ToJson(payload)));
            }

            Debug.Log($"{LOG_PREFIX} Published beat command. sequence={sequencedSnapshot.Sequence}, player={sequencedSnapshot.PlayerId}, beat={sequencedSnapshot.BeatIndex}, success={sequencedSnapshot.IsSuccess}");
        }

        public void PublishStrikerPreCommandSnapshot(OnlineStrikerPreCommandSnapshot snapshot) {
            if (!IsReady) {
                return;
            }

            strikerPreCommandSnapshotSequence += 1;
            var sequencedSnapshot = snapshot with {
                Sequence = strikerPreCommandSnapshotSequence,
            };
            var payload = BuildStrikerPreCommandSnapshotPayload(sequencedSnapshot);
            if (runner.IsServer) {
                Broadcast(OnlineBattleProtocol.StrikerPreCommandSnapshotKey, payload);
            }
            else {
                runner.SendReliableDataToServer(OnlineBattleProtocol.StrikerPreCommandSnapshotKey, Encoding.UTF8.GetBytes(JsonUtility.ToJson(payload)));
            }

            Debug.Log($"{LOG_PREFIX} Published striker pre-command snapshot. sequence={sequencedSnapshot.Sequence}, player={sequencedSnapshot.PlayerId}, beat={sequencedSnapshot.ApplyBeatIndex}, sent={sequencedSnapshot.SentNetworkTime:0.000}");
        }

        public bool TryGetLatestStrikerPreCommandSnapshot(int applyBeatIndex, int playerId, out OnlineStrikerPreCommandSnapshot snapshot) {
            return latestStrikerPreCommandSnapshots.TryGetValue((applyBeatIndex, playerId), out snapshot);
        }

        public async Task<OnlineStrikerPreCommandSnapshot> WaitForStrikerPreCommandSnapshotAsync(int applyBeatIndex, int playerId, float waitTimeoutSeconds) {
            var waitUntil = NetworkTime + Mathf.Max(0f, waitTimeoutSeconds);
            while (!disconnected && NetworkTime < waitUntil) {
                if (TryGetLatestStrikerPreCommandSnapshot(applyBeatIndex, playerId, out var snapshot)) {
                    return snapshot;
                }

                await Task.Yield();
            }

            throw new TimeoutException($"Timed out waiting striker pre-command snapshot. beat={applyBeatIndex}, player={playerId}");
        }

        public void ClearStrikerPreCommandSnapshotsBefore(int beatIndex) {
            var removeKeys = new List<(int ApplyBeatIndex, int PlayerId)>();
            foreach (var pair in latestStrikerPreCommandSnapshots) {
                if (pair.Key.ApplyBeatIndex < beatIndex) {
                    removeKeys.Add(pair.Key);
                }
            }

            foreach (var key in removeKeys) {
                latestStrikerPreCommandSnapshots.Remove(key);
            }
        }

        public void RequestRoundStartReady(int round) {
            SendRequest(OnlineBattleProtocol.RoundStartReadyKey, new RoundStartReadyPayload {
                round = round,
            });
        }

        public void PublishRoundStartSchedule(int round, float startNetworkTime) {
            if (!IsSessionHost) {
                return;
            }

            roundStartSequence += 1;
            var snapshot = new OnlineRoundStartSnapshot(roundStartSequence, round, startNetworkTime);
            latestRoundStart.Value = snapshot;
            var payload = new RoundStartSchedulePayload {
                sequence = (long)snapshot.Sequence,
                round = snapshot.Round,
                startNetworkTime = snapshot.StartNetworkTime,
            };
            Broadcast(OnlineBattleProtocol.RoundStartScheduleKey, payload);
            Debug.Log($"{LOG_PREFIX} Published round start schedule. sequence={snapshot.Sequence}, round={round}, start={startNetworkTime:0.000}");
        }

        public void PublishBeatSyncResume(int beatIndex, float resumeNetworkTime, float hostPlaybackTime) {
            if (!IsSessionHost) {
                return;
            }

            beatSyncResumeSequence += 1;
            var snapshot = new OnlineBeatSyncResumeSnapshot(beatSyncResumeSequence, beatIndex, resumeNetworkTime, hostPlaybackTime);
            latestBeatSyncResume.Value = snapshot;
            var payload = new BeatSyncResumePayload {
                sequence = (long)snapshot.Sequence,
                beatIndex = snapshot.BeatIndex,
                resumeNetworkTime = snapshot.ResumeNetworkTime,
                hostPlaybackTime = snapshot.HostPlaybackTime,
            };
            Broadcast(OnlineBattleProtocol.BeatSyncResumeKey, payload);
            Debug.Log($"{LOG_PREFIX} Published beat sync resume. sequence={snapshot.Sequence}, beat={beatIndex}, resume={resumeNetworkTime:0.000}, hostPlayback={hostPlaybackTime:0.000}");
        }

        public async Task WaitForPhaseAtLeastAsync(BattleFlowState state, int round) {
            if (!IsReady || IsSessionHost) {
                return;
            }

            while (!disconnected && !IsPhaseAtLeast(latestPhase.CurrentValue, state, round)) {
                await Task.Yield();
            }
        }

        public async Task<BattleOutcomeSnapshot> WaitForOutcomeAsync(BattleOutcomeKind kind, int finishedRound) {
            while (!disconnected) {
                var snapshot = latestOutcome.CurrentValue;
                if (snapshot.Sequence > 0 && snapshot.Kind == kind && snapshot.FinishedRound >= finishedRound) {
                    return snapshot;
                }

                if (kind == BattleOutcomeKind.RoundResolved
                    && snapshot.Sequence > 0
                    && snapshot.Kind == BattleOutcomeKind.BattleFinished
                    && snapshot.FinishedRound >= finishedRound) {
                    return snapshot;
                }

                await Task.Yield();
            }

            throw new InvalidOperationException("Online battle sync disconnected while waiting for outcome.");
        }

        public async Task WaitForRoundStartReadyAsync(int round) {
            if (!IsReady || !IsSessionHost) {
                return;
            }

            while (!disconnected && latestRoundStartReadyRound < round) {
                await Task.Yield();
            }
        }

        public async Task<OnlineRoundStartSnapshot> WaitForRoundStartScheduleAsync(int round) {
            while (!disconnected) {
                var snapshot = latestRoundStart.CurrentValue;
                if (snapshot.Sequence > 0 && snapshot.Round == round) {
                    return snapshot;
                }

                await Task.Yield();
            }

            throw new InvalidOperationException("Online battle sync disconnected while waiting for round start schedule.");
        }

        public async Task<OnlineBeatSyncResumeSnapshot> WaitForBeatSyncResumeAsync(int beatIndex) {
            while (!disconnected) {
                var snapshot = latestBeatSyncResume.CurrentValue;
                if (snapshot.Sequence > 0 && snapshot.BeatIndex == beatIndex) {
                    return snapshot;
                }

                await Task.Yield();
            }

            throw new InvalidOperationException("Online battle sync disconnected while waiting for beat sync resume.");
        }

        bool TryRegisterCallbacks() {
            if (callbacksRegistered && runner != null && runner.IsRunning) {
                return true;
            }

            if (!runnerProvider.TryGetRunner(out runner)) {
                return false;
            }

            runner.AddCallbacks(this);
            callbacksRegistered = true;
            Debug.Log($"{LOG_PREFIX} Registered runner callbacks. isServer={runner.IsServer}");
            return true;
        }

        bool IsOnline() {
            return appNetworkSetting.IsOnline.CurrentValue;
        }

        void Broadcast<T>(ReliableKey key, T payload) {
            var bytes = Encoding.UTF8.GetBytes(JsonUtility.ToJson(payload));
            if (!runner.IsServer) {
                runner.SendReliableDataToServer(key, bytes);
                return;
            }

            foreach (var player in runner.ActivePlayers) {
                if (player == runner.LocalPlayer) {
                    continue;
                }

                runner.SendReliableDataToPlayer(player, key, bytes);
            }
        }

        void SendRequest<T>(ReliableKey key, T payload) {
            if (!IsReady || IsSessionHost) {
                return;
            }

            runner.SendReliableDataToServer(key, Encoding.UTF8.GetBytes(JsonUtility.ToJson(payload)));
        }

        void BroadcastBytesExcept(ReliableKey key, byte[] bytes, PlayerRef excludedPlayer) {
            foreach (var player in runner.ActivePlayers) {
                if (player == runner.LocalPlayer || player == excludedPlayer) {
                    continue;
                }

                runner.SendReliableDataToPlayer(player, key, bytes);
            }
        }

        static BeatCommandPayload BuildBeatCommandPayload(OnlineBeatCommandSnapshot snapshot) {
            return new BeatCommandPayload {
                sequence = (long)snapshot.Sequence,
                playerId = snapshot.PlayerId,
                beatIndex = snapshot.BeatIndex,
                time = snapshot.Time,
                isSuccess = snapshot.IsSuccess,
                zone = (int)snapshot.Zone,
                button = (int)snapshot.Button,
                directionX = snapshot.Direction.x,
                directionY = snapshot.Direction.y,
            };
        }

        static StrikerPreCommandSnapshotPayload BuildStrikerPreCommandSnapshotPayload(OnlineStrikerPreCommandSnapshot snapshot) {
            return new StrikerPreCommandSnapshotPayload {
                sequence = (long)snapshot.Sequence,
                applyBeatIndex = snapshot.ApplyBeatIndex,
                playerId = snapshot.PlayerId,
                hitPoint = snapshot.HitPoint,
                specialPoint = snapshot.SpecialPoint,
                positionX = snapshot.Position.x,
                positionY = snapshot.Position.y,
                positionZ = snapshot.Position.z,
                statePathId = snapshot.StatePathId,
                sentNetworkTime = snapshot.SentNetworkTime,
            };
        }

        static bool IsPhaseAtLeast(BattleFlowPhaseSnapshot snapshot, BattleFlowState state, int round) {
            if (snapshot.Round != round) {
                return snapshot.Round > round;
            }

            return GetPhaseOrder(snapshot.State) >= GetPhaseOrder(state);
        }

        static int GetPhaseOrder(BattleFlowState state) {
            return state switch {
                BattleFlowState.NotStarted => 0,
                BattleFlowState.Opening => 1,
                BattleFlowState.RoundStarting => 2,
                BattleFlowState.Playing => 3,
                BattleFlowState.Suspended => 4,
                BattleFlowState.AttentionSuspended => 4,
                BattleFlowState.TutorialSuspended => 4,
                BattleFlowState.ResolvingRound => 5,
                BattleFlowState.EndingBattle => 6,
                BattleFlowState.EndingToTitle => 6,
                BattleFlowState.Finished => 7,
                _ => 0,
            };
        }

        static string Decode(ArraySegment<byte> data) {
            if (data.Array == null) {
                throw new InvalidOperationException("Reliable data payload is empty.");
            }

            return Encoding.UTF8.GetString(data.Array, data.Offset, data.Count);
        }

        public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) {
            if (runner.IsServer && OnlineBattleProtocol.IsRelayKey(key)) {
                if (data.Array == null) {
                    throw new InvalidOperationException("Reliable data payload is empty.");
                }

                var bytes = new byte[data.Count];
                Buffer.BlockCopy(data.Array, data.Offset, bytes, 0, data.Count);
                BroadcastBytesExcept(key, bytes, player);
                Debug.Log($"{LOG_PREFIX} Relayed reliable data. key={key}, fromPlayer={player}, bytes={bytes.Length}");
                return;
            }

            if (key == OnlineBattleProtocol.PhaseKey) {
                var payload = JsonUtility.FromJson<PhasePayload>(Decode(data));
                var snapshot = new BattleFlowPhaseSnapshot((ulong)payload.sequence, (BattleFlowState)payload.phase, payload.round);
                if (snapshot.Sequence > latestPhase.CurrentValue.Sequence) {
                    latestPhase.Value = snapshot;
                    Debug.Log($"{LOG_PREFIX} Received phase. sequence={snapshot.Sequence}, state={snapshot.State}, round={snapshot.Round}");
                }
                return;
            }

            if (key == OnlineBattleProtocol.OutcomeKey) {
                var payload = JsonUtility.FromJson<OutcomePayload>(Decode(data));
                var snapshot = new BattleOutcomeSnapshot(
                    (ulong)payload.sequence,
                    (BattleOutcomeKind)payload.kind,
                    payload.finishedRound,
                    payload.deadPlayerId,
                    payload.roundWinnerPlayerId,
                    payload.continueBattle,
                    payload.finalWinnerPlayerId,
                    payload.stopMusic,
                    payload.playerIds ?? Array.Empty<int>(),
                    payload.roundWinCounts ?? Array.Empty<int>());
                if (snapshot.Sequence > latestOutcome.CurrentValue.Sequence) {
                    latestOutcome.Value = snapshot;
                    Debug.Log($"{LOG_PREFIX} Received outcome. sequence={snapshot.Sequence}, kind={snapshot.Kind}, round={snapshot.FinishedRound}");
                }
                return;
            }

            if (key == OnlineBattleProtocol.BeatCommandKey) {
                var payload = JsonUtility.FromJson<BeatCommandPayload>(Decode(data));
                var snapshot = new OnlineBeatCommandSnapshot(
                    (ulong)payload.sequence,
                    payload.playerId,
                    payload.beatIndex,
                    payload.time,
                    payload.isSuccess,
                    (BeatJudgeZone)payload.zone,
                    (GamePadButton)payload.button,
                    new Vector2(payload.directionX, payload.directionY));
                beatCommandReceivedSubject.OnNext(snapshot);
                Debug.Log($"{LOG_PREFIX} Received beat command. sequence={snapshot.Sequence}, player={snapshot.PlayerId}, beat={snapshot.BeatIndex}, success={snapshot.IsSuccess}");
                return;
            }

            if (key == OnlineBattleProtocol.StrikerPreCommandSnapshotKey) {
                var payload = JsonUtility.FromJson<StrikerPreCommandSnapshotPayload>(Decode(data));
                var snapshot = new OnlineStrikerPreCommandSnapshot(
                    (ulong)payload.sequence,
                    payload.applyBeatIndex,
                    payload.playerId,
                    payload.hitPoint,
                    payload.specialPoint,
                    new Vector3(payload.positionX, payload.positionY, payload.positionZ),
                    payload.statePathId ?? string.Empty,
                    payload.sentNetworkTime);
                StoreStrikerPreCommandSnapshot(snapshot);
                strikerPreCommandSnapshotReceivedSubject.OnNext(snapshot);
                Debug.Log($"{LOG_PREFIX} Received striker pre-command snapshot. sequence={snapshot.Sequence}, player={snapshot.PlayerId}, beat={snapshot.ApplyBeatIndex}, sent={snapshot.SentNetworkTime:0.000}");
                return;
            }

            if (key == OnlineBattleProtocol.RoundStartScheduleKey) {
                var payload = JsonUtility.FromJson<RoundStartSchedulePayload>(Decode(data));
                var snapshot = new OnlineRoundStartSnapshot(
                    (ulong)payload.sequence,
                    payload.round,
                    payload.startNetworkTime);
                if (snapshot.Sequence > latestRoundStart.CurrentValue.Sequence) {
                    latestRoundStart.Value = snapshot;
                    Debug.Log($"{LOG_PREFIX} Received round start schedule. sequence={snapshot.Sequence}, round={snapshot.Round}, start={snapshot.StartNetworkTime:0.000}");
                }
                return;
            }

            if (key == OnlineBattleProtocol.BeatSyncResumeKey) {
                var payload = JsonUtility.FromJson<BeatSyncResumePayload>(Decode(data));
                var snapshot = new OnlineBeatSyncResumeSnapshot(
                    (ulong)payload.sequence,
                    payload.beatIndex,
                    payload.resumeNetworkTime,
                    payload.hostPlaybackTime);
                if (snapshot.Sequence > latestBeatSyncResume.CurrentValue.Sequence) {
                    latestBeatSyncResume.Value = snapshot;
                    Debug.Log($"{LOG_PREFIX} Received beat sync resume. sequence={snapshot.Sequence}, beat={snapshot.BeatIndex}, resume={snapshot.ResumeNetworkTime:0.000}, hostPlayback={snapshot.HostPlaybackTime:0.000}");
                }
                return;
            }

            if (key == OnlineBattleProtocol.PauseRequestKey) {
                pauseRequestedSubject.OnNext(Unit.Default);
                return;
            }

            if (key == OnlineBattleProtocol.ResumeRequestKey) {
                resumeRequestedSubject.OnNext(Unit.Default);
                return;
            }

            if (key == OnlineBattleProtocol.SuspendFinishRequestKey) {
                suspendFinishRequestedSubject.OnNext(Unit.Default);
                return;
            }

            if (key == OnlineBattleProtocol.RoundResolutionRequestKey) {
                var payload = JsonUtility.FromJson<RoundResolutionRequestPayload>(Decode(data));
                roundResolutionRequestedSubject.OnNext(payload.deadPlayerId);
                return;
            }

            if (key == OnlineBattleProtocol.RoundStartReadyKey) {
                var payload = JsonUtility.FromJson<RoundStartReadyPayload>(Decode(data));
                if (payload.round > latestRoundStartReadyRound) {
                    latestRoundStartReadyRound = payload.round;
                }
                Debug.Log($"{LOG_PREFIX} Received round start ready. round={payload.round}");
            }
        }

        public void OnPlayerLeft(NetworkRunner runner, PlayerRef player) {
            disconnected = true;
            disconnectedSubject.OnNext(Unit.Default);
        }

        public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason) {
            if (shutdownReason != ShutdownReason.Ok) {
                disconnected = true;
                disconnectedSubject.OnNext(Unit.Default);
            }
        }

        public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) {
            disconnected = true;
            disconnectedSubject.OnNext(Unit.Default);
        }

        public void Dispose() {
            if (callbacksRegistered && runner != null) {
                runner.RemoveCallbacks(this);
            }

            latestPhase.Dispose();
            latestOutcome.Dispose();
            latestRoundStart.Dispose();
            latestBeatSyncResume.Dispose();
            beatCommandReceivedSubject.Dispose();
            strikerPreCommandSnapshotReceivedSubject.Dispose();
            pauseRequestedSubject.Dispose();
            resumeRequestedSubject.Dispose();
            suspendFinishRequestedSubject.Dispose();
            roundResolutionRequestedSubject.Dispose();
            disconnectedSubject.Dispose();
        }

        public void OnPlayerJoined(NetworkRunner runner, PlayerRef player) { }
        public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
        public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
        public void OnInput(NetworkRunner runner, NetworkInput input) { }
        public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
        public void OnConnectedToServer(NetworkRunner runner) { }
        public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }
        public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
        public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
        public void OnSceneLoadDone(NetworkRunner runner) { }
        public void OnSceneLoadStart(NetworkRunner runner) { }
        public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
        public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
        public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) {
            disconnected = true;
            disconnectedSubject.OnNext(Unit.Default);
        }

        public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) {
            request.Accept();
        }

        void StoreStrikerPreCommandSnapshot(OnlineStrikerPreCommandSnapshot snapshot) {
            var key = (snapshot.ApplyBeatIndex, snapshot.PlayerId);
            if (latestStrikerPreCommandSnapshots.TryGetValue(key, out var current)
                && current.SentNetworkTime > snapshot.SentNetworkTime) {
                return;
            }

            latestStrikerPreCommandSnapshots[key] = snapshot;
        }

        [Serializable]
        class EmptyPayload { }

        [Serializable]
        class PhasePayload {
            public long sequence;
            public int phase;
            public int round;
        }

        [Serializable]
        class OutcomePayload {
            public long sequence;
            public int kind;
            public int finishedRound;
            public int deadPlayerId;
            public int roundWinnerPlayerId;
            public bool continueBattle;
            public int finalWinnerPlayerId;
            public bool stopMusic;
            public int[] playerIds;
            public int[] roundWinCounts;
        }

        [Serializable]
        class RoundResolutionRequestPayload {
            public int deadPlayerId;
        }

        [Serializable]
        class BeatCommandPayload {
            public long sequence;
            public int playerId;
            public int beatIndex;
            public float time;
            public bool isSuccess;
            public int zone;
            public int button;
            public float directionX;
            public float directionY;
        }

        [Serializable]
        class StrikerPreCommandSnapshotPayload {
            public long sequence;
            public int applyBeatIndex;
            public int playerId;
            public float hitPoint;
            public float specialPoint;
            public float positionX;
            public float positionY;
            public float positionZ;
            public string statePathId;
            public float sentNetworkTime;
        }

        [Serializable]
        class RoundStartReadyPayload {
            public int round;
        }

        [Serializable]
        class RoundStartSchedulePayload {
            public long sequence;
            public int round;
            public float startNetworkTime;
        }

        [Serializable]
        class BeatSyncResumePayload {
            public long sequence;
            public int beatIndex;
            public float resumeNetworkTime;
            public float hostPlaybackTime;
        }
    }
}
