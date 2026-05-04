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

    public record OnlineSuspendMenuBeatSnapshot(int ApplyBeatIndex, int PlayerId);

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
        Observable<OnlineSuspendMenuBeatSnapshot> OnSuspendMenuBeatReceived { get; }
        void ResetOnlineBattleFlowSyncState();
        Task PassFlowGateAsync(BattleFlowSyncGate gate, int round, int subIndex = 0);
        void PublishRoundStartReadyWithTime(int round, float readyNetworkTime);
        Task<float> WaitSymmetricRoundStartNetworkTimeAsync(int round, float leadSeconds, float minLeadSeconds);
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
        void PublishSuspendMenuBeatRequest(int applyBeatIndex);
        bool TryConsumeDualSuspendMenuRequests(int applyBeatIndex);
        void PublishResumeAck(float ackNetworkTime);
        Task<float> WaitSymmetricResumeNetworkTimeAsync(float resumeLeadSeconds, float minLeadSeconds);
        void ClearResumeAckState();
    }

    public class BattleOnlineSync : IBattleOnlineSync, INetworkRunnerCallbacks, IDisposable {
        const string LOG_PREFIX = "[BattleOnlineSync]";
        public const float DefaultFlowGateTimeoutSeconds = 90f;

        readonly INetworkRunnerProvider runnerProvider;
        readonly IAppNetworkSetting appNetworkSetting;
        readonly ReactiveProperty<BattleFlowPhaseSnapshot> latestPhase = new(new BattleFlowPhaseSnapshot(0, BattleFlowState.NotStarted, 0));
        readonly ReactiveProperty<BattleOutcomeSnapshot> latestOutcome = new(new BattleOutcomeSnapshot(0, 0, 0, -1, -1, false, -1, false, Array.Empty<int>(), Array.Empty<int>()));
        readonly ReactiveProperty<OnlineRoundStartSnapshot> latestRoundStart = new(new OnlineRoundStartSnapshot(0, 0, 0));
        readonly ReactiveProperty<OnlineBeatSyncResumeSnapshot> latestBeatSyncResume = new(new OnlineBeatSyncResumeSnapshot(0, -1, 0, 0));
        readonly Subject<OnlineBeatCommandSnapshot> beatCommandReceivedSubject = new();
        readonly Subject<OnlineStrikerPreCommandSnapshot> strikerPreCommandSnapshotReceivedSubject = new();
        readonly Subject<OnlineSuspendMenuBeatSnapshot> suspendMenuBeatReceivedSubject = new();
        readonly Dictionary<(int ApplyBeatIndex, int PlayerId), OnlineStrikerPreCommandSnapshot> latestStrikerPreCommandSnapshots = new();
        readonly Subject<Unit> pauseRequestedSubject = new();
        readonly Subject<Unit> resumeRequestedSubject = new();
        readonly Subject<Unit> suspendFinishRequestedSubject = new();
        readonly Subject<int> roundResolutionRequestedSubject = new();
        readonly Subject<Unit> disconnectedSubject = new();
        readonly Dictionary<(int Gate, int Round, int Sub), int> flowGateArrivalMask = new();
        readonly Dictionary<int, int> roundStartReadyMaskByRound = new();
        readonly Dictionary<int, float> roundStartReadyTimePlayer0 = new();
        readonly Dictionary<int, float> roundStartReadyTimePlayer1 = new();
        readonly Dictionary<int, int> suspendMenuBeatMaskByBeat = new();
        ulong flowGateEmitSequence;
        int resumeAckMask;
        float resumeAckTimePlayer0;
        float resumeAckTimePlayer1;
        NetworkRunner runner;
        ulong phaseSequence;
        ulong outcomeWireSequence;
        ulong acceptedOutcomeSequence;
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
        public Observable<OnlineSuspendMenuBeatSnapshot> OnSuspendMenuBeatReceived => suspendMenuBeatReceivedSubject;
        public Observable<Unit> OnPauseRequested => pauseRequestedSubject;
        public Observable<Unit> OnResumeRequested => resumeRequestedSubject;
        public Observable<Unit> OnSuspendFinishRequested => suspendFinishRequestedSubject;
        public Observable<int> OnRoundResolutionRequested => roundResolutionRequestedSubject;
        public Observable<Unit> OnDisconnected => disconnectedSubject;

        // バトル開始時にゲート・ラウンド開始 ready・サスペンド/再開の途中状態だけを捨てる（アウトカムやフェーズの履歴は別途）。
        public void ResetOnlineBattleFlowSyncState() {
            flowGateArrivalMask.Clear();
            roundStartReadyMaskByRound.Clear();
            roundStartReadyTimePlayer0.Clear();
            roundStartReadyTimePlayer1.Clear();
            suspendMenuBeatMaskByBeat.Clear();
            resumeAckMask = 0;
            resumeAckTimePlayer0 = 0f;
            resumeAckTimePlayer1 = 0f;
            latestRoundStartReadyRound = 0;
        }

        // 双方が同じ (gate, round, subIndex) に到達するまでブロック。先に着いた側は相手の FlowGate 受信でマスクが埋まるまで待つ。
        // タイムアウト時は切断扱いにしてタイトル遷移など既存の OnDisconnected 経路に寄せる。
        public async Task PassFlowGateAsync(BattleFlowSyncGate gate, int round, int subIndex = 0) {
            if (!IsReady || !IsOnline()) {
                return;
            }

            var key = ((int)gate, round, subIndex);
            if (!flowGateArrivalMask.TryGetValue(key, out var mask)) {
                mask = 0;
            }

            var localId = Mathf.Clamp(appNetworkSetting.LocalOnlinePlayerId, 0, 1);
            mask |= 1 << localId;
            flowGateArrivalMask[key] = mask;
            flowGateEmitSequence += 1;
            Broadcast(OnlineBattleProtocol.FlowGateKey, new FlowGatePayload {
                gate = (int)gate,
                round = round,
                subIndex = subIndex,
                playerId = localId,
            });
            var waitUntil = NetworkTime + DefaultFlowGateTimeoutSeconds;
            while (!disconnected && NetworkTime < waitUntil) {
                if (flowGateArrivalMask.TryGetValue(key, out var m) && m == 0b11) {
                    return;
                }

                await Task.Yield();
            }

            if (!disconnected) {
                disconnected = true;
                disconnectedSubject.OnNext(Unit.Default);
            }
        }

        // 各ピアが「このラウンドで再生開始の準備ができた時刻」を送る。PlayerId 0/1 別に保持し、揃ったら WaitSymmetric で同一式を評価する。
        public void PublishRoundStartReadyWithTime(int round, float readyNetworkTime) {
            if (!IsReady || !IsOnline()) {
                return;
            }

            var localId = Mathf.Clamp(appNetworkSetting.LocalOnlinePlayerId, 0, 1);
            if (!roundStartReadyMaskByRound.TryGetValue(round, out var mask)) {
                mask = 0;
            }

            mask |= 1 << localId;
            roundStartReadyMaskByRound[round] = mask;
            if (localId == 0) {
                roundStartReadyTimePlayer0[round] = readyNetworkTime;
            }
            else {
                roundStartReadyTimePlayer1[round] = readyNetworkTime;
            }

            Broadcast(OnlineBattleProtocol.RoundStartReadyKey, new RoundStartReadyPayload {
                round = round,
                readyNetworkTime = readyNetworkTime,
                playerId = localId,
            });
            if (round > latestRoundStartReadyRound) {
                latestRoundStartReadyRound = round;
            }

            Debug.Log($"{LOG_PREFIX} Published round start ready. round={round}, readyTime={readyNetworkTime:0.000}, player={localId}");
        }

        // 両者の readyNetworkTime を受信済みとみなしたうえで、max(t0,t1)+lead を NetworkTime 軸の再生開始時刻とする。minLead で過去クリップし非対称を防ぐ。
        public async Task<float> WaitSymmetricRoundStartNetworkTimeAsync(int round, float leadSeconds, float minLeadSeconds) {
            if (!IsReady || !IsOnline()) {
                return 0f;
            }

            var waitUntil = NetworkTime + DefaultFlowGateTimeoutSeconds;
            while (!disconnected && NetworkTime < waitUntil) {
                if (roundStartReadyMaskByRound.TryGetValue(round, out var mask) && mask == 0b11) {
                    roundStartReadyTimePlayer0.TryGetValue(round, out var t0);
                    roundStartReadyTimePlayer1.TryGetValue(round, out var t1);
                    var agreed = Mathf.Max(t0, t1) + leadSeconds;
                    var floor = NetworkTime + Mathf.Max(0f, minLeadSeconds);
                    return Mathf.Max(agreed, floor);
                }

                await Task.Yield();
            }

            throw new InvalidOperationException("Online battle sync disconnected or timed out while waiting for round start readiness.");
        }

        // ローカルが「この applyBeatIndex でサスペンド要求した」ビットを立てつつ相手へ中継。相手分が揃ったら BeatJudge が同一拍でポーズを実行する。
        public void PublishSuspendMenuBeatRequest(int applyBeatIndex) {
            if (!IsReady || !IsOnline()) {
                return;
            }

            var localId = Mathf.Clamp(appNetworkSetting.LocalOnlinePlayerId, 0, 1);
            if (!suspendMenuBeatMaskByBeat.TryGetValue(applyBeatIndex, out var mask)) {
                mask = 0;
            }

            mask |= 1 << localId;
            suspendMenuBeatMaskByBeat[applyBeatIndex] = mask;
            Broadcast(OnlineBattleProtocol.SuspendMenuBeatKey, new SuspendMenuBeatPayload {
                applyBeatIndex = applyBeatIndex,
                playerId = localId,
            });
            suspendMenuBeatReceivedSubject.OnNext(new OnlineSuspendMenuBeatSnapshot(applyBeatIndex, localId));
            Debug.Log($"{LOG_PREFIX} Published suspend menu beat request. beat={applyBeatIndex}, player={localId}");
        }

        // 指定拍について双方のサスペンド要求が揃っているときだけ true を返し、マスクを消費する（二重適用防止）。
        public bool TryConsumeDualSuspendMenuRequests(int applyBeatIndex) {
            if (!suspendMenuBeatMaskByBeat.TryGetValue(applyBeatIndex, out var mask) || mask != 0b11) {
                return false;
            }

            suspendMenuBeatMaskByBeat.Remove(applyBeatIndex);
            return true;
        }

        // 解除操作を送った側が「解除 ACK」を出す。双方分の ackNetworkTime が揃うまで WaitSymmetricResumeNetworkTimeAsync で待つ。
        public void PublishResumeAck(float ackNetworkTime) {
            if (!IsReady || !IsOnline()) {
                return;
            }

            var localId = Mathf.Clamp(appNetworkSetting.LocalOnlinePlayerId, 0, 1);
            resumeAckMask |= 1 << localId;
            if (localId == 0) {
                resumeAckTimePlayer0 = ackNetworkTime;
            }
            else {
                resumeAckTimePlayer1 = ackNetworkTime;
            }

            Broadcast(OnlineBattleProtocol.ResumeAckKey, new ResumeAckPayload {
                playerId = localId,
                ackNetworkTime = ackNetworkTime,
            });
            Debug.Log($"{LOG_PREFIX} Published resume ack. player={localId}, time={ackNetworkTime:0.000}");
        }

        public void ClearResumeAckState() {
            resumeAckMask = 0;
            resumeAckTimePlayer0 = 0f;
            resumeAckTimePlayer1 = 0f;
        }

        // ラウンド開始と同様、双方の ACK 時刻の max に余裕を足した resumeNetworkTime を決定（NetworkTime のみで合意）。
        public async Task<float> WaitSymmetricResumeNetworkTimeAsync(float resumeLeadSeconds, float minLeadSeconds) {
            if (!IsReady || !IsOnline()) {
                return NetworkTime + minLeadSeconds;
            }

            var waitUntil = NetworkTime + DefaultFlowGateTimeoutSeconds;
            while (!disconnected && NetworkTime < waitUntil) {
                if (resumeAckMask == 0b11) {
                    var agreed = Mathf.Max(resumeAckTimePlayer0, resumeAckTimePlayer1) + resumeLeadSeconds;
                    var floor = NetworkTime + Mathf.Max(0f, minLeadSeconds);
                    return Mathf.Max(agreed, floor);
                }

                await Task.Yield();
            }

            throw new InvalidOperationException("Online battle sync disconnected or timed out while waiting for resume acks.");
        }

        public void PublishPhase(BattleFlowState state, int round) {
            if (!IsReady || !IsOnline()) {
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

        // 送信前にローカル権威マージを通す。先に採用済みのラウンド/内容より遅い・矛盾する送信は弾かれ、先着アウトカムを維持する。
        public void PublishOutcome(BattleOutcomeSnapshot snapshot) {
            if (!IsReady || !IsOnline()) {
                return;
            }

            if (!TryMergeOutcomeAuthoritative(snapshot, out var merged)) {
                return;
            }

            outcomeWireSequence += 1;
            var payload = new OutcomePayload {
                sequence = (long)outcomeWireSequence,
                kind = (int)merged.Kind,
                finishedRound = merged.FinishedRound,
                deadPlayerId = merged.DeadPlayerId,
                roundWinnerPlayerId = merged.RoundWinnerPlayerId,
                continueBattle = merged.ContinueBattle,
                finalWinnerPlayerId = merged.FinalWinnerPlayerId,
                stopMusic = merged.StopMusic,
                playerIds = merged.PlayerIds,
                roundWinCounts = merged.RoundWinCounts,
            };
            Broadcast(OnlineBattleProtocol.OutcomeKey, payload);
            Debug.Log($"{LOG_PREFIX} Published outcome. wireSeq={outcomeWireSequence}, kind={merged.Kind}, round={merged.FinishedRound}");
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
            PublishRoundStartReadyWithTime(round, NetworkTime);
        }

        public void PublishRoundStartSchedule(int round, float startNetworkTime) {
            if (!IsReady || !IsOnline()) {
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
            if (!IsReady || !IsOnline()) {
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
            if (!IsReady || !IsOnline()) {
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
            if (!IsReady || !IsOnline()) {
                return;
            }

            var waitUntil = NetworkTime + DefaultFlowGateTimeoutSeconds;
            while (!disconnected && NetworkTime < waitUntil) {
                if (roundStartReadyMaskByRound.TryGetValue(round, out var mask) && mask == 0b11) {
                    return;
                }

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
            if (!runnerProvider.TryGetRunner(out var fromProvider) || fromProvider == null || !fromProvider.IsRunning) {
                if (callbacksRegistered && runner != null) {
                    runner.RemoveCallbacks(this);
                    callbacksRegistered = false;
                }

                runner = null;
                return false;
            }

            if (callbacksRegistered && runner != null && (!ReferenceEquals(runner, fromProvider) || !runner.IsRunning)) {
                runner.RemoveCallbacks(this);
                callbacksRegistered = false;
            }

            runner = fromProvider;
            if (!callbacksRegistered) {
                runner.AddCallbacks(this);
                callbacksRegistered = true;
                Debug.Log($"{LOG_PREFIX} Registered runner callbacks. isServer={runner.IsServer}");
            }

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
            if (!IsReady || runner.IsServer) {
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

        // 受信・送信の両方で「いま正とするアウトカム」を一本化。同一ラウンドの重複は冪等、RoundResolved から BattleFinished への更新のみ許可する。
        bool TryMergeOutcomeAuthoritative(BattleOutcomeSnapshot incoming, out BattleOutcomeSnapshot merged) {
            merged = incoming;
            var cur = latestOutcome.CurrentValue;
            if (cur.Sequence == 0) {
                acceptedOutcomeSequence += 1;
                merged = incoming with {
                    Sequence = acceptedOutcomeSequence,
                };
                latestOutcome.Value = merged;
                return true;
            }

            if (incoming.FinishedRound < cur.FinishedRound) {
                return false;
            }

            if (incoming.FinishedRound == cur.FinishedRound) {
                if (incoming.Kind == cur.Kind && OutcomesEquivalent(cur, incoming)) {
                    return false;
                }

                if (cur.Kind == BattleOutcomeKind.RoundResolved
                    && incoming.Kind == BattleOutcomeKind.BattleFinished) {
                    acceptedOutcomeSequence += 1;
                    merged = incoming with {
                        Sequence = acceptedOutcomeSequence,
                    };
                    latestOutcome.Value = merged;
                    return true;
                }

                return false;
            }

            acceptedOutcomeSequence += 1;
            merged = incoming with {
                Sequence = acceptedOutcomeSequence,
            };
            latestOutcome.Value = merged;
            return true;
        }

        static bool OutcomesEquivalent(BattleOutcomeSnapshot a, BattleOutcomeSnapshot b) {
            return a.Kind == b.Kind
                && a.FinishedRound == b.FinishedRound
                && a.DeadPlayerId == b.DeadPlayerId
                && a.RoundWinnerPlayerId == b.RoundWinnerPlayerId
                && a.ContinueBattle == b.ContinueBattle
                && a.FinalWinnerPlayerId == b.FinalWinnerPlayerId
                && a.StopMusic == b.StopMusic;
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

        // 相手から届いた FlowGate をローカルマスクに OR する。PassFlowGateAsync は自プレイヤー分も OR 済みなので 0b11 で解放。
        void RecordFlowGateRemoteArrival(int gate, int round, int subIndex, int playerId) {
            var key = (gate, round, subIndex);
            if (!flowGateArrivalMask.TryGetValue(key, out var mask)) {
                mask = 0;
            }

            playerId = Mathf.Clamp(playerId, 0, 1);
            mask |= 1 << playerId;
            flowGateArrivalMask[key] = mask;
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

            if (key == OnlineBattleProtocol.FlowGateKey) {
                var payload = JsonUtility.FromJson<FlowGatePayload>(Decode(data));
                RecordFlowGateRemoteArrival(payload.gate, payload.round, payload.subIndex, payload.playerId);
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
                if (TryMergeOutcomeAuthoritative(snapshot, out var merged)) {
                    Debug.Log($"{LOG_PREFIX} Received outcome. acceptedSeq={merged.Sequence}, kind={merged.Kind}, round={merged.FinishedRound}");
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
                var pid = Mathf.Clamp(payload.playerId, 0, 1);
                if (!roundStartReadyMaskByRound.TryGetValue(payload.round, out var mask)) {
                    mask = 0;
                }

                mask |= 1 << pid;
                roundStartReadyMaskByRound[payload.round] = mask;
                if (pid == 0) {
                    roundStartReadyTimePlayer0[payload.round] = payload.readyNetworkTime;
                }
                else {
                    roundStartReadyTimePlayer1[payload.round] = payload.readyNetworkTime;
                }

                if (payload.round > latestRoundStartReadyRound) {
                    latestRoundStartReadyRound = payload.round;
                }

                Debug.Log($"{LOG_PREFIX} Received round start ready. round={payload.round}, player={pid}, ready={payload.readyNetworkTime:0.000}");
                return;
            }

            if (key == OnlineBattleProtocol.SuspendMenuBeatKey) {
                var payload = JsonUtility.FromJson<SuspendMenuBeatPayload>(Decode(data));
                var pid = Mathf.Clamp(payload.playerId, 0, 1);
                if (!suspendMenuBeatMaskByBeat.TryGetValue(payload.applyBeatIndex, out var m)) {
                    m = 0;
                }

                m |= 1 << pid;
                suspendMenuBeatMaskByBeat[payload.applyBeatIndex] = m;
                suspendMenuBeatReceivedSubject.OnNext(new OnlineSuspendMenuBeatSnapshot(payload.applyBeatIndex, pid));
                return;
            }

            if (key == OnlineBattleProtocol.ResumeAckKey) {
                var payload = JsonUtility.FromJson<ResumeAckPayload>(Decode(data));
                var pid = Mathf.Clamp(payload.playerId, 0, 1);
                resumeAckMask |= 1 << pid;
                if (pid == 0) {
                    resumeAckTimePlayer0 = payload.ackNetworkTime;
                }
                else {
                    resumeAckTimePlayer1 = payload.ackNetworkTime;
                }

                return;
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
            suspendMenuBeatReceivedSubject.Dispose();
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
        class FlowGatePayload {
            public int gate;
            public int round;
            public int subIndex;
            public int playerId;
        }

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
            public float readyNetworkTime;
            public int playerId;
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

        [Serializable]
        class SuspendMenuBeatPayload {
            public int applyBeatIndex;
            public int playerId;
        }

        [Serializable]
        class ResumeAckPayload {
            public int playerId;
            public float ackNetworkTime;
        }
    }
}
