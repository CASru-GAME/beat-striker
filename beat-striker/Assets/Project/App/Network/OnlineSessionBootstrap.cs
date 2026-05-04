using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Fusion;
using Fusion.Sockets;
using UnityEngine;
using VContainer;

namespace Alice {
    public interface IOnlineSessionBootstrap {
        Task<OnlineMatchResult> MatchAsync(OnlineMatchRequest request);
        void CancelMatchmaking();
        Task TeardownOnlineRunnerAsync();
    }

    public interface INetworkRunnerProvider {
        bool TryGetRunner(out NetworkRunner runner);
    }

    public class OnlineSessionBootstrap : IOnlineSessionBootstrap, INetworkRunnerProvider, INetworkRunnerCallbacks {
        const string LOG_PREFIX = "[OnlineSessionBootstrap]";
        const float WaitHeartbeatIntervalSeconds = 1f;
        const float RunnerReleaseWaitTimeoutSeconds = 30f;

        readonly IAppNetworkSetting networkSetting;

        NetworkRunner runner;
        OnlineSessionMainThreadQueue mainThreadQueue;
        TaskCompletionSource<OnlineMatchResult> matchCompletion;
        OnlineMatchRequest localRequest;
        bool cancellationRequested;
        int matchLogSequence;
        int currentMatchLogId;

        [Inject]
        public OnlineSessionBootstrap(IAppNetworkSetting networkSetting) {
            this.networkSetting = networkSetting;
        }

        public bool TryGetRunner(out NetworkRunner runner) {
            runner = this.runner;
            return runner != null && runner.IsRunning;
        }

        public async Task<OnlineMatchResult> MatchAsync(OnlineMatchRequest request) {
            if (matchCompletion != null && !matchCompletion.Task.IsCompleted) {
                Debug.LogError($"{LOG_PREFIX} [match#?] MatchAsync rejected: matchmaking already in progress. existingTaskStatus={matchCompletion.Task.Status}");
                throw new InvalidOperationException("Online matchmaking is already running.");
            }

            await ShutdownRunningRunnerForFreshMatchAsync("MatchAsync before new match search");

            matchLogSequence++;
            currentMatchLogId = matchLogSequence;
            var mid = currentMatchLogId;

            localRequest = request;
            cancellationRequested = false;
            matchCompletion = new TaskCompletionSource<OnlineMatchResult>();

            Debug.Log($"{LOG_PREFIX} [match#{mid}] MatchAsync begin. sessionName={networkSetting.SessionName}, timeout={networkSetting.MatchTimeoutSeconds:0.#}s, localStriker={request.LocalStriker}, stage={request.CandidateStage}, musicId={request.CandidateMusicId}, tcsCreated task.Status={matchCompletion.Task.Status}");
            EnsureRunner(mid);
            var activeRunner = runner;
            Debug.Log($"{LOG_PREFIX} [match#{mid}] MatchAsync after EnsureRunner. hasRunner={activeRunner != null}, runner.IsRunning={activeRunner != null && activeRunner.IsRunning}, mainThreadQueue={(mainThreadQueue != null ? $"instanceId={mainThreadQueue.GetInstanceID()}" : "null")}");
            var projectConfig = NetworkProjectConfig.Deserialize(
                NetworkProjectConfig.Serialize(NetworkProjectConfig.Global));
            var simulation = projectConfig.Simulation;
            simulation.Topology = Topologies.ClientServer;
            projectConfig.Simulation = simulation;

            Debug.Log($"{LOG_PREFIX} [match#{mid}] MatchAsync StartGame begin. mode=Client, sessionName={networkSetting.SessionName}, playerCount=2");
            var startResult = await activeRunner.StartGame(new StartGameArgs {
                GameMode = GameMode.Client,
                SessionName = networkSetting.SessionName,
                PlayerCount = 2,
                Config = projectConfig,
            });

            Debug.Log($"{LOG_PREFIX} [match#{mid}] MatchAsync StartGame completed. ok={startResult.Ok}, shutdownReason={startResult.ShutdownReason}, message={startResult.ErrorMessage}");

            if (!startResult.Ok) {
                if (startResult.ShutdownReason == ShutdownReason.OperationCanceled) {
                    var canceledException = new OperationCanceledException("Online matchmaking canceled by player.");
                    var setExc = matchCompletion.TrySetException(canceledException);
                    Debug.Log($"{LOG_PREFIX} [match#{mid}] MatchAsync StartGame not ok (canceled). TrySetException={setExc}");
                    ReleaseRunner(activeRunner, $"[match#{mid}] StartGame canceled");
                    throw canceledException;
                }

                var exception = new InvalidOperationException($"Fusion StartGame failed. reason={startResult.ShutdownReason}, message={startResult.ErrorMessage}");
                var setFail = matchCompletion.TrySetException(exception);
                Debug.LogError($"{LOG_PREFIX} [match#{mid}] MatchAsync StartGame failed. TrySetException={setFail}, reason={startResult.ShutdownReason}");
                ReleaseRunner(activeRunner, $"[match#{mid}] StartGame failed");
                throw exception;
            }

            if (activeRunner == null || !activeRunner.IsRunning) {
                var exception = new OperationCanceledException("Online matchmaking canceled before runner became ready.");
                var setExc = matchCompletion.TrySetException(exception);
                Debug.LogWarning($"{LOG_PREFIX} [match#{mid}] MatchAsync runner not running after ok StartGame. TrySetException={setExc}");
                ReleaseRunner(activeRunner, $"[match#{mid}] runner not running after StartGame");
                throw exception;
            }

            Debug.Log($"{LOG_PREFIX} [match#{mid}] MatchAsync runner running. isServer={activeRunner.IsServer}, localPlayer={activeRunner.LocalPlayer}");

            var requestBytes = OnlineMatchProtocol.SerializeRequest(localRequest);
            Debug.Log($"{LOG_PREFIX} [match#{mid}] MatchAsync sending match request to server. localPlayer={activeRunner.LocalPlayer}, requestPayloadBytes={requestBytes.Length}");
            activeRunner.SendReliableDataToServer(OnlineMatchProtocol.RequestKey, requestBytes);
            Debug.Log($"{LOG_PREFIX} [match#{mid}] MatchAsync entering WaitForMatchAsync. timeoutSeconds={networkSetting.MatchTimeoutSeconds:0.#}");
            var result = await WaitForMatchAsync(mid);
            Debug.Log($"{LOG_PREFIX} [match#{mid}] MatchAsync WaitForMatchAsync returned. stage={result.Stage}, musicId={result.MusicId}, localIsP1={result.LocalIsPlayer1}");
            return result;
        }

