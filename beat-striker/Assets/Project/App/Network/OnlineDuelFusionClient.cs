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
        int LastSceneSyncId { get; }
        Task NotifySceneReadyAsync(AppScene scene, bool appOverlayEnabled);
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
        bool currentAppOverlayEnabled;
        OnlineDuelPlayerStatus currentPlayerStatus = OnlineDuelPlayerStatus.StageSelecting;
        int sceneSyncSequence;
        int lastSceneSyncId;
        int lastViewSeq;
        float lastPresenceHeartbeatAt;
        bool commandSendSuspended = true;

        public ReadOnlyReactiveProperty<OnlineDuelUiState> State => state;
        public int LastSceneSyncId => lastSceneSyncId;

        [Inject]
        public OnlineDuelFusionClient(IAppNetworkSetting networkSetting, IOnlineDuelIdentity identity) {
            this.networkSetting = networkSetting;
            this.identity = identity;
            state = new ReactiveProperty<OnlineDuelUiState>(OnlineDuelUiState.Idle(identity.DuelSessionId));
        }

        public void Initialize() {
            Debug.Log($"{LOG_PREFIX} Initialize begin. currentScene={currentScene}, runnerExists={runner != null}, runnerIsRunning={(runner != null && runner.IsRunning)}");
            Observable.EveryUpdate().Subscribe(_ => TickPresenceHeartbeat()).AddTo(disposables);
            _ = EnsureRunnerStartedAsync("initialize");
        }

        public bool TryGetRunner(out NetworkRunner runner) {
            runner = this.runner;
            return runner != null && runner.IsRunning;
        }

        public async Task NotifySceneReadyAsync(AppScene scene, bool appOverlayEnabled) {
            Debug.Log($"{LOG_PREFIX} NotifySceneReadyAsync begin. scene={scene}, currentScene={currentScene}, runnerExists={runner != null}, runnerIsRunning={(runner != null && runner.IsRunning)}");
            currentScene = scene;
            currentAppOverlayEnabled = appOverlayEnabled;
            currentPlayerStatus = ResolveInitialPlayerStatus(scene);
            if (IsBattleScene(scene)) {
                Debug.Log($"{LOG_PREFIX} NotifySceneReadyAsync skipped because scene is battle. scene={scene}");
                return;
            }

            sceneSyncSequence += 1;
            lastSceneSyncId = sceneSyncSequence;
            if (!currentAppOverlayEnabled && (runner == null || !runner.IsRunning)) {
                Debug.Log($"{LOG_PREFIX} NotifySceneReadyAsync skipped runner start because AppOverlay is disabled. scene={scene}");
                return;
            }

            Debug.Log($"{LOG_PREFIX} Scene sync incremented. scene={scene}, sceneSyncSequence={sceneSyncSequence}, lastSceneSyncId={lastSceneSyncId}");
            await EnsureRunnerStartedAsync($"scene ready {scene}");
            Debug.Log($"{LOG_PREFIX} NotifySceneReadyAsync after EnsureRunnerStarted. scene={scene}, runnerExists={runner != null}, runnerIsRunning={(runner != null && runner.IsRunning)}");
            SendCommand(new OnlineDuelCommandPayload {
                kind = (int)OnlineDuelCommandKind.PresenceUpdate,
                duelSessionId = identity.DuelSessionId,
                scene = scene.ToString(),
                appOverlayEnabled = currentAppOverlayEnabled,
                sceneSyncId = lastSceneSyncId,
            });
        }

        public async Task NotifyPlayerStatusAsync(OnlineDuelPlayerStatus status) {
            Debug.Log($"{LOG_PREFIX} NotifyPlayerStatusAsync begin. status={status}, currentScene={currentScene}, runnerExists={runner != null}, runnerIsRunning={(runner != null && runner.IsRunning)}");
            currentPlayerStatus = status;
            if (IsBattleScene(currentScene)) {
                Debug.Log($"{LOG_PREFIX} NotifyPlayerStatusAsync skipped because current scene is battle. currentScene={currentScene}");
                return;
            }

            await EnsureRunnerStartedAsync($"player status {status}");
            Debug.Log($"{LOG_PREFIX} NotifyPlayerStatusAsync after EnsureRunnerStarted. status={status}, currentScene={currentScene}, runnerExists={runner != null}, runnerIsRunning={(runner != null && runner.IsRunning)}");
            SendCommand(new OnlineDuelCommandPayload {
                kind = (int)OnlineDuelCommandKind.PresenceUpdate,
                duelSessionId = identity.DuelSessionId,
                scene = currentScene.ToString(),
                appOverlayEnabled = currentAppOverlayEnabled,
            });
        }

        public async void InviteCandidate() {
            var current = state.CurrentValue;
            if (string.IsNullOrWhiteSpace(current.CandidateSessionId)) {
                return;
            }

            await EnsureRunnerStartedAsync("InviteCandidate");
            SendCommand(new OnlineDuelCommandPayload {
                kind = (int)OnlineDuelCommandKind.InviteCreate,
                duelSessionId = identity.DuelSessionId,
                targetSessionId = current.CandidateSessionId,
            });
        }

        public void SkipCandidate() {
        }

        public async void AcceptInvite() {
            var current = state.CurrentValue;
            if (string.IsNullOrWhiteSpace(current.InviteId)) {
                return;
            }

            await EnsureRunnerStartedAsync("AcceptInvite");
            SendCommand(new OnlineDuelCommandPayload {
                kind = (int)OnlineDuelCommandKind.InviteAccept,
                duelSessionId = identity.DuelSessionId,
                inviteId = current.InviteId,
            });
        }

        public async void RejectInvite() {
            var current = state.CurrentValue;
            if (string.IsNullOrWhiteSpace(current.InviteId)) {
                return;
            }

            await EnsureRunnerStartedAsync("RejectInvite");
            SendCommand(new OnlineDuelCommandPayload {
                kind = (int)OnlineDuelCommandKind.InviteReject,
                duelSessionId = identity.DuelSessionId,
                inviteId = current.InviteId,
            });
        }

        public async void CancelInvite() {
            var current = state.CurrentValue;
            if (string.IsNullOrWhiteSpace(current.InviteId)) {
                return;
            }

            await EnsureRunnerStartedAsync("CancelInvite");
            SendCommand(new OnlineDuelCommandPayload {
                kind = (int)OnlineDuelCommandKind.InviteCancel,
                duelSessionId = identity.DuelSessionId,
                inviteId = current.InviteId,
            });
        }

        public async void ConsumeReservation() {
            var current = state.CurrentValue;
            if (string.IsNullOrWhiteSpace(current.ReservationId)) {
                return;
            }

            currentPlayerStatus = OnlineDuelPlayerStatus.Waiting;
            await EnsureRunnerStartedAsync("ConsumeReservation");
            SendCommand(new OnlineDuelCommandPayload {
                kind = (int)OnlineDuelCommandKind.ReservationConsume,
                duelSessionId = identity.DuelSessionId,
                reservationId = current.ReservationId,
            });
        }

        public async Task<OnlineMatchResult> MatchAsync(OnlineMatchRequest request) {
            if (matchCompletion != null && !matchCompletion.Task.IsCompleted) {
                throw new InvalidOperationException("Online matchmaking is already running.");
            }

            if (string.IsNullOrWhiteSpace(request.ReservationId)) {
                throw new InvalidOperationException("Online matchmaking requires a reservationId.");
            }

            await EnsureRunnerStartedAsync("MatchAsync");

            matchLogSequence++;
            currentMatchLogId = matchLogSequence;
            var mid = currentMatchLogId;
            localMatchRequest = request;
            cancellationRequested = false;
            matchCompletion = new TaskCompletionSource<OnlineMatchResult>();
            currentPlayerStatus = OnlineDuelPlayerStatus.Waiting;

            SendCommand(new OnlineDuelCommandPayload {
                kind = (int)OnlineDuelCommandKind.MatchRequest,
                duelSessionId = identity.DuelSessionId,
                reservationId = request.ReservationId,
                striker = (int)request.LocalStriker,
                stage = (int)request.CandidateStage,
                musicId = request.CandidateMusicId,
            });

            Debug.Log($"{LOG_PREFIX} [match#{mid}] Match request sent. reservationId={request.ReservationId}, striker={request.LocalStriker}, stage={request.CandidateStage}, musicId={request.CandidateMusicId}");
            return await WaitForMatchAsync(mid);
        }

        public void CancelMatchmaking() {
            var current = state.CurrentValue;
            if (matchCompletion == null || matchCompletion.Task.IsCompleted) {
                if (!string.IsNullOrWhiteSpace(current.ReservationId)) {
                    SendCommand(new OnlineDuelCommandPayload {
                        kind = (int)OnlineDuelCommandKind.MatchCancel,
                        duelSessionId = identity.DuelSessionId,
                        reservationId = current.ReservationId,
                    });
                }
                return;
            }

            cancellationRequested = true;
            SendCommand(new OnlineDuelCommandPayload {
                kind = (int)OnlineDuelCommandKind.MatchCancel,
                duelSessionId = identity.DuelSessionId,
                reservationId = !string.IsNullOrWhiteSpace(localMatchRequest.ReservationId)
                    ? localMatchRequest.ReservationId
                    : current.ReservationId,
            });
            matchCompletion.TrySetException(new OperationCanceledException("Online matchmaking canceled by player."));
        }

        public async Task TeardownOnlineRunnerAsync() {
            matchCompletion?.TrySetException(new OperationCanceledException("Online runner was torn down."));
            ResetActiveDuelState("Online runner was torn down.");
            if (runner == null || !runner.IsRunning) {
                return;
            }

            await runner.Shutdown();
            await WaitUntilRunnerReleasedAsync("TeardownOnlineRunnerAsync");
        }

        async Task EnsureRunnerStartedAsync(string context) {
            Debug.Log($"{LOG_PREFIX} EnsureRunnerStartedAsync begin. context={context}, runnerExists={runner != null}, runnerIsRunning={(runner != null && runner.IsRunning)}, startRunnerTaskActive={(startRunnerTask != null && !startRunnerTask.IsCompleted)}");
            if (runner != null && runner.IsRunning) {
                Debug.Log($"{LOG_PREFIX} EnsureRunnerStartedAsync skipped because runner already running. context={context}");
                return;
            }

            if (startRunnerTask != null && !startRunnerTask.IsCompleted) {
                Debug.Log($"{LOG_PREFIX} EnsureRunnerStartedAsync awaiting already-starting runner task. context={context}");
                await startRunnerTask;
                return;
            }

            startRunnerTask = StartRunnerAsync(context);
            await startRunnerTask;
            Debug.Log($"{LOG_PREFIX} EnsureRunnerStartedAsync completed. context={context}, runnerExists={runner != null}, runnerIsRunning={(runner != null && runner.IsRunning)}");
        }

        async Task StartRunnerAsync(string context) {
            Debug.Log($"{LOG_PREFIX} StartRunnerAsync begin. context={context}");
            ReleaseRunner($"StartRunnerAsync replace stale. context={context}");
            commandSendSuspended = true;

            Debug.Log($"{LOG_PREFIX} Creating runner GameObject.");
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
                commandSendSuspended = true;
                UpdateState(state.CurrentValue with {
                    UiMode = OnlineDuelUiMode.Error,
                    Message = exception.Message,
                });
                throw exception;
            }

            commandSendSuspended = false;

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

        async Task<OnlineMatchResult> WaitForMatchAsync(int mid) {
            var waitStartedAt = Time.realtimeSinceStartup;
            var lastHeartbeatAt = waitStartedAt;

            while (true) {
                mainThreadQueue?.Flush();
                if (matchCompletion.Task.IsCompleted) {
                    return await matchCompletion.Task;
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
            Debug.Log($"{LOG_PREFIX} SendCommand requested. kind={(OnlineDuelCommandKind)payload.kind}, scene={payload.scene}, sceneSyncId={payload.sceneSyncId}, runnerExists={runner != null}, runnerIsRunning={(runner != null && runner.IsRunning)}, commandSendSuspended={commandSendSuspended}");
            if (runner == null || !runner.IsRunning || commandSendSuspended) {
                Debug.LogWarning($"{LOG_PREFIX} Command skipped because runner is not running or suspended. kind={(OnlineDuelCommandKind)payload.kind}, runnerExists={runner != null}, runnerIsRunning={(runner != null && runner.IsRunning)}, commandSendSuspended={commandSendSuspended}");
                return;
            }

            if (string.IsNullOrWhiteSpace(payload.scene)) {
                payload.scene = currentScene.ToString();
            }
            payload.appOverlayEnabled = currentAppOverlayEnabled;
            payload.playerStatus = currentPlayerStatus;
            try {
                Debug.Log($"{LOG_PREFIX} Sending command. kind={(OnlineDuelCommandKind)payload.kind}, scene={payload.scene}, sceneSyncId={payload.sceneSyncId}, playerStatus={payload.playerStatus}");
                runner.SendReliableDataToServer(OnlineDuelProtocol.CommandKey, OnlineDuelProtocol.SerializeCommand(payload));
            }
            catch (NullReferenceException) {
                commandSendSuspended = true;
                Debug.LogWarning($"{LOG_PREFIX} Command skipped because runner became unavailable during send. kind={(OnlineDuelCommandKind)payload.kind}");
            }
        }

        void TickPresenceHeartbeat() {
            if (runner == null || !runner.IsRunning) {
                return;
            }

            var now = Time.realtimeSinceStartup;
            if (now - lastPresenceHeartbeatAt < PresenceHeartbeatIntervalSeconds) {
                return;
            }

            lastPresenceHeartbeatAt = now;
            Debug.Log($"{LOG_PREFIX} TickPresenceHeartbeat sending heartbeat. currentScene={currentScene}, currentPlayerStatus={currentPlayerStatus}");
            SendCommand(new OnlineDuelCommandPayload {
                kind = (int)OnlineDuelCommandKind.PresenceUpdate,
                duelSessionId = identity.DuelSessionId,
                scene = currentScene.ToString(),
                appOverlayEnabled = currentAppOverlayEnabled,
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

        void UpdateState(OnlineDuelUiState next) {
            state.OnNext(next);
        }

        void ApplyDuelEvent(byte[] dataCopy) {
            var payload = OnlineDuelProtocol.DeserializeEvent(new ArraySegment<byte>(dataCopy));
            var kind = (OnlineDuelEventKind)payload.kind;
            var current = state.CurrentValue;
            if (kind == OnlineDuelEventKind.ViewState) {
                if (payload.seq <= lastViewSeq) {
                    Debug.Log($"{LOG_PREFIX} Dropped stale ViewState. seq={payload.seq}, lastViewSeq={lastViewSeq}");
                    return;
                }

                lastViewSeq = payload.seq;
                var next = MapToViewState(payload, current.MatchResult);
                UpdateState(next);
                if (next.UiMode == OnlineDuelUiMode.Error
                    && matchCompletion != null
                    && !matchCompletion.Task.IsCompleted
                    && !cancellationRequested) {
                    matchCompletion.TrySetException(new InvalidOperationException(next.Message ?? "Online duel error."));
                }
                else if (current.HasReservation
                         && !next.HasReservation
                         && matchCompletion != null
                         && !matchCompletion.Task.IsCompleted
                         && !cancellationRequested) {
                    matchCompletion.TrySetException(new OperationCanceledException(string.IsNullOrWhiteSpace(next.Message)
                        ? "Reservation expired."
                        : next.Message));
                }

                Debug.Log($"{LOG_PREFIX} Event applied. kind={kind}, seq={payload.seq}, uiMode={next.UiMode}, reservationId={next.ReservationId}, opponent={next.OpponentSessionId}");
                return;
            }

            if (cancellationRequested && kind == OnlineDuelEventKind.MatchResult) {
                Debug.Log($"{LOG_PREFIX} Ignoring MatchResult because cancellation was requested.");
                return;
            }

            var matchResult = new OnlineMatchResult(
                (Striker)payload.localStriker,
                (Striker)payload.opponentStriker,
                (Stage)payload.stage,
                payload.musicId ?? "",
                payload.localIsPlayer1);
            var nextState = current with {
                UiMode = OnlineDuelUiMode.EnterBattle,
                LocalSessionId = string.IsNullOrWhiteSpace(payload.localSessionId) ? identity.DuelSessionId : payload.localSessionId,
                ReservationId = payload.reservationId ?? current.ReservationId,
                OpponentSessionId = payload.opponentSessionId ?? current.OpponentSessionId,
                Message = payload.message ?? "",
                MatchResult = matchResult,
            };
            UpdateState(nextState);

            if (matchCompletion != null && !matchCompletion.Task.IsCompleted) {
                matchCompletion.TrySetResult(matchResult);
            }

            Debug.Log($"{LOG_PREFIX} Event applied. kind={kind}, uiMode={nextState.UiMode}, reservationId={nextState.ReservationId}, opponent={nextState.OpponentSessionId}");
        }

        void ResetActiveDuelState(string message) {
            if (!cancellationRequested) {
                matchCompletion?.TrySetException(new OperationCanceledException(message));
            }

            localMatchRequest = default;
            cancellationRequested = false;
            lastViewSeq = 0;
            UpdateState(OnlineDuelUiState.Idle(identity.DuelSessionId, message));
        }

        OnlineDuelUiState MapToViewState(OnlineDuelEventPayload payload, OnlineMatchResult matchResult) {
            return new OnlineDuelUiState(
                (OnlineDuelUiMode)payload.uiMode,
                string.IsNullOrWhiteSpace(payload.localSessionId) ? identity.DuelSessionId : payload.localSessionId,
                payload.candidateSessionId ?? "",
                payload.inviteId ?? "",
                payload.inviteFromSessionId ?? "",
                payload.inviteToSessionId ?? "",
                payload.reservationId ?? "",
                payload.opponentSessionId ?? "",
                payload.opponentScene ?? "",
                payload.opponentStatus,
                payload.message ?? "",
                matchResult);
        }

        void ReleaseRunner(string context) {
            if (runner == null) {
                commandSendSuspended = true;
                return;
            }

            var targetRunner = runner;
            runner = null;
            commandSendSuspended = true;
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
                Debug.LogWarning($"{LOG_PREFIX} OnReliableDataReceived ignored due to stale runner. incomingRunner={runner?.GetInstanceID()}, activeRunner={this.runner?.GetInstanceID()}");
                return;
            }

            if (key != OnlineDuelProtocol.EventKey) {
                Debug.LogWarning($"{LOG_PREFIX} OnReliableDataReceived ignored due to unexpected key. key={key}");
                return;
            }

            Debug.Log($"{LOG_PREFIX} OnReliableDataReceived. player={player}, eventKey={key}, bytes={data.Count}");
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

            commandSendSuspended = true;
            ResetActiveDuelState($"Online session was shut down. reason={shutdownReason}");
            ReleaseRunner($"OnShutdown {shutdownReason}");
        }

        public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) {
            if (!ReferenceEquals(runner, this.runner)) {
                return;
            }

            commandSendSuspended = true;
            ResetActiveDuelState($"Disconnected from online session. reason={reason}");
            ReleaseRunner($"OnDisconnectedFromServer {reason}");
        }

        public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) {
            if (!ReferenceEquals(runner, this.runner)) {
                return;
            }

            commandSendSuspended = true;
            ResetActiveDuelState($"Online session connection failed. reason={reason}");
            ReleaseRunner($"OnConnectFailed {reason}");
        }

        public void Dispose() {
            disposables.Dispose();
            commandSendSuspended = true;
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
        public void OnConnectedToServer(NetworkRunner runner) {
            if (!ReferenceEquals(runner, this.runner)) {
                return;
            }

            commandSendSuspended = false;
        }
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
