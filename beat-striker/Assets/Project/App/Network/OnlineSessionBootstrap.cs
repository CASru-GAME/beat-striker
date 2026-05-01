using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Fusion;
using Fusion.Sockets;
using UnityEngine;

namespace Alice {
    public readonly struct OnlineMatchRequest {
        public readonly Striker LocalStriker;
        public readonly Stage CandidateStage;
        public readonly string CandidateMusicId;

        public OnlineMatchRequest(Striker localStriker, Stage candidateStage, string candidateMusicId) {
            LocalStriker = localStriker;
            CandidateStage = candidateStage;
            CandidateMusicId = candidateMusicId;
        }
    }

    public readonly struct OnlineMatchResult {
        public readonly Striker LocalStriker;
        public readonly Striker OpponentStriker;
        public readonly Stage Stage;
        public readonly string MusicId;
        public readonly bool LocalIsPlayer1;

        public OnlineMatchResult(Striker localStriker, Striker opponentStriker, Stage stage, string musicId, bool localIsPlayer1) {
            LocalStriker = localStriker;
            OpponentStriker = opponentStriker;
            Stage = stage;
            MusicId = musicId;
            LocalIsPlayer1 = localIsPlayer1;
        }
    }

    public interface IOnlineSessionBootstrap {
        Task<OnlineMatchResult> MatchAsync(OnlineMatchRequest request);
        void CancelMatchmaking();
    }

    public interface INetworkRunnerProvider {
        bool TryGetRunner(out NetworkRunner runner);
    }

    public class OnlineSessionBootstrap : IOnlineSessionBootstrap, INetworkRunnerProvider, INetworkRunnerCallbacks {
        const string LOG_PREFIX = "[OnlineSessionBootstrap]";
        static readonly ReliableKey RequestKey = ReliableKey.FromInts(0x4253, 1, 1);
        static readonly ReliableKey ResultKey = ReliableKey.FromInts(0x4253, 1, 2);

        readonly IAppNetworkSetting networkSetting;
        readonly Dictionary<PlayerRef, OnlineMatchRequest> requestsByPlayer = new();

        NetworkRunner runner;
        TaskCompletionSource<OnlineMatchResult> matchCompletion;
        OnlineMatchRequest localRequest;
        bool resultPublished;
        bool cancellationRequested;
        bool requestSentToServer;

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
            requestsByPlayer.Clear();
            resultPublished = false;
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