        public async Task TeardownOnlineRunnerAsync() {
            if (matchCompletion != null && !matchCompletion.Task.IsCompleted) {
                Debug.Log($"{LOG_PREFIX} TeardownOnlineRunnerAsync: matchmaking in progress; canceling first.");
                CancelMatchmaking();
                await WaitUntilRunnerReleasedAsync("TeardownOnlineRunnerAsync after cancel");
                return;
            }

            await ShutdownRunningRunnerForFreshMatchAsync("TeardownOnlineRunnerAsync");
        }

        async Task ShutdownRunningRunnerForFreshMatchAsync(string context) {
            if (runner == null || !runner.IsRunning) {
                if (runner != null) {
                    Debug.Log($"{LOG_PREFIX} ShutdownRunningRunnerForFreshMatchAsync: non-running runner remains; releasing. context={context}");
                    ReleaseRunner(runner, context);
                }

                return;
            }

            Debug.Log($"{LOG_PREFIX} ShutdownRunningRunnerForFreshMatchAsync: shutting down running runner. context={context}, runnerInstanceId={runner.GetInstanceID()}");
            await runner.Shutdown();
            await WaitUntilRunnerReleasedAsync(context);
        }

        async Task WaitUntilRunnerReleasedAsync(string context) {
            var deadline = Time.realtimeSinceStartup + RunnerReleaseWaitTimeoutSeconds;
            while (Time.realtimeSinceStartup < deadline) {
                if (runner == null) {
                    Debug.Log($"{LOG_PREFIX} WaitUntilRunnerReleasedAsync completed (runner null). context={context}");
                    return;
                }

                if (!runner.IsRunning) {
                    Debug.Log($"{LOG_PREFIX} WaitUntilRunnerReleasedAsync: runner not running but ref exists; releasing. context={context}");
                    ReleaseRunner(runner, context);
                    return;
                }

                await Awaitable.NextFrameAsync();
            }

            Debug.LogWarning($"{LOG_PREFIX} WaitUntilRunnerReleasedAsync timed out after {RunnerReleaseWaitTimeoutSeconds:0.#}s. context={context}, runnerNull={runner == null}");
            if (runner != null) {
                ReleaseRunner(runner, $"{context} WaitUntilRunnerReleasedAsync timeout");
            }
        }

