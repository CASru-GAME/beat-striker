using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Fusion;
using Fusion.Sockets;
using UnityEngine;

namespace Alice {
    public class OnlineSessionRelayServer : MonoBehaviour, INetworkRunnerCallbacks {
        const string LOG_PREFIX = "[OnlineSessionRelayServer]";
        const string DEFAULT_SESSION_NAME = "beat-striker-minimal";

        [SerializeField] string sessionName = DEFAULT_SESSION_NAME;
        [SerializeField, Min(2)] int playerCount = 2;
        [Tooltip("シーン読込時に Fusion を Server モードで起動する。クライアント用ビルドにこのコンポーネントが含まれる場合はオフにする。")]
        [SerializeField] bool startOnAwake;

        readonly Dictionary<PlayerRef, OnlineMatchRequest> requestsByPlayer = new();
        NetworkRunner runner;
        bool resultPublished;
        bool serverStartRequested;

        static OnlineSessionRelayServer activeRelayInstance;

        void Awake() {
            if (activeRelayInstance != null && activeRelayInstance != this) {
                Debug.LogWarning($"{LOG_PREFIX} Duplicate relay server GameObject destroyed.");
                Destroy(gameObject);
                return;
            }

            activeRelayInstance = this;

            if (!startOnAwake) {
                return;
            }

            _ = StartServerAsync();
        }

        void OnDestroy() {
            if (activeRelayInstance == this) {
                activeRelayInstance = null;
            }
        }

        async Task StartServerAsync() {
            if (runner != null && runner.IsRunning) {
                return;
            }

            if (serverStartRequested) {
                return;
            }

            serverStartRequested = true;

            runner = gameObject.AddComponent<NetworkRunner>();
            runner.AddCallbacks(this);

            var projectConfig = NetworkProjectConfig.Deserialize(
                NetworkProjectConfig.Serialize(NetworkProjectConfig.Global));
            var simulation = projectConfig.Simulation;
            simulation.Topology = Topologies.ClientServer;
            projectConfig.Simulation = simulation;

            Debug.Log($"{LOG_PREFIX} StartGame begin. sessionName={SessionName}, playerCount={PlayerCount}");
            var startResult = await runner.StartGame(new StartGameArgs {
                GameMode = GameMode.Server,
                SessionName = SessionName,
                PlayerCount = PlayerCount,
                Config = projectConfig,
            });

            if (startResult.Ok) {
                Debug.Log($"{LOG_PREFIX} StartGame completed.");
                return;
            }

            serverStartRequested = false;
            Debug.LogError($"{LOG_PREFIX} StartGame failed. reason={startResult.ShutdownReason}, message={startResult.ErrorMessage}");
        }

        string SessionName => string.IsNullOrWhiteSpace(sessionName) ? DEFAULT_SESSION_NAME : sessionName;
        int PlayerCount => Mathf.Max(2, playerCount);

        void TryPublishMatchResult() {
            if (resultPublished) {
                return;
            }

            var matchedPlayers = new List<PlayerRef>();
            foreach (var player in runner.ActivePlayers) {
                if (requestsByPlayer.ContainsKey(player)) {
                    matchedPlayers.Add(player);
                }
            }

            if (matchedPlayers.Count < PlayerCount) {
                Debug.Log($"{LOG_PREFIX} Waiting for match requests. count={matchedPlayers.Count}/{PlayerCount}");
                return;
            }

            matchedPlayers.Sort((a, b) => a.RawEncoded.CompareTo(b.RawEncoded));

            resultPublished = true;
            var player1 = matchedPlayers[0];
            var player2 = matchedPlayers[1];
            var player1Request = requestsByPlayer[player1];
            var player2Request = requestsByPlayer[player2];
            var random = new System.Random(Environment.TickCount);
            var selectedStage = random.Next(2) == 0 ? player1Request.CandidateStage : player2Request.CandidateStage;
            var selectedMusicId = random.Next(2) == 0 ? player1Request.CandidateMusicId : player2Request.CandidateMusicId;

            var player1Result = new OnlineMatchResult(player1Request.LocalStriker, player2Request.LocalStriker, selectedStage, selectedMusicId, true);
            var player2Result = new OnlineMatchResult(player2Request.LocalStriker, player1Request.LocalStriker, selectedStage, selectedMusicId, false);

            runner.SendReliableDataToPlayer(player1, OnlineMatchProtocol.ResultKey, OnlineMatchProtocol.SerializeResult(player1Result));
            runner.SendReliableDataToPlayer(player2, OnlineMatchProtocol.ResultKey, OnlineMatchProtocol.SerializeResult(player2Result));
            Debug.Log($"{LOG_PREFIX} Match decided. player1={player1}, player2={player2}, stage={selectedStage}, musicId={selectedMusicId}");
        }

        void RelayReliableData(ReliableKey key, PlayerRef sender, ArraySegment<byte> data) {
            if (data.Array == null) {
                throw new InvalidOperationException("Reliable data payload is empty.");
            }

            var bytes = new byte[data.Count];
            Buffer.BlockCopy(data.Array, data.Offset, bytes, 0, data.Count);
            foreach (var player in runner.ActivePlayers) {
                if (player == sender) {
                    continue;
                }

                runner.SendReliableDataToPlayer(player, key, bytes);
            }

            Debug.Log($"{LOG_PREFIX} Relayed reliable data. key={key}, fromPlayer={sender}, bytes={bytes.Length}");
        }

        public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) {
            if (!ReferenceEquals(runner, this.runner)) {
                return;
            }

            if (key == OnlineMatchProtocol.RequestKey) {
                requestsByPlayer[player] = OnlineMatchProtocol.DeserializeRequest(data);
                Debug.Log($"{LOG_PREFIX} Received match request. player={player}");
                TryPublishMatchResult();
                return;
            }

            if (OnlineBattleProtocol.IsRelayKey(key)) {
                RelayReliableData(key, player, data);
            }
        }

        public void OnPlayerLeft(NetworkRunner runner, PlayerRef player) {
            if (!ReferenceEquals(runner, this.runner)) {
                return;
            }

            requestsByPlayer.Remove(player);
            resultPublished = false;
            Debug.Log($"{LOG_PREFIX} Player left. player={player}");
        }

        public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason) {
            if (!ReferenceEquals(runner, this.runner)) {
                return;
            }

            requestsByPlayer.Clear();
            resultPublished = false;
            serverStartRequested = false;
            this.runner = null;
            Debug.Log($"{LOG_PREFIX} Shutdown. reason={shutdownReason}");
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
        public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) { }
        public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }

        public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) {
            request.Accept();
        }
    }
}
