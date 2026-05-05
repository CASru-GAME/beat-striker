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
        const float PresenceTtlSeconds = 120f;
        const float InviteTtlSeconds = 60f;
        const float ReservationTtlSeconds = 180f;

        [SerializeField] string sessionName = DEFAULT_SESSION_NAME;
        [SerializeField, Min(2)] int playerCount = 32;
        [Tooltip("シーン読込時に Fusion を Server モードで起動する。クライアント用ビルドにこのコンポーネントが含まれる場合はオフにする。")]
        [SerializeField] bool startOnAwake;

        readonly Dictionary<PlayerRef, string> sessionByPlayer = new();
        readonly Dictionary<string, PlayerRef> playerBySession = new();
        readonly Dictionary<string, PresenceState> presenceBySession = new();
        readonly Dictionary<string, InviteState> invitesById = new();
        readonly Dictionary<string, ReservationState> reservationsById = new();
        readonly Dictionary<string, OnlineMatchRequest> matchRequestsBySession = new();
        readonly Dictionary<PlayerRef, PlayerRef> battleOpponentByPlayer = new();
        NetworkRunner runner;
        bool serverStartRequested;
        int inviteSequence;
        int reservationSequence;

        static OnlineSessionRelayServer activeRelayInstance;

        void Awake() {
            if (activeRelayInstance != null && activeRelayInstance != this) {
                Debug.LogWarning($"{LOG_PREFIX} Duplicate relay server GameObject destroyed.");
                Destroy(gameObject);
                return;
            }

            activeRelayInstance = this;

            if (startOnAwake) {
                _ = StartServerAsync();
            }
        }

        void Update() {
            ExpireServerState();
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

        void HandleDuelCommand(PlayerRef player, OnlineDuelCommandPayload command) {
            var kind = (OnlineDuelCommandKind)command.kind;
            if (string.IsNullOrWhiteSpace(command.duelSessionId)) {
                SendError(player, "", "duelSessionId is required.");
                return;
            }

            RegisterSession(player, command.duelSessionId, command.scene);

            switch (kind) {
                case OnlineDuelCommandKind.PresenceUpdate:
                case OnlineDuelCommandKind.Resync:
                    PublishSnapshot(player, command.duelSessionId);
                    PublishCandidateFor(command.duelSessionId);
                    break;
                case OnlineDuelCommandKind.InviteCreate:
                    CreateInvite(player, command.duelSessionId, command.targetSessionId);
                    break;
                case OnlineDuelCommandKind.InviteAccept:
                    AcceptInvite(player, command.duelSessionId, command.inviteId);
                    break;
                case OnlineDuelCommandKind.InviteReject:
                    FinishInvite(command.duelSessionId, command.inviteId, "rejected");
                    break;
                case OnlineDuelCommandKind.InviteCancel:
                    FinishInvite(command.duelSessionId, command.inviteId, "canceled");
                    break;
                case OnlineDuelCommandKind.ReservationConsume:
                    ConsumeReservation(command.duelSessionId, command.reservationId);
                    break;
                case OnlineDuelCommandKind.MatchRequest:
                    RegisterMatchRequest(command);
                    break;
                default:
                    SendError(player, command.duelSessionId, $"Unsupported duel command. kind={kind}");
                    break;
            }
        }

        void RegisterSession(PlayerRef player, string duelSessionId, string scene) {
            if (sessionByPlayer.TryGetValue(player, out var previousSession) && previousSession != duelSessionId) {
                playerBySession.Remove(previousSession);
            }

            sessionByPlayer[player] = duelSessionId;
            playerBySession[duelSessionId] = player;
            var resolvedScene = !string.IsNullOrWhiteSpace(scene)
                ? scene
                : presenceBySession.TryGetValue(duelSessionId, out var existingPresence)
                    ? existingPresence.Scene
                    : "";
            presenceBySession[duelSessionId] = new PresenceState {
                SessionId = duelSessionId,
                Player = player,
                Scene = resolvedScene,
                ExpiresAt = Time.realtimeSinceStartup + PresenceTtlSeconds,
            };
        }

        void PublishSnapshot(PlayerRef player, string duelSessionId) {
            if (TryGetActiveReservation(duelSessionId, out var reservation)) {
                SendReservedEvent(duelSessionId, reservation, OnlineDuelEventKind.Snapshot);
                return;
            }

            if (TryFindPendingIncomingInvite(duelSessionId, out var invite)) {
                SendEvent(player, new OnlineDuelEventPayload {
                    kind = (int)OnlineDuelEventKind.IncomingInvite,
                    localSessionId = duelSessionId,
                    inviteId = invite.Id,
                    inviteFromSessionId = invite.FromSessionId,
                    inviteToSessionId = invite.ToSessionId,
                    opponentSessionId = invite.FromSessionId,
                    opponentScene = TryGetScene(invite.FromSessionId),
                });
                return;
            }

            SendEvent(player, new OnlineDuelEventPayload {
                kind = (int)OnlineDuelEventKind.Snapshot,
                localSessionId = duelSessionId,
            });
        }

        void PublishCandidateFor(string localSessionId) {
            if (!playerBySession.TryGetValue(localSessionId, out var localPlayer)
                || TryGetActiveReservation(localSessionId, out _)
                || TryFindPendingIncomingInvite(localSessionId, out _)) {
                return;
            }

            foreach (var candidate in presenceBySession.Values) {
                if (candidate.SessionId == localSessionId || !IsPresenceActive(candidate.SessionId) || TryGetActiveReservation(candidate.SessionId, out _)) {
                    continue;
                }

                SendEvent(localPlayer, new OnlineDuelEventPayload {
                    kind = (int)OnlineDuelEventKind.CandidateShown,
                    localSessionId = localSessionId,
                    candidateSessionId = candidate.SessionId,
                    opponentSessionId = candidate.SessionId,
                    opponentScene = candidate.Scene,
                });
                return;
            }
        }

        void CreateInvite(PlayerRef player, string fromSessionId, string toSessionId) {
            if (string.IsNullOrWhiteSpace(toSessionId) || fromSessionId == toSessionId || !playerBySession.TryGetValue(toSessionId, out var targetPlayer)) {
                SendError(player, fromSessionId, "Target player is not available.");
                return;
            }

            if (TryGetActiveReservation(fromSessionId, out _) || TryGetActiveReservation(toSessionId, out _)) {
                SendError(player, fromSessionId, "Player is already reserved.");
                return;
            }

            inviteSequence++;
            var invite = new InviteState {
                Id = $"invite-{inviteSequence}",
                FromSessionId = fromSessionId,
                ToSessionId = toSessionId,
                Status = "pending",
                ExpiresAt = Time.realtimeSinceStartup + InviteTtlSeconds,
            };
            invitesById[invite.Id] = invite;

            SendEvent(player, new OnlineDuelEventPayload {
                kind = (int)OnlineDuelEventKind.InviteUpdated,
                localSessionId = fromSessionId,
                inviteId = invite.Id,
                inviteFromSessionId = invite.FromSessionId,
                inviteToSessionId = invite.ToSessionId,
                opponentSessionId = toSessionId,
                opponentScene = TryGetScene(toSessionId),
            });
            SendEvent(targetPlayer, new OnlineDuelEventPayload {
                kind = (int)OnlineDuelEventKind.IncomingInvite,
                localSessionId = toSessionId,
                inviteId = invite.Id,
                inviteFromSessionId = invite.FromSessionId,
                inviteToSessionId = invite.ToSessionId,
                opponentSessionId = fromSessionId,
                opponentScene = TryGetScene(fromSessionId),
            });
            Debug.Log($"{LOG_PREFIX} Invite created. inviteId={invite.Id}, from={fromSessionId}, to={toSessionId}");
        }

        void AcceptInvite(PlayerRef player, string duelSessionId, string inviteId) {
            if (!invitesById.TryGetValue(inviteId, out var invite) || invite.Status != "pending") {
                SendError(player, duelSessionId, "Invite is not pending.");
                return;
            }

            if (invite.ToSessionId != duelSessionId) {
                SendError(player, duelSessionId, "Only invite target can accept.");
                return;
            }

            if (Time.realtimeSinceStartup >= invite.ExpiresAt) {
                invite.Status = "expired";
                SendReservationExpired(invite.FromSessionId, "");
                SendReservationExpired(invite.ToSessionId, "");
                return;
            }

            invite.Status = "accepted";
            reservationSequence++;
            var reservation = new ReservationState {
                Id = $"reservation-{reservationSequence}",
                InviteId = invite.Id,
                Player1SessionId = invite.FromSessionId,
                Player2SessionId = invite.ToSessionId,
                ExpiresAt = Time.realtimeSinceStartup + ReservationTtlSeconds,
            };
            reservationsById[reservation.Id] = reservation;
            SendReservedEvent(invite.FromSessionId, reservation, OnlineDuelEventKind.Reserved);
            SendReservedEvent(invite.ToSessionId, reservation, OnlineDuelEventKind.Reserved);
            Debug.Log($"{LOG_PREFIX} Reservation created. reservationId={reservation.Id}, inviteId={invite.Id}, p1={reservation.Player1SessionId}, p2={reservation.Player2SessionId}");
        }

        void FinishInvite(string duelSessionId, string inviteId, string status) {
            if (!invitesById.TryGetValue(inviteId, out var invite)) {
                return;
            }

            if (invite.FromSessionId != duelSessionId && invite.ToSessionId != duelSessionId) {
                return;
            }

            invite.Status = status;
            SendIdle(invite.FromSessionId, $"Invite {status}.");
            SendIdle(invite.ToSessionId, $"Invite {status}.");
            Debug.Log($"{LOG_PREFIX} Invite finished. inviteId={inviteId}, status={status}");
        }

        void ConsumeReservation(string duelSessionId, string reservationId) {
            if (!TryGetReservationFor(duelSessionId, reservationId, out var reservation)) {
                SendReservationExpired(duelSessionId, reservationId);
                return;
            }

            if (reservation.Player1SessionId == duelSessionId) {
                reservation.Player1Consumed = true;
            }
            else if (reservation.Player2SessionId == duelSessionId) {
                reservation.Player2Consumed = true;
            }

            SendMatchStatus(reservation);
            TryPublishReservedMatchResult(reservation);
        }

        void RegisterMatchRequest(OnlineDuelCommandPayload command) {
            var request = new OnlineMatchRequest(
                (Striker)command.striker,
                (Stage)command.stage,
                command.musicId ?? "",
                command.reservationId ?? "",
                command.duelSessionId);
            matchRequestsBySession[command.duelSessionId] = request;

            if (!string.IsNullOrWhiteSpace(request.ReservationId) && TryGetReservationFor(command.duelSessionId, request.ReservationId, out var reservation)) {
                ConsumeReservation(command.duelSessionId, request.ReservationId);
                TryPublishReservedMatchResult(reservation);
                return;
            }

            TryPublishRandomMatchResult();
        }

        void TryPublishReservedMatchResult(ReservationState reservation) {
            if (!reservation.Player1Consumed || !reservation.Player2Consumed) {
                return;
            }

            if (!matchRequestsBySession.TryGetValue(reservation.Player1SessionId, out var p1Request)
                || !matchRequestsBySession.TryGetValue(reservation.Player2SessionId, out var p2Request)) {
                return;
            }

            PublishMatchResult(p1Request, p2Request, reservation.Id);
            reservationsById.Remove(reservation.Id);
        }

        void TryPublishRandomMatchResult() {
            var candidates = new List<OnlineMatchRequest>();
            foreach (var request in matchRequestsBySession.Values) {
                if (string.IsNullOrWhiteSpace(request.ReservationId) && playerBySession.ContainsKey(request.DuelSessionId)) {
                    candidates.Add(request);
                }
            }

            if (candidates.Count < 2) {
                return;
            }

            candidates.Sort((a, b) => string.CompareOrdinal(a.DuelSessionId, b.DuelSessionId));
            PublishMatchResult(candidates[0], candidates[1], "");
        }

        void PublishMatchResult(OnlineMatchRequest player1Request, OnlineMatchRequest player2Request, string reservationId) {
            var random = new System.Random(Environment.TickCount);
            var selectedStage = random.Next(2) == 0 ? player1Request.CandidateStage : player2Request.CandidateStage;
            var selectedMusicId = random.Next(2) == 0 ? player1Request.CandidateMusicId : player2Request.CandidateMusicId;

            var p1 = playerBySession[player1Request.DuelSessionId];
            var p2 = playerBySession[player2Request.DuelSessionId];
            battleOpponentByPlayer[p1] = p2;
            battleOpponentByPlayer[p2] = p1;

            SendEvent(p1, new OnlineDuelEventPayload {
                kind = (int)OnlineDuelEventKind.MatchResult,
                localSessionId = player1Request.DuelSessionId,
                reservationId = reservationId,
                opponentSessionId = player2Request.DuelSessionId,
                localStriker = (int)player1Request.LocalStriker,
                opponentStriker = (int)player2Request.LocalStriker,
                stage = (int)selectedStage,
                musicId = selectedMusicId,
                localIsPlayer1 = true,
            });
            SendEvent(p2, new OnlineDuelEventPayload {
                kind = (int)OnlineDuelEventKind.MatchResult,
                localSessionId = player2Request.DuelSessionId,
                reservationId = reservationId,
                opponentSessionId = player1Request.DuelSessionId,
                localStriker = (int)player2Request.LocalStriker,
                opponentStriker = (int)player1Request.LocalStriker,
                stage = (int)selectedStage,
                musicId = selectedMusicId,
                localIsPlayer1 = false,
            });

            matchRequestsBySession.Remove(player1Request.DuelSessionId);
            matchRequestsBySession.Remove(player2Request.DuelSessionId);
            Debug.Log($"{LOG_PREFIX} Match decided. p1={player1Request.DuelSessionId}, p2={player2Request.DuelSessionId}, reservationId={reservationId}, stage={selectedStage}, musicId={selectedMusicId}");
        }

        void SendReservedEvent(string localSessionId, ReservationState reservation, OnlineDuelEventKind kind) {
            if (!playerBySession.TryGetValue(localSessionId, out var player)) {
                return;
            }

            var opponent = reservation.Player1SessionId == localSessionId
                ? reservation.Player2SessionId
                : reservation.Player1SessionId;
            SendEvent(player, new OnlineDuelEventPayload {
                kind = (int)kind,
                localSessionId = localSessionId,
                reservationId = reservation.Id,
                inviteId = reservation.InviteId,
                opponentSessionId = opponent,
                opponentScene = TryGetScene(opponent),
            });
        }

        void SendMatchStatus(ReservationState reservation) {
            SendReservedEvent(reservation.Player1SessionId, reservation, OnlineDuelEventKind.MatchStatus);
            SendReservedEvent(reservation.Player2SessionId, reservation, OnlineDuelEventKind.MatchStatus);
        }

        void SendReservationExpired(string duelSessionId, string reservationId) {
            if (!playerBySession.TryGetValue(duelSessionId, out var player)) {
                return;
            }

            SendEvent(player, new OnlineDuelEventPayload {
                kind = (int)OnlineDuelEventKind.ReservationExpired,
                localSessionId = duelSessionId,
                reservationId = reservationId,
                message = "Reservation expired.",
            });
        }

        void SendIdle(string duelSessionId, string message) {
            if (!playerBySession.TryGetValue(duelSessionId, out var player)) {
                return;
            }

            SendEvent(player, new OnlineDuelEventPayload {
                kind = (int)OnlineDuelEventKind.Snapshot,
                localSessionId = duelSessionId,
                message = message,
            });
        }

        void SendError(PlayerRef player, string duelSessionId, string message) {
            SendEvent(player, new OnlineDuelEventPayload {
                kind = (int)OnlineDuelEventKind.Error,
                localSessionId = duelSessionId,
                message = message,
            });
        }

        void SendEvent(PlayerRef player, OnlineDuelEventPayload payload) {
            runner.SendReliableDataToPlayer(player, OnlineDuelProtocol.EventKey, OnlineDuelProtocol.SerializeEvent(payload));
        }

        void RelayReliableData(ReliableKey key, PlayerRef sender, ArraySegment<byte> data) {
            if (!battleOpponentByPlayer.TryGetValue(sender, out var opponent) || !IsActivePlayer(opponent)) {
                Debug.LogWarning($"{LOG_PREFIX} Battle relay skipped because opponent pair is missing. key={key}, sender={sender}");
                return;
            }

            if (data.Array == null) {
                throw new InvalidOperationException("Reliable data payload is empty.");
            }

            var bytes = new byte[data.Count];
            Buffer.BlockCopy(data.Array, data.Offset, bytes, 0, data.Count);
            runner.SendReliableDataToPlayer(opponent, key, bytes);
            Debug.Log($"{LOG_PREFIX} Relayed reliable data to battle opponent. key={key}, fromPlayer={sender}, toPlayer={opponent}, bytes={bytes.Length}");
        }

        void ExpireServerState() {
            var now = Time.realtimeSinceStartup;
            var expiredInvites = new List<InviteState>();
            foreach (var invite in invitesById.Values) {
                if (invite.Status == "pending" && now >= invite.ExpiresAt) {
                    expiredInvites.Add(invite);
                }
            }

            foreach (var invite in expiredInvites) {
                invite.Status = "expired";
                SendIdle(invite.FromSessionId, "Invite expired.");
                SendIdle(invite.ToSessionId, "Invite expired.");
            }

            var expiredReservations = new List<ReservationState>();
            foreach (var reservation in reservationsById.Values) {
                if (now >= reservation.ExpiresAt) {
                    expiredReservations.Add(reservation);
                }
            }

            foreach (var reservation in expiredReservations) {
                reservationsById.Remove(reservation.Id);
                SendReservationExpired(reservation.Player1SessionId, reservation.Id);
                SendReservationExpired(reservation.Player2SessionId, reservation.Id);
            }
        }

        bool TryGetActiveReservation(string duelSessionId, out ReservationState reservation) {
            foreach (var candidate in reservationsById.Values) {
                if ((candidate.Player1SessionId == duelSessionId || candidate.Player2SessionId == duelSessionId)
                    && Time.realtimeSinceStartup < candidate.ExpiresAt) {
                    reservation = candidate;
                    return true;
                }
            }

            reservation = null;
            return false;
        }

        bool TryGetReservationFor(string duelSessionId, string reservationId, out ReservationState reservation) {
            if (!string.IsNullOrWhiteSpace(reservationId)
                && reservationsById.TryGetValue(reservationId, out reservation)
                && (reservation.Player1SessionId == duelSessionId || reservation.Player2SessionId == duelSessionId)
                && Time.realtimeSinceStartup < reservation.ExpiresAt) {
                return true;
            }

            reservation = null;
            return false;
        }

        bool TryFindPendingIncomingInvite(string duelSessionId, out InviteState invite) {
            foreach (var candidate in invitesById.Values) {
                if (candidate.ToSessionId == duelSessionId && candidate.Status == "pending" && Time.realtimeSinceStartup < candidate.ExpiresAt) {
                    invite = candidate;
                    return true;
                }
            }

            invite = null;
            return false;
        }

        bool IsPresenceActive(string duelSessionId) {
            return presenceBySession.TryGetValue(duelSessionId, out var presence)
                   && playerBySession.ContainsKey(duelSessionId)
                   && Time.realtimeSinceStartup < presence.ExpiresAt;
        }

        string TryGetScene(string duelSessionId) {
            return presenceBySession.TryGetValue(duelSessionId, out var presence) ? presence.Scene : "";
        }

        bool IsActivePlayer(PlayerRef target) {
            foreach (var player in runner.ActivePlayers) {
                if (player == target) {
                    return true;
                }
            }

            return false;
        }

        public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) {
            if (!ReferenceEquals(runner, this.runner)) {
                return;
            }

            if (key == OnlineDuelProtocol.CommandKey) {
                HandleDuelCommand(player, OnlineDuelProtocol.DeserializeCommand(data));
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

            if (sessionByPlayer.TryGetValue(player, out var sessionId)) {
                sessionByPlayer.Remove(player);
                playerBySession.Remove(sessionId);
                presenceBySession.Remove(sessionId);
                matchRequestsBySession.Remove(sessionId);
            }

            if (battleOpponentByPlayer.TryGetValue(player, out var opponent)) {
                battleOpponentByPlayer.Remove(opponent);
                battleOpponentByPlayer.Remove(player);
            }

            Debug.Log($"{LOG_PREFIX} Player left. player={player}");
        }

        public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason) {
            if (!ReferenceEquals(runner, this.runner)) {
                return;
            }

            sessionByPlayer.Clear();
            playerBySession.Clear();
            presenceBySession.Clear();
            invitesById.Clear();
            reservationsById.Clear();
            matchRequestsBySession.Clear();
            battleOpponentByPlayer.Clear();
            serverStartRequested = false;
            this.runner = null;
            Debug.Log($"{LOG_PREFIX} Shutdown. reason={shutdownReason}");
        }

        class PresenceState {
            public string SessionId;
            public PlayerRef Player;
            public string Scene;
            public float ExpiresAt;
        }

        class InviteState {
            public string Id;
            public string FromSessionId;
            public string ToSessionId;
            public string Status;
            public float ExpiresAt;
        }

        class ReservationState {
            public string Id;
            public string InviteId;
            public string Player1SessionId;
            public string Player2SessionId;
            public bool Player1Consumed;
            public bool Player2Consumed;
            public float ExpiresAt;
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