        public void CancelMatchmaking() {
            if (matchCompletion == null) {
                Debug.Log($"{LOG_PREFIX} [match#{currentMatchLogId}] CancelMatchmaking ignored: matchCompletion is null.");
                return;
            }

            if (matchCompletion.Task.IsCompleted) {
                Debug.Log($"{LOG_PREFIX} [match#{currentMatchLogId}] CancelMatchmaking ignored: task already completed. status={matchCompletion.Task.Status}");
                return;
            }

            cancellationRequested = true;
            var setExc = matchCompletion.TrySetException(new OperationCanceledException("Online matchmaking canceled by player."));
            Debug.Log($"{LOG_PREFIX} [match#{currentMatchLogId}] CancelMatchmaking TrySetException={setExc}, runnerNull={runner == null}, runner.IsRunning={runner != null && runner.IsRunning}");
            if (runner != null && runner.IsRunning) {
                Debug.Log($"{LOG_PREFIX} [match#{currentMatchLogId}] CancelMatchmaking shutting down runner. isServer={runner.IsServer}");
                _ = runner.Shutdown();
                return;
            }

            Debug.Log($"{LOG_PREFIX} [match#{currentMatchLogId}] CancelMatchmaking release runner without shutdown.");
            ReleaseRunner("[match#" + currentMatchLogId + "] CancelMatchmaking runner not running");
        }

        void EnsureRunner(int matchLogId) {
            if (runner != null && runner.IsRunning) {
                Debug.Log($"{LOG_PREFIX} [match#{matchLogId}] EnsureRunner reuse existing. runnerInstanceId={runner.GetInstanceID()}, mainThreadQueue={(mainThreadQueue != null ? mainThreadQueue.GetInstanceID().ToString() : "null")}");
                return;
            }

            ReleaseRunner($"[match#{matchLogId}] EnsureRunner replace stale");
            var runnerObject = new GameObject("OnlineSessionRunner");
            UnityEngine.Object.DontDestroyOnLoad(runnerObject);
            mainThreadQueue = runnerObject.AddComponent<OnlineSessionMainThreadQueue>();
            runner = runnerObject.AddComponent<NetworkRunner>();
            runner.AddCallbacks(this);
            Debug.Log($"{LOG_PREFIX} [match#{matchLogId}] EnsureRunner created new runner. runnerInstanceId={runner.GetInstanceID()}, queueInstanceId={mainThreadQueue.GetInstanceID()}, gameObject={runnerObject.name}");
        }

        void ReleaseRunner(string context) {
            ReleaseRunner(runner, context);
        }

        void ReleaseRunner(NetworkRunner targetRunner, string context = "unspecified") {
            if (targetRunner == null) {
                Debug.Log($"{LOG_PREFIX} ReleaseRunner skipped (targetRunner null). context={context}");
                return;
            }

            var wasCurrent = ReferenceEquals(runner, targetRunner);
            var goName = targetRunner.gameObject != null ? targetRunner.gameObject.name : "null";
            Debug.Log($"{LOG_PREFIX} ReleaseRunner begin. context={context}, wasCurrentRunner={wasCurrent}, targetRunnerInstanceId={targetRunner.GetInstanceID()}, gameObject={goName}");

            if (wasCurrent) {
                runner = null;
                mainThreadQueue = null;
            }

            targetRunner.RemoveCallbacks(this);
            var runnerObject = targetRunner.gameObject;
            if (runnerObject != null) {
                UnityEngine.Object.Destroy(runnerObject);
            }

            Debug.Log($"{LOG_PREFIX} ReleaseRunner completed. context={context}, wasCurrentRunner={wasCurrent}");
        }

