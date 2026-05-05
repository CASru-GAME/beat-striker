using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Fusion;
using Fusion.Sockets;
using R3;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Alice {
    public interface IOnlineSessionBootstrap {
        Task<OnlineMatchResult> MatchAsync(OnlineMatchRequest request);
        void CancelMatchmaking();
        Task TeardownOnlineRunnerAsync();
    }

    public interface INetworkRunnerProvider {
        bool TryGetRunner(out NetworkRunner runner);
    }

    public interface IOnlineDuelFusionClient {
        ReadOnlyReactiveProperty<OnlineDuelUiState> State { get; }
        bool HasReservation { get; }
        string ReservationId { get; }
        int LastSceneSyncId { get; }
        Task NotifySceneReadyAsync(AppScene scene);
        Task NotifyPlayerStatusAsync(OnlineDuelPlayerStatus status);
        void InviteCandidate();
        void SkipCandidate();
        void AcceptInvite();
        void RejectInvite();
        void CancelInvite();
        void ConsumeReservation();
    }

    public class OnlineDuelFusionClient :
        IOnlineDuelFusionClient,
        IOnlineSessionBootstrap,
        INetworkRunnerProvider,
        INetworkRunnerCallbacks,
        IInitializable,
        IDisposable {
        const string LOG_PREFIX = "[OnlineDuelFusionClient]";
        const int LobbyPlayerCount = 32;
        const float WaitHeartbeatIntervalSeconds = 1f;
        const float PresenceHeartbeatIntervalSeconds = 30f;
        const float RunnerReleaseWaitTimeoutSeconds = 30f;

        readonly IAppNetworkSetting networkSetting;
        readonly IOnlineDuelIdentity identity;
        readonly ReactiveProperty<OnlineDuelUiState> state;
        readonly CompositeDisposable disposables = new();

        NetworkRunner runner;
        OnlineSessionMainThreadQueue mainThreadQueue;
        Task startRunnerTask;
        TaskCompletionSource<OnlineMatchResult> matchCompletion;
        OnlineMatchRequest localMatchRequest;
        bool cancellationRequested;
        int matchLogSequence;
        int currentMatchLogId;
        AppScene currentScene = AppScene.Title;
        OnlineDuelPlayerStatus currentPlayerStatus = OnlineDuelPlayerStatus.StageSelecting;
        int sceneSyncSequence;
        int lastSceneSyncId;
        float lastPresenceHeartbeatAt;

        public ReadOnlyReactiveProperty<OnlineDuelUiState> State => state;
        public bool HasReservation => state.CurrentValue.HasReservation;
        public string ReservationId => state.CurrentValue.ReservationId;
        public int LastSceneSyncId => lastSceneSyncId;

        [Inject]
        public OnlineDuelFusionClient(IAppNetworkSetting networkSetting, IOnlineDuelIdentity identity) {
            this.networkSetting = networkSetting;
            this.identity = identity;
            state = new ReactiveProperty<OnlineDuelUiState>(OnlineDuelUiState.Idle(identity.DuelSessionId));
        }

        public void Initialize() {
            networkSetting.IsOnline.Subscribe(__ => {
                _ = EnsureRunnerStartedAsync("online setting changed");
            }).AddTo(disposables);
            Observable.EveryUpdate().Subscribe(_ => TickPresenceHeartbeat()).AddTo(disposables);
            _ = EnsureRunnerStartedAsync("initialize");
        }

        public bool TryGetRunner(out NetworkRunner runner) {
            runner = this.runner;
            return runner != null && runner.IsRunning;
        }

        public async Task NotifySceneReadyAsync(AppScene scene) {
            currentScene = scene;
            currentPlayerStatus = ResolveInitialPlayerStatus(scene);
            if (IsBattleScene(scene)) {
                return;
            }

            sceneSyncSequence += 1;
            lastSceneSyncId = sceneSyncSequence;
            await EnsureRunnerStartedAsync($"scene ready {scene}");
            SendCommand(new OnlineDuelCommandPayload {
                kind = (int)OnlineDuelCommandKind.PresenceUpdate,
                duelSessionId = identity.DuelSessionId,
                scene = scene.ToString(),
                sceneSyncId = lastSceneSyncId,
            });
        }

        public async Task NotifyPlayerStatusAsync(OnlineDuelPlayerStatus status) {
            currentPlayerStatus = status;
            if (IsBattleScene(currentScene)) {
                return;
            }

            await EnsureRunnerStartedAsync($"player status {status}");
            SendCommand(new OnlineDuelCommandPayload {
                kind = (int)OnlineDuelCommandKind.PresenceUpdate,
                duelSessionId = identity.DuelSessionId,
                scene = currentScene.ToString(),
            });
        }

        public void InviteCandidate() {
            var current = state.CurrentValue;
            if (string.IsNullOrWhiteSpace(current.CandidateSessionId)) {
                return;
            }

            SendCommand(new OnlineDuelCommandPayload {
                kind = (int)OnlineDuelCommandKind.InviteCreate,
                duelSessionId = identity.DuelSessionId,
                targetSessionId = current.CandidateSessionId,
            });
            state.OnNext(current with {
                Phase = OnlineDuelPhase.InviteSent,
                OpponentSessionId = current.CandidateSessionId,
                Message = "",
            });
        }

        public void SkipCandidate() {
            var current = state.CurrentValue;
            if (current.Phase == OnlineDuelPhase.CandidateShown) {
                state.OnNext(OnlineDuelUiState.Idle(identity.DuelSessionId));
            }
        }

        public void AcceptInvite() {
            var current = state.CurrentValue;
            if (string.IsNullOrWhiteSpace(current.InviteId)) {
                return;
            }

            SendCommand(new OnlineDuelCommandPayload {
                kind = (int)OnlineDuelCommandKind.InviteAccept,
                duelSessionId = identity.DuelSessionId,
                inviteId = current.InviteId,
            });
        }

        public void RejectInvite() {
            var current = state.CurrentValue;
            if (string.IsNullOrWhiteSpace(current.InviteId)) {
                return;
            }

            SendCommand(new OnlineDuelCommandPayload {
                kind = (int)OnlineDuelCommandKind.InviteReject,
                duelSessionId = identity.DuelSessionId,
                inviteId = current.InviteId,
            });
            state.OnNext(OnlineDuelUiState.Idle(identity.DuelSessionId));
        }

        public void CancelInvite() {
            var current = state.CurrentValue;
            if (string.IsNullOrWhiteSpace(current.InviteId)) {
                return;
            }

            SendCommand(new OnlineDuelCommandPayload {
                kind = (int)OnlineDuelCommandKind.InviteCancel,
                duelSessionId = identity.DuelSessionId,
                inviteId = current.InviteId,
            });
            state.OnNext(OnlineDuelUiState.Idle(identity.DuelSessionId));
        }

        public void ConsumeReservation() {
            var current = state.CurrentValue;
            if (string.IsNullOrWhiteSpace(current.ReservationId)) {
                return;
            }

            currentPlayerStatus = OnlineDuelPlayerStatus.Waiting;
            SendCommand(new OnlineDuelCommandPayload {
                kind = (int)OnlineDuelCommandKind.ReservationConsume,
                duelSessionId = identity.DuelSessionId,
                reservationId = current.ReservationId,
            });
            state.OnNext(current with {
                Phase = OnlineDuelPhase.Consumed,
            });
        }

        public async Task<OnlineMatchResult> MatchAsync(OnlineMatchRequest request) {
            if (matchCompletion != null && !matchCompletion.Task.IsCompleted) {
                throw new InvalidOperationException("Online matchmaking is already running.");
            }

            await EnsureRunnerStartedAsync("MatchAsync");

            matchLogSequence++;
            currentMatchLogId = matchLogSequence;
            var mid = currentMatchLogId;
            localMatchRequest = request;
            cancellationRequested = false;
            matchCompletion = new TaskCompletionSource<OnlineMatchResult>();
            currentPlayerStatus = OnlineDuelPlayerStatus.Waiting;
            var deadline = Time.realtimeSinceStartup + networkSetting.MatchTimeoutSeconds;
            var currentState = state.CurrentValue;
            state.OnNext(currentState with {
                Phase = OnlineDuelPhase.Matching,
                ReservationId = request.ReservationId,
                OpponentSessionId = string.IsNullOrWhiteSpace(currentState.OpponentSessionId)
                    ? currentState.CandidateSessionId
                    : currentState.OpponentSessionId,
                MatchDeadlineRealtime = deadline,
            });

            SendCommand(new OnlineDuelCommandPayload {
                kind = (int)OnlineDuelCommandKind.MatchRequest,
                duelSessionId = identity.DuelSessionId,
                reservationId = request.ReservationId,
                striker = (int)request.LocalStriker,
                stage = (int)request.CandidateStage,
                musicId = request.CandidateMusicId,
            });

            Debug.Log($"{LOG_PREFIX} [match#{mid}] Match request sent. reservationId={request.ReservationId}, striker={request.LocalStriker}, stage={request.CandidateStage}, musicId={request.CandidateMusicId}");
            return await WaitForMatchAsync(mid, deadline);
        }

        public void CancelMatchmaking() {
            if (matchCompletion == null || matchCompletion.Task.IsCompleted) {
                return;
            }

            cancellationRequested = true;
            SendCommand(new OnlineDuelCommandPayload {
                kind = (int)OnlineDuelCommandKind.MatchCancel,
                duelSessionId = identity.DuelSessionId,
                reservationId = localMatchRequest.ReservationId,
            });
            matchCompletion.TrySetException(new OperationCanceledException("Online matchmaking canceled by player."));
            state.OnNext(OnlineDuelUiState.Idle(identity.DuelSessionId));
        }

        public async Task TeardownOnlineRunnerAsync() {
            matchCompletion?.TrySetException(new OperationCanceledException("Online runner was torn down."));
            state.OnNext(OnlineDuelUiState.Idle(identity.DuelSessionId));
            if (runner == null || !runner.IsRunning) {
                return;
            }

            await runner.Shutdown();
            await WaitUntilRunnerReleasedAsync("TeardownOnlineRunnerAsync");
        }

        async Task EnsureRunnerStartedAsync(string context) {
            if (runner != null && runner.IsRunning) {
                return;
            }

            if (startRunnerTask != null && !startRunnerTask.IsCompleted) {
                await startRunnerTask;
                return;
            }

            startRunnerTask = StartRunnerAsync(context);
            await startRunnerTask;
        }

        async Task StartRunnerAsync(string context) {
            ReleaseRunner($"StartRunnerAsync replace stale. context={context}");

            var runnerObject = new GameObject("OnlineDuelFusionRunner");
            UnityEngine.Object.DontDestroyOnLoad(runnerObject);
            mainThreadQueue = runnerObject.AddComponent<OnlineSessionMainThreadQueue>();
            runner = runnerObject.AddComponent<NetworkRunner>();
            runner.AddCallbacks(this);

            var projectConfig = NetworkProjectConfig.Deserialize(
                NetworkProjectConfig.Serialize(NetworkProjectConfig.Global));
            var simulation = projectConfig.Simulation;
            simulation.Topology = Topologies.ClientServer;
            projectConfig.Simulation = simulation;

            var startResult = await runner.StartGame(new StartGameArgs {
                GameMode = GameMode.Client,
                SessionName = networkSetting.SessionName,
                PlayerCount = LobbyPlayerCount,
                Config = projectConfig,
            });

            if (!startResult.Ok) {
                var exception = new InvalidOperationException($"Fusion StartGame failed. reason={startResult.ShutdownReason}, message={startResult.ErrorMessage}");
                ReleaseRunner($"StartRunnerAsync failed. context={context}");
                state.OnNext(state.CurrentValue with {
                    Phase = OnlineDuelPhase.Error,
                    Message = exception.Message,
                });
                throw exception;
            }

            SendCommand(new OnlineDuelCommandPayload {
                kind = (int)OnlineDuelCommandKind.Resync,
                duelSessionId = identity.DuelSessionId,
                scene = currentScene.ToString(),
            });
            Debug.Log($"{LOG_PREFIX} Runner started. context={context}, sessionName={networkSetting.SessionName}, duelSessionId={identity.DuelSessionId}");
        }

        async Task WaitUntilRunnerReleasedAsync(string context) {
            var deadline = Time.realtimeSinceStartup + RunnerReleaseWaitTimeoutSeconds;
            while (Time.realtimeSinceStartup < deadline) {
                if (runner == null) {
                    return;
                }

                if (!runner.IsRunning) {
                    ReleaseRunner(context);
                    return;
                }

                await Awaitable.NextFrameAsync();
            }

            ReleaseRunner($"{context} timeout");
        }

        async Task<OnlineMatchResult> WaitForMatchAsync(int mid, float deadline) {
            var waitStartedAt = Time.realtimeSinceStartup;
            var lastHeartbeatAt = waitStartedAt;

            while (true) {
                mainThreadQueue?.Flush();
                if (matchCompletion.Task.IsCompleted) {
                    return await matchCompletion.Task;
                }

                if (Time.realtimeSinceStartup >= deadline) {
                    var exception = new TimeoutException($"Online matchmaking timed out after {networkSetting.MatchTimeoutSeconds:0.#} seconds.");
                    matchCompletion.TrySetException(exception);
                    state.OnNext(OnlineDuelUiState.Idle(identity.DuelSessionId));
                    throw exception;
                }

                var now = Time.realtimeSinceStartup;
                if (now - lastHeartbeatAt >= WaitHeartbeatIntervalSeconds) {
                    lastHeartbeatAt = now;
                    Debug.Log($"{LOG_PREFIX} [match#{mid}] waiting. elapsed={now - waitStartedAt:0.#}s, runnerRunning={runner != null && runner.IsRunning}, taskStatus={matchCompletion.Task.Status}");
                }

                await Awaitable.NextFrameAsync();
            }
        }

        void SendCommand(OnlineDuelCommandPayload payload) {
            if (runner == null || !runner.IsRunning) {
                Debug.LogWarning($"{LOG_PREFIX} Command skipped because runner is not running. kind={(OnlineDuelCommandKind)payload.kind}");
                return;
            }

            if (string.IsNullOrWhiteSpace(payload.scene)) {
                payload.scene = currentScene.ToString();
            }
            payload.playerStatus = currentPlayerStatus;
            runner.SendReliableDataToServer(OnlineDuelProtocol.CommandKey, OnlineDuelProtocol.SerializeCommand(payload));
        }

        void TickPresenceHeartbeat() {
            if (runner == null || !runner.IsRunning || IsBattleScene(currentScene)) {
                return;
            }

            var now = Time.realtimeSinceStartup;
            if (now - lastPresenceHeartbeatAt < PresenceHeartbeatIntervalSeconds) {
                return;
            }

            lastPresenceHeartbeatAt = now;
            SendCommand(new OnlineDuelCommandPayload {
                kind = (int)OnlineDuelCommandKind.PresenceUpdate,
                duelSessionId = identity.DuelSessionId,
                scene = currentScene.ToString(),
            });
        }

        void EnqueueMainThread(Action action, string label) {
            if (mainThreadQueue != null) {
                mainThreadQueue.Enqueue(action, label);
            }
            else {
                action();
            }
        }

        void ApplyDuelEvent(byte[] dataCopy) {
            var payload = OnlineDuelProtocol.DeserializeEvent(new ArraySegment<byte>(dataCopy));
            var kind = (OnlineDuelEventKind)payload.kind;
            var matchResult = new OnlineMatchResult(
                (Striker)payload.localStriker,
                (Striker)payload.opponentStriker,
                (Stage)payload.stage,
                payload.musicId ?? "",
                payload.localIsPlayer1);

            var nextPhase = PhaseFromEvent(kind, payload);
            var current = state.CurrentValue;
            if (cancellationRequested && (kind == OnlineDuelEventKind.MatchResult || kind == OnlineDuelEventKind.MatchStatus)) {
                Debug.Log($"{LOG_PREFIX} Ignoring {kind} because cancellation was requested.");
                return;
            }
            if (kind == OnlineDuelEventKind.MatchStatus
                && (current.Phase == OnlineDuelPhase.Consumed || current.Phase == OnlineDuelPhase.Matching)
                && current.ReservationId == (payload.reservationId ?? "")) {
                nextPhase = current.Phase;
            }
            var matchDeadlineRealtime = nextPhase == OnlineDuelPhase.Matching ? current.MatchDeadlineRealtime : 0f;

            var next = new OnlineDuelUiState(
                nextPhase,
                payload.localSessionId ?? identity.DuelSessionId,
                payload.candidateSessionId ?? "",
                payload.inviteId ?? "",
                payload.inviteFromSessionId ?? "",
                payload.inviteToSessionId ?? "",
                payload.reservationId ?? "",
                payload.opponentSessionId ?? "",
                payload.opponentScene ?? "",
                payload.opponentStatus,
                payload.message ?? "",
                matchDeadlineRealtime,
                payload.sceneSyncId,
                matchResult);
            state.OnNext(next);

            if (kind == OnlineDuelEventKind.MatchResult && matchCompletion != null && !matchCompletion.Task.IsCompleted) {
                matchCompletion.TrySetResult(matchResult);
            }

            if (kind == OnlineDuelEventKind.ReservationExpired && matchCompletion != null && !matchCompletion.Task.IsCompleted) {
                matchCompletion.TrySetException(new OperationCanceledException(payload.message ?? "Reservation expired."));
            }

            if (kind == OnlineDuelEventKind.Error && matchCompletion != null && !matchCompletion.Task.IsCompleted && !cancellationRequested) {
                matchCompletion.TrySetException(new InvalidOperationException(payload.message ?? "Online duel error."));
            }

            Debug.Log($"{LOG_PREFIX} Event applied. kind={kind}, phase={next.Phase}, reservationId={next.ReservationId}, opponent={next.OpponentSessionId}");
        }

        void ResetActiveDuelState(string message) {
            if (!cancellationRequested) {
                matchCompletion?.TrySetException(new OperationCanceledException(message));
            }

            localMatchRequest = default;
            cancellationRequested = false;
            state.OnNext(OnlineDuelUiState.Idle(identity.DuelSessionId) with {
                Message = message,
            });
        }

        static OnlineDuelPhase PhaseFromEvent(OnlineDuelEventKind kind, OnlineDuelEventPayload payload) {
            return kind switch {
                OnlineDuelEventKind.CandidateShown => OnlineDuelPhase.CandidateShown,
                OnlineDuelEventKind.IncomingInvite => OnlineDuelPhase.IncomingInvite,
                OnlineDuelEventKind.InviteUpdated => OnlineDuelPhase.InviteSent,
                OnlineDuelEventKind.Reserved => OnlineDuelPhase.Reserved,
                OnlineDuelEventKind.ReservationExpired => OnlineDuelPhase.Idle,
                OnlineDuelEventKind.MatchStatus => OnlineDuelPhase.Reserved,
                OnlineDuelEventKind.MatchResult => OnlineDuelPhase.EnterBattle,
                OnlineDuelEventKind.Error => OnlineDuelPhase.Error,
                OnlineDuelEventKind.Snapshot => string.IsNullOrWhiteSpace(payload.reservationId)
                    ? OnlineDuelPhase.Idle
                    : OnlineDuelPhase.Reserved,
                _ => OnlineDuelPhase.Idle,
            };
        }

        void ReleaseRunner(string context) {
            if (runner == null) {
                return;
            }

            var targetRunner = runner;
            runner = null;
            mainThreadQueue = null;
            targetRunner.RemoveCallbacks(this);
            var runnerObject = targetRunner.gameObject;
            if (runnerObject != null) {
                UnityEngine.Object.Destroy(runnerObject);
            }

            Debug.Log($"{LOG_PREFIX} Runner released. context={context}");
        }

        static bool IsBattleScene(AppScene scene) {
            return scene == AppScene.Live || scene == AppScene.Street;
        }

        static OnlineDuelPlayerStatus ResolveInitialPlayerStatus(AppScene scene) {
            return scene switch {
                AppScene.CharacterSelect => OnlineDuelPlayerStatus.CharacterSelecting,
                _ => OnlineDuelPlayerStatus.StageSelecting,
            };
        }

        public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) {
            if (!ReferenceEquals(runner, this.runner)) {
                return;
            }

            if (key != OnlineDuelProtocol.EventKey) {
                return;
            }

            var copy = new byte[data.Count];
            if (data.Count > 0) {
                data.CopyTo(copy);
            }

            EnqueueMainThread(() => ApplyDuelEvent(copy), "ApplyDuelEvent");
        }

        public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason) {
            if (!ReferenceEquals(runner, this.runner)) {
                return;
            }

            ResetActiveDuelState($"Online session was shut down. reason={shutdownReason}");
            ReleaseRunner($"OnShutdown {shutdownReason}");
        }

        public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) {
            if (!ReferenceEquals(runner, this.runner)) {
                return;
            }

            ResetActiveDuelState($"Disconnected from online session. reason={reason}");
            ReleaseRunner($"OnDisconnectedFromServer {reason}");
        }

        public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) {
            if (!ReferenceEquals(runner, this.runner)) {
                return;
            }

            ResetActiveDuelState($"Online session connection failed. reason={reason}");
            ReleaseRunner($"OnConnectFailed {reason}");
        }

        public void Dispose() {
            disposables.Dispose();
            if (runner != null && runner.IsRunning) {
                _ = runner.Shutdown();
            }
        }

        public void OnPlayerJoined(NetworkRunner runner, PlayerRef player) { }
        public void OnPlayerLeft(NetworkRunner runner, PlayerRef player) { }
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
        public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) {
            request.Accept();
        }
    }
}
