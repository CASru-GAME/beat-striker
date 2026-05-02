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
    }

    public interface INetworkRunnerProvider {
        bool TryGetRunner(out NetworkRunner runner);
    }

    public class OnlineSessionBootstrap : IOnlineSessionBootstrap, INetworkRunnerProvider, INetworkRunnerCallbacks {
        const string LOG_PREFIX = "[OnlineSessionBootstrap]";

        readonly IAppNetworkSetting networkSetting;

        NetworkRunner runner;
        TaskCompletionSource<OnlineMatchResult> matchCompletion;
        OnlineMatchRequest localRequest;
        bool cancellationRequested;
        bool requestSentToServer;

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
                throw new InvalidOperationException("Online matchmaking is already running.");
            }

            localRequest = request;
            cancellationRequested = false;
            requestSentToServer = false;
            matchCompletion = new TaskCompletionSource<OnlineMatchResult>(TaskCreationOptions.RunContinuationsAsynchronously);

            Debug.Log($"{LOG_PREFIX} MatchAsync begin. sessionName={networkSetting.SessionName}, timeout={networkSetting.MatchTimeoutSeconds:0.#}s, localStriker={request.LocalStriker}, stage={request.CandidateStage}, musicId={request.CandidateMusicId}");
            EnsureRunner();
            var activeRunner = runner;
            Debug.Log($"{LOG_PREFIX} MatchAsync runner ready. hasRunner={activeRunner != null}");
            var projectConfig = NetworkProjectConfig.Deserialize(
                NetworkProjectConfig.Serialize(NetworkProjectConfig.Global));
            var simulation = projectConfig.Simulation;
            simulation.Topology = Topologies.ClientServer;
            projectConfig.Simulation = simulation;

            Debug.Log($"{LOG_PREFIX} MatchAsync StartGame begin. mode=Client, sessionName={networkSetting.SessionName}, playerCount=2");
            var startResult = await activeRunner.StartGame(new StartGameArgs {
                GameMode = GameMode.Client,
                SessionName = networkSetting.SessionName,
                PlayerCount = 2,
                Config = projectConfig,
            });

            Debug.Log($"{LOG_PREFIX} MatchAsync StartGame completed. ok={startResult.Ok}, shutdownReason={startResult.ShutdownReason}, message={startResult.ErrorMessage}");

            if (!startResult.Ok) {
                if (startResult.ShutdownReason == ShutdownReason.OperationCanceled) {
                    var canceledException = new OperationCanceledException("Online matchmaking canceled by player.");
                    matchCompletion.TrySetException(canceledException);
                    ReleaseRunner();
                    throw canceledException;
                }

                var exception = new InvalidOperationException($"Fusion StartGame failed. reason={startResult.ShutdownReason}, message={startResult.ErrorMessage}");
                matchCompletion.TrySetException(exception);
                ReleaseRunner();
                throw exception;
            }

            if (activeRunner == null || !activeRunner.IsRunning) {
                var exception = new OperationCanceledException("Online matchmaking canceled before runner became ready.");
                matchCompletion.TrySetException(exception);
                ReleaseRunner(activeRunner);
                throw exception;
            }

            Debug.Log($"{LOG_PREFIX} MatchAsync runner running. isServer={activeRunner.IsServer}, localPlayer={activeRunner.LocalPlayer}");

            requestSentToServer = true;
            Debug.Log($"{LOG_PREFIX} MatchAsync sending request to server. localPlayer={activeRunner.LocalPlayer}");
            activeRunner.SendReliableDataToServer(OnlineMatchProtocol.RequestKey, OnlineMatchProtocol.SerializeRequest(localRequest));
            return await WaitForMatchAsync();
        }

        public void CancelMatchmaking() {
            if (matchCompletion == null || matchCompletion.Task.IsCompleted) {
                return;
            }

            cancellationRequested = true;
            matchCompletion.TrySetException(new OperationCanceledException("Online matchmaking canceled by player."));
            if (runner != null && runner.IsRunning) {
                Debug.Log($"{LOG_PREFIX} CancelMatchmaking shutting down runner. isServer={runner.IsServer}");
                runner.Shutdown();
                return;
            }

            Debug.Log($"{LOG_PREFIX} CancelMatchmaking release runner without shutdown.");
            ReleaseRunner();
        }

        void EnsureRunner() {
            if (runner != null && runner.IsRunning) {
                return;
            }

            ReleaseRunner();
            var runnerObject = new GameObject("OnlineSessionRunner");
            UnityEngine.Object.DontDestroyOnLoad(runnerObject);
            runner = runnerObject.AddComponent<NetworkRunner>();
            runner.AddCallbacks(this);
            Debug.Log($"{LOG_PREFIX} EnsureRunner created new runner. instanceId={runner.GetInstanceID()}");
        }

        void ReleaseRunner() {
            ReleaseRunner(runner);
        }

        void ReleaseRunner(NetworkRunner targetRunner) {
            if (targetRunner == null) {
                return;
            }

            if (ReferenceEquals(runner, targetRunner)) {
                runner = null;
            }

            targetRunner.RemoveCallbacks(this);
            var runnerObject = targetRunner.gameObject;
            if (runnerObject != null) {
                UnityEngine.Object.Destroy(runnerObject);
            }
            Debug.Log($"{LOG_PREFIX} ReleaseRunner completed. targetWasCurrent={ReferenceEquals(runner, targetRunner)}");
        }

        async Task<OnlineMatchResult> WaitForMatchAsync() {
            var timeout = Task.Delay(TimeSpan.FromSeconds(networkSetting.MatchTimeoutSeconds));
            var completedTask = await Task.WhenAny(matchCompletion.Task, timeout);
            if (completedTask != matchCompletion.Task) {
                var exception = new TimeoutException($"Online matchmaking timed out after {networkSetting.MatchTimeoutSeconds:0.#} seconds.");
                matchCompletion.TrySetException(exception);
                Debug.LogWarning($"{LOG_PREFIX} WaitForMatchAsync timeout. isServer={runner != null && runner.IsServer}, localPlayer={runner?.LocalPlayer}");
                throw exception;
            }

            Debug.Log($"{LOG_PREFIX} WaitForMatchAsync completed.");
            return await matchCompletion.Task;
        }

        public void OnPlayerJoined(NetworkRunner runner, PlayerRef player) {
            if (!ReferenceEquals(runner, this.runner)) {
                return;
            }

            Debug.Log($"{LOG_PREFIX} OnPlayerJoined. player={player}, isServer={runner.IsServer}, localPlayer={runner.LocalPlayer}");
        }

        public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) {
            if (!ReferenceEquals(runner, this.runner)) {
                return;
            }

            if (key == OnlineMatchProtocol.ResultKey) {
                Debug.Log($"{LOG_PREFIX} OnReliableDataReceived ResultKey. fromPlayer={player}, isServer={runner.IsServer}");
                matchCompletion?.TrySetResult(OnlineMatchProtocol.DeserializeResult(data));
            }
        }

        public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason) {
            if (shutdownReason != ShutdownReason.Ok && !cancellationRequested) {
                matchCompletion?.TrySetException(new InvalidOperationException($"Fusion shutdown. reason={shutdownReason}"));
            }

            Debug.Log($"{LOG_PREFIX} OnShutdown. reason={shutdownReason}, canceled={cancellationRequested}");
            ReleaseRunner(runner);
            cancellationRequested = false;
        }

        public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) {
            matchCompletion?.TrySetException(new InvalidOperationException($"Fusion disconnected. reason={reason}"));
            Debug.LogWarning($"{LOG_PREFIX} OnDisconnectedFromServer. reason={reason}");
            ReleaseRunner(runner);
        }

        public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) {
            matchCompletion?.TrySetException(new InvalidOperationException($"Fusion connection failed. reason={reason}"));
            Debug.LogWarning($"{LOG_PREFIX} OnConnectFailed. reason={reason}, remote={remoteAddress}");
            ReleaseRunner(runner);
        }

        public void OnPlayerLeft(NetworkRunner runner, PlayerRef player) { }
        public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
        public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
        public void OnInput(NetworkRunner runner, NetworkInput input) { }
        public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
        public void OnConnectedToServer(NetworkRunner runner) {
            if (!ReferenceEquals(runner, this.runner)) {
                return;
            }

            Debug.Log($"{LOG_PREFIX} OnConnectedToServer. isServer={runner.IsServer}, localPlayer={runner.LocalPlayer}");
            if (!runner.IsServer && !requestSentToServer && matchCompletion != null && !matchCompletion.Task.IsCompleted) {
                requestSentToServer = true;
                Debug.Log($"{LOG_PREFIX} OnConnectedToServer sending request to server.");
                runner.SendReliableDataToServer(OnlineMatchProtocol.RequestKey, OnlineMatchProtocol.SerializeRequest(localRequest));
            }
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