        async Task<OnlineMatchResult> WaitForMatchAsync(int mid) {
            var deadline = Time.realtimeSinceStartup + networkSetting.MatchTimeoutSeconds;
            var waitStartedAt = Time.realtimeSinceStartup;
            var lastHeartbeatAt = waitStartedAt;
            var iteration = 0;
            Debug.Log($"{LOG_PREFIX} [match#{mid}] WaitForMatchAsync begin. deadlineRealtime={deadline:0.###}, timeoutSec={networkSetting.MatchTimeoutSeconds:0.###}, matchTaskInitialStatus={matchCompletion.Task.Status}, runnerRunning={runner != null && runner.IsRunning}, queuePendingApprox={mainThreadQueue?.PendingApprox.ToString() ?? "no-queue"}");

            while (true) {
                iteration++;
                var flushed = mainThreadQueue != null ? mainThreadQueue.Flush() : 0;
                if (flushed > 0) {
                    Debug.Log($"{LOG_PREFIX} [match#{mid}] WaitForMatchAsync loop iteration={iteration} Flush drained deferredActions={flushed}, queuePendingApproxAfter={mainThreadQueue?.PendingApprox.ToString() ?? "0"}");
                }

                if (matchCompletion.Task.IsCompleted) {
                    var st = matchCompletion.Task.Status;
                    Debug.Log($"{LOG_PREFIX} [match#{mid}] WaitForMatchAsync observed completed task. status={st}, iterations={iteration}, elapsedWaitRealtime={Time.realtimeSinceStartup - waitStartedAt:0.###}s");
                    if (matchCompletion.Task.IsFaulted) {
                        Debug.Log($"{LOG_PREFIX} [match#{mid}] WaitForMatchAsync task faulted. exception={matchCompletion.Task.Exception?.GetBaseException().Message}");
                    }

                    Debug.Log($"{LOG_PREFIX} [match#{mid}] WaitForMatchAsync awaiting matchCompletion.Task (propagate result or throw).");
                    return await matchCompletion.Task;
                }

                if (Time.realtimeSinceStartup >= deadline) {
                    Debug.LogWarning($"{LOG_PREFIX} [match#{mid}] WaitForMatchAsync deadline reached. iteration={iteration}, matchTaskCompleted={matchCompletion.Task.IsCompleted}, status={matchCompletion.Task.Status}, runnerRunning={runner != null && runner.IsRunning}");
                    if (matchCompletion.Task.IsCompleted) {
                        Debug.Log($"{LOG_PREFIX} [match#{mid}] WaitForMatchAsync deadline branch but task completed (race); awaiting.");
                        return await matchCompletion.Task;
                    }

                    var exception = new TimeoutException($"Online matchmaking timed out after {networkSetting.MatchTimeoutSeconds:0.#} seconds.");
                    var setExc = matchCompletion.TrySetException(exception);
                    Debug.LogWarning($"{LOG_PREFIX} [match#{mid}] WaitForMatchAsync TrySetException(timeout)={setExc}, afterStatus={matchCompletion.Task.Status}");
                    if (matchCompletion.Task.IsCompletedSuccessfully) {
                        Debug.Log($"{LOG_PREFIX} [match#{mid}] WaitForMatchAsync match result arrived during timeout handling; completing successfully.");
                        return await matchCompletion.Task;
                    }

                    Debug.LogWarning($"{LOG_PREFIX} [match#{mid}] WaitForMatchAsync timeout path. isServer={runner != null && runner.IsServer}, localPlayer={runner?.LocalPlayer}");
                    if (runner != null && runner.IsRunning) {
                        Debug.Log($"{LOG_PREFIX} [match#{mid}] Matchmaking timeout; shutting down runner so the next MatchAsync can create a fresh NetworkRunner.");
                        await runner.Shutdown();
                    }
                    else {
                        ReleaseRunner($"[match#{mid}] WaitForMatchAsync timeout runner already stopped");
                    }

                    throw exception;
                }

                var now = Time.realtimeSinceStartup;
                if (now - lastHeartbeatAt >= WaitHeartbeatIntervalSeconds) {
                    lastHeartbeatAt = now;
                    var remaining = deadline - now;
                    Debug.Log($"{LOG_PREFIX} [match#{mid}] WaitForMatchAsync heartbeat. iteration={iteration}, elapsedRealtime={now - waitStartedAt:0.#}s, remainingToDeadline={remaining:0.#}s, taskStatus={matchCompletion.Task.Status}, taskCompleted={matchCompletion.Task.IsCompleted}, runnerRunning={runner != null && runner.IsRunning}, localPlayer={runner?.LocalPlayer}, queuePendingApprox={mainThreadQueue?.PendingApprox.ToString() ?? "no-queue"}");
                }

                await Awaitable.NextFrameAsync();
            }
        }