            Debug.Log($"{LOG_PREFIX} MatchAsync StartGame begin. mode=AutoHostOrClient, sessionName={networkSetting.SessionName}, playerCount=2");
            var startResult = await activeRunner.StartGame(new StartGameArgs {
                GameMode = GameMode.AutoHostOrClient,
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

            requestsByPlayer[activeRunner.LocalPlayer] = localRequest;
            if (!activeRunner.IsServer) {
                requestSentToServer = true;
                Debug.Log($"{LOG_PREFIX} MatchAsync sending request to server. localPlayer={activeRunner.LocalPlayer}");
                activeRunner.SendReliableDataToServer(RequestKey, SerializeRequest(localRequest));
            }

            TryPublishHostResult();
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

        void TryPublishHostResult() {
            if (resultPublished || runner == null || !runner.IsServer) {
                Debug.Log($"{LOG_PREFIX} TryPublishHostResult skipped. published={resultPublished}, hasRunner={runner != null}, isServer={runner != null && runner.IsServer}");
                return;
            }

            if (!TryGetOpponentPlayer(out var opponentPlayer)) {
                Debug.Log($"{LOG_PREFIX} TryPublishHostResult waiting for opponent. localPlayer={runner.LocalPlayer}");
                return;
            }

            if (!requestsByPlayer.TryGetValue(runner.LocalPlayer, out var hostRequest)
                || !requestsByPlayer.TryGetValue(opponentPlayer, out var opponentRequest)) {
                Debug.Log($"{LOG_PREFIX} TryPublishHostResult waiting for requests. hasHost={requestsByPlayer.ContainsKey(runner.LocalPlayer)}, hasOpponent={requestsByPlayer.ContainsKey(opponentPlayer)}");
                return;
            }

            resultPublished = true;
            var random = new System.Random(Environment.TickCount);
            var selectedStage = random.Next(2) == 0 ? hostRequest.CandidateStage : opponentRequest.CandidateStage;
            var selectedMusicId = random.Next(2) == 0 ? hostRequest.CandidateMusicId : opponentRequest.CandidateMusicId;

            var hostResult = new OnlineMatchResult(hostRequest.LocalStriker, opponentRequest.LocalStriker, selectedStage, selectedMusicId, true);
            var opponentResult = new OnlineMatchResult(opponentRequest.LocalStriker, hostRequest.LocalStriker, selectedStage, selectedMusicId, false);

            runner.SendReliableDataToPlayer(opponentPlayer, ResultKey, SerializeResult(opponentResult));
            matchCompletion.TrySetResult(hostResult);
            Debug.Log($"{LOG_PREFIX} Match decided. stage={selectedStage}, musicId={selectedMusicId}");
        }

        bool TryGetOpponentPlayer(out PlayerRef opponentPlayer) {
            foreach (var player in runner.ActivePlayers) {
                if (player != runner.LocalPlayer) {
                    opponentPlayer = player;
                    return true;
                }
            }

            opponentPlayer = default;
            return false;
        }

        static byte[] SerializeRequest(OnlineMatchRequest request) {
            var payload = new MatchRequestPayload {
                striker = (int)request.LocalStriker,
                stage = (int)request.CandidateStage,
                musicId = request.CandidateMusicId,
            };
            return Encoding.UTF8.GetBytes(JsonUtility.ToJson(payload));
        }

        static byte[] SerializeResult(OnlineMatchResult result) {
            var payload = new MatchResultPayload {
                localStriker = (int)result.LocalStriker,
                opponentStriker = (int)result.OpponentStriker,
                stage = (int)result.Stage,
                musicId = result.MusicId,
                localIsPlayer1 = result.LocalIsPlayer1,
            };
            return Encoding.UTF8.GetBytes(JsonUtility.ToJson(payload));
        }

        static OnlineMatchRequest DeserializeRequest(ArraySegment<byte> data) {
            var json = Decode(data);
            var payload = JsonUtility.FromJson<MatchRequestPayload>(json);
            return new OnlineMatchRequest((Striker)payload.striker, (Stage)payload.stage, payload.musicId);
        }

        static OnlineMatchResult DeserializeResult(ArraySegment<byte> data) {
            var json = Decode(data);
            var payload = JsonUtility.FromJson<MatchResultPayload>(json);
            return new OnlineMatchResult((Striker)payload.localStriker, (Striker)payload.opponentStriker, (Stage)payload.stage, payload.musicId, payload.localIsPlayer1);
        }

        static string Decode(ArraySegment<byte> data) {
            if (data.Array == null) {
                throw new InvalidOperationException("Reliable data payload is empty.");
            }

            return Encoding.UTF8.GetString(data.Array, data.Offset, data.Count);
        }

        public void OnPlayerJoined(NetworkRunner runner, PlayerRef player) {
            if (!ReferenceEquals(runner, this.runner)) {
                return;
            }

            Debug.Log($"{LOG_PREFIX} OnPlayerJoined. player={player}, isServer={runner.IsServer}, localPlayer={runner.LocalPlayer}");
            TryPublishHostResult();
        }

        public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) {
            if (!ReferenceEquals(runner, this.runner)) {
                return;
            }

            if (key == RequestKey && runner.IsServer) {
                Debug.Log($"{LOG_PREFIX} OnReliableDataReceived RequestKey. fromPlayer={player}, isServer={runner.IsServer}");
                requestsByPlayer[player] = DeserializeRequest(data);
                TryPublishHostResult();
                return;
            }

            if (key == ResultKey && !runner.IsServer) {
                Debug.Log($"{LOG_PREFIX} OnReliableDataReceived ResultKey. fromPlayer={player}, isServer={runner.IsServer}");
                matchCompletion?.TrySetResult(DeserializeResult(data));
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
                runner.SendReliableDataToServer(RequestKey, SerializeRequest(localRequest));
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

        [Serializable]
        class MatchRequestPayload {
            public int striker;
            public int stage;
            public string musicId;
        }

        [Serializable]
        class MatchResultPayload {
            public int localStriker;
            public int opponentStriker;
            public int stage;
            public string musicId;
            public bool localIsPlayer1;
        }
    }
}