        void EnqueueMainThread(Action action, string label) {
            if (mainThreadQueue != null) {
                mainThreadQueue.Enqueue(action, label);
            }
            else {
                Debug.LogWarning($"{LOG_PREFIX} [match#{currentMatchLogId}] EnqueueMainThread: mainThreadQueue null, invoking inline. label={label}");
                action();
            }
        }

        void ApplyMatchResultFromReliableData(byte[] dataCopy) {
            var mid = currentMatchLogId;
            Debug.Log($"{LOG_PREFIX} [match#{mid}] ApplyMatchResultFromReliableData begin. byteLen={dataCopy.Length}, matchCompletionNull={matchCompletion == null}, taskCompleted={matchCompletion?.Task.IsCompleted ?? false}");

            if (matchCompletion == null) {
                Debug.LogWarning($"{LOG_PREFIX} [match#{mid}] ApplyMatchResultFromReliableData aborted: matchCompletion is null.");
                return;
            }

            if (matchCompletion.Task.IsCompleted) {
                Debug.Log($"{LOG_PREFIX} [match#{mid}] ApplyMatchResultFromReliableData skipped: task already completed. status={matchCompletion.Task.Status}");
                return;
            }

            var segment = new ArraySegment<byte>(dataCopy);
            if (!OnlineMatchProtocol.TryDeserializeResult(segment, out var parsed, out var preview, out var failure)) {
                Debug.LogError($"{LOG_PREFIX} [match#{mid}] Match result deserialize failed. byteCount={dataCopy.Length}, utf8Preview={preview}, reason={failure}");
                var okExc = matchCompletion.TrySetException(new InvalidOperationException($"Online match result deserialize failed. {failure}"));
                Debug.Log($"{LOG_PREFIX} [match#{mid}] TrySetException(deserialize)={okExc}");
                return;
            }

            Debug.Log($"{LOG_PREFIX} [match#{mid}] Match result parsed. stage={parsed.Stage}, musicId={parsed.MusicId}, local={parsed.LocalStriker}, opponent={parsed.OpponentStriker}, localIsPlayer1={parsed.LocalIsPlayer1}");
            var okRes = matchCompletion.TrySetResult(parsed);
            Debug.Log($"{LOG_PREFIX} [match#{mid}] TrySetResult(parsed)={okRes}, taskStatusAfter={matchCompletion.Task.Status}");
        }

        public void OnPlayerJoined(NetworkRunner runner, PlayerRef player) {
            if (!ReferenceEquals(runner, this.runner)) {
                Debug.Log($"{LOG_PREFIX} [match#{currentMatchLogId}] OnPlayerJoined ignored (runner mismatch). callbackRunner={runner?.GetInstanceID()}, joinedPlayer={player}");
                return;
            }

            Debug.Log($"{LOG_PREFIX} [match#{currentMatchLogId}] OnPlayerJoined. player={player}, isServer={runner.IsServer}, localPlayer={runner.LocalPlayer}");
        }

        public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) {
            if (!ReferenceEquals(runner, this.runner)) {
                Debug.Log($"{LOG_PREFIX} [match#{currentMatchLogId}] OnReliableDataReceived ignored (runner mismatch). callbackRunner={runner?.GetInstanceID()}, thisRunner={this.runner?.GetInstanceID()}, key={key}, byteCount={data.Count}");
                return;
            }

            if (key == OnlineMatchProtocol.ResultKey) {
                Debug.Log($"{LOG_PREFIX} [match#{currentMatchLogId}] OnReliableDataReceived ResultKey. fromPlayer={player}, isServer={runner.IsServer}, byteCount={data.Count}, matchTaskCompleted={matchCompletion?.Task.IsCompleted ?? true}");
                var copy = new byte[data.Count];
                if (data.Count > 0) {
                    data.CopyTo(copy);
                }

                EnqueueMainThread(() => ApplyMatchResultFromReliableData(copy), "ApplyMatchResultFromReliableData");
            }
            else {
                Debug.Log($"{LOG_PREFIX} [match#{currentMatchLogId}] OnReliableDataReceived non-ResultKey (ignored for match). key={key}, byteCount={data.Count}");
            }
        }

        public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason) {
            if (!ReferenceEquals(runner, this.runner)) {
                Debug.Log($"{LOG_PREFIX} [match#{currentMatchLogId}] OnShutdown ignored (runner mismatch). callbackRunner={runner?.GetInstanceID()}, thisRunner={this.runner?.GetInstanceID()}, reason={shutdownReason}");
                return;
            }

            var mid = currentMatchLogId;
            if (shutdownReason != ShutdownReason.Ok && !cancellationRequested) {
                var ex = new InvalidOperationException($"Fusion shutdown. reason={shutdownReason}");
                var setExc = matchCompletion?.TrySetException(ex) ?? false;
                Debug.LogWarning($"{LOG_PREFIX} [match#{mid}] OnShutdown TrySetException(shutdown)={setExc}, reason={shutdownReason}, matchTaskAfter={matchCompletion?.Task.Status}");
            }
            else {
                Debug.Log($"{LOG_PREFIX} [match#{mid}] OnShutdown no TrySetException (ok shutdown or cancel). reason={shutdownReason}, canceled={cancellationRequested}");
            }

            Debug.Log($"{LOG_PREFIX} [match#{mid}] OnShutdown ReleaseRunner. reason={shutdownReason}, canceled={cancellationRequested}, matchTaskCompleted={matchCompletion?.Task.IsCompleted ?? true}");
            ReleaseRunner(runner, $"[match#{mid}] OnShutdown");
            cancellationRequested = false;
        }

        public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) {
            if (!ReferenceEquals(runner, this.runner)) {
                Debug.Log($"{LOG_PREFIX} [match#{currentMatchLogId}] OnDisconnectedFromServer ignored (runner mismatch).");
                return;
            }

            var mid = currentMatchLogId;
            var ex = new InvalidOperationException($"Fusion disconnected. reason={reason}");
            var setExc = matchCompletion?.TrySetException(ex) ?? false;
            Debug.LogWarning($"{LOG_PREFIX} [match#{mid}] OnDisconnectedFromServer. TrySetException={setExc}, reason={reason}, matchTaskAfter={matchCompletion?.Task.Status}");
            ReleaseRunner(runner, $"[match#{mid}] OnDisconnectedFromServer");
        }

        public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) {
            if (!ReferenceEquals(runner, this.runner)) {
                Debug.Log($"{LOG_PREFIX} [match#{currentMatchLogId}] OnConnectFailed ignored (runner mismatch).");
                return;
            }

            var mid = currentMatchLogId;
            var ex = new InvalidOperationException($"Fusion connection failed. reason={reason}");
            var setExc = matchCompletion?.TrySetException(ex) ?? false;
            Debug.LogWarning($"{LOG_PREFIX} [match#{mid}] OnConnectFailed. TrySetException={setExc}, reason={reason}, remote={remoteAddress}, matchTaskAfter={matchCompletion?.Task.Status}");
            ReleaseRunner(runner, $"[match#{mid}] OnConnectFailed");
        }

        public void OnPlayerLeft(NetworkRunner runner, PlayerRef player) { }
        public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
        public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
        public void OnInput(NetworkRunner runner, NetworkInput input) { }
        public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
        public void OnConnectedToServer(NetworkRunner runner) {
            if (!ReferenceEquals(runner, this.runner)) {
                Debug.Log($"{LOG_PREFIX} [match#{currentMatchLogId}] OnConnectedToServer ignored (runner mismatch).");
                return;
            }

            Debug.Log($"{LOG_PREFIX} [match#{currentMatchLogId}] OnConnectedToServer. isServer={runner.IsServer}, localPlayer={runner.LocalPlayer}, sessionName={runner.SessionInfo?.Name ?? "?"}");
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
