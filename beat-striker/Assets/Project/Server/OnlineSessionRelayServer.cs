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
        const float PresenceHeartbeatIntervalSeconds = 30f;
        const float PresenceHeartbeatGraceMultiplier = 4f;
        const float PresenceTtlSeconds = PresenceHeartbeatIntervalSeconds * PresenceHeartbeatGraceMultiplier;
        const float InviteTtlSeconds = 60f;

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
        readonly Dictionary<string, string> candidateBySession = new();
        readonly Dictionary<string, int> viewStateSeqBySession = new();
        readonly Dictionary<string, ComputedView> lastViewSnapshotBySession = new();
        readonly Dictionary<string, string> pendingViewMessageBySession = new();
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
                Debug.Log($"{LOG_PREFIX} StartServerAsync skipped because runner is already running.");
                return;
            }

            if (serverStartRequested) {
                Debug.Log($"{LOG_PREFIX} StartServerAsync skipped because server start is already requested.");
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
            Debug.Log($"{LOG_PREFIX} Received duel command. kind={kind}, player={player}, session={command.duelSessionId}");
            if (string.IsNullOrWhiteSpace(command.duelSessionId)) {
                SendError(player, "", "duelSessionId is required.");
                return;
            }

            RegisterSession(player, command.duelSessionId, command.scene, command.appOverlayEnabled, command.playerStatus, command.sceneSyncId);

            var affected = new HashSet<string>();
            affected.Add(command.duelSessionId);

            switch (kind) {
                case OnlineDuelCommandKind.PresenceUpdate:
                case OnlineDuelCommandKind.Resync:
                    AppendAllActiveSessions(affected);
                    break;
                case OnlineDuelCommandKind.InviteCreate:
                    if (!TryCreateInvite(player, command.duelSessionId, command.targetSessionId, affected)) {
                        return;
                    }
                    AppendAllActiveSessions(affected);
                    break;
                case OnlineDuelCommandKind.InviteAccept:
                    if (!TryAcceptInvite(player, command.duelSessionId, command.inviteId, affected)) {
                        return;
                    }
                    AppendAllActiveSessions(affected);
                    break;
                case OnlineDuelCommandKind.InviteReject:
                    FinishInvite(command.duelSessionId, command.inviteId, "rejected", "Invite rejected.", affected);
                    AppendAllActiveSessions(affected);
                    break;
                case OnlineDuelCommandKind.InviteCancel:
                    if (!string.IsNullOrWhiteSpace(command.reservationId)) {
                        CancelReservationByIdForSession(command.duelSessionId, command.reservationId, "Reservation canceled.", affected);
                    }
                    else {
                        FinishInvite(command.duelSessionId, command.inviteId, "canceled", "Invite canceled.", affected);
                    }
                    AppendAllActiveSessions(affected);
                    break;
                case OnlineDuelCommandKind.MatchCancel:
                    if (!string.IsNullOrWhiteSpace(command.reservationId)) {
                        CancelReservationByIdForSession(command.duelSessionId, command.reservationId, "Matchmaking canceled.", affected);
                    }
                    else {
                        CancelMatchRequestBySession(command.duelSessionId, "Matchmaking canceled.", affected);
                    }
                    AppendAllActiveSessions(affected);
                    break;
                case OnlineDuelCommandKind.ReservationConsume:
                    if (!TryConsumeReservation(command.duelSessionId, command.reservationId, affected)) {
                        return;
                    }
                    break;
                case OnlineDuelCommandKind.MatchRequest:
                    if (RegisterMatchRequest(player, command, affected)) {
                        return;
                    }
                    break;
                default:
                    SendError(player, command.duelSessionId, $"Unsupported duel command. kind={kind}");
                    return;
            }

            DispatchAffected(affected, kind.ToString());
        }

        void RegisterSession(PlayerRef player, string duelSessionId, string scene, bool appOverlayEnabled, OnlineDuelPlayerStatus playerStatus, int sceneSyncId) {
            if (sessionByPlayer.TryGetValue(player, out var previousSession) && previousSession != duelSessionId) {
                playerBySession.Remove(previousSession);
                viewStateSeqBySession.Remove(previousSession);
                lastViewSnapshotBySession.Remove(previousSession);
                pendingViewMessageBySession.Remove(previousSession);
                Debug.Log($"{LOG_PREFIX} Session remapped. player={player}, from={previousSession}, to={duelSessionId}");
            }

            sessionByPlayer[player] = duelSessionId;
            playerBySession[duelSessionId] = player;

            var resolvedScene = !string.IsNullOrWhiteSpace(scene)
                ? scene
                : presenceBySession.TryGetValue(duelSessionId, out var existingPresence)
                    ? existingPresence.Scene
                    : "";
            var resolvedSceneSyncId = sceneSyncId > 0
                ? sceneSyncId
                : presenceBySession.TryGetValue(duelSessionId, out var existingPresenceSync)
                    ? existingPresenceSync.SceneSyncId
                    : 0;

            presenceBySession[duelSessionId] = new PresenceState {
                SessionId = duelSessionId,
                Player = player,
                Scene = resolvedScene,
                SceneSyncId = resolvedSceneSyncId,
                AppOverlayEnabled = appOverlayEnabled,
                Status = playerStatus,
                ExpiresAt = Time.realtimeSinceStartup + PresenceTtlSeconds,
            };

            if (!appOverlayEnabled) {
                candidateBySession.Remove(duelSessionId);
                RemoveCandidateReferencesTo(duelSessionId);
            }

            Debug.Log($"{LOG_PREFIX} Presence updated. session={duelSessionId}, player={player}, scene={resolvedScene}, sceneSyncId={resolvedSceneSyncId}, overlay={appOverlayEnabled}, status={playerStatus}");
        }

        bool TryCreateInvite(PlayerRef player, string fromSessionId, string toSessionId, HashSet<string> affected) {
            if (string.IsNullOrWhiteSpace(toSessionId) || fromSessionId == toSessionId || !playerBySession.ContainsKey(toSessionId)) {
                SendError(player, fromSessionId, "Target player is not available.");
                return false;
            }

            if (!IsDuelCandidateEligible(fromSessionId)
                || !IsDuelCandidateEligible(toSessionId)
                || TryGetActiveReservation(fromSessionId, out _)
                || TryGetActiveReservation(toSessionId, out _)
                || HasPendingInvite(fromSessionId)
                || HasPendingInvite(toSessionId)) {
                SendError(player, fromSessionId, "Target player is not available.");
                return false;
            }

            CancelActiveReservationsForOverwrite("Reservation overwritten.", new[] { fromSessionId, toSessionId }, affected);

            inviteSequence++;
            invitesById[$"invite-{inviteSequence}"] = new InviteState {
                Id = $"invite-{inviteSequence}",
                FromSessionId = fromSessionId,
                ToSessionId = toSessionId,
                Status = "pending",
                ExpiresAt = Time.realtimeSinceStartup + InviteTtlSeconds,
            };

            candidateBySession.Remove(fromSessionId);
            candidateBySession.Remove(toSessionId);
            affected.Add(fromSessionId);
            affected.Add(toSessionId);
            Debug.Log($"{LOG_PREFIX} Invite created. from={fromSessionId}, to={toSessionId}");
            return true;
        }

        bool TryAcceptInvite(PlayerRef player, string duelSessionId, string inviteId, HashSet<string> affected) {
            if (!invitesById.TryGetValue(inviteId, out var invite) || invite.Status != "pending") {
                SendError(player, duelSessionId, "Invite is not pending.");
                return false;
            }

            if (invite.ToSessionId != duelSessionId) {
                SendError(player, duelSessionId, "Only invite target can accept.");
                return false;
            }

            if (Time.realtimeSinceStartup >= invite.ExpiresAt) {
                invite.Status = "expired";
                SetPendingMessage(invite.FromSessionId, "Invite expired.");
                SetPendingMessage(invite.ToSessionId, "Invite expired.");
                affected.Add(invite.FromSessionId);
                affected.Add(invite.ToSessionId);
                return true;
            }

            invite.Status = "accepted";
            CancelActiveReservationsForOverwrite("Reservation overwritten.", new[] { invite.FromSessionId, invite.ToSessionId }, affected);

            reservationSequence++;
            var reservation = new ReservationState {
                Id = $"reservation-{reservationSequence}",
                InviteId = invite.Id,
                Player1SessionId = invite.FromSessionId,
                Player2SessionId = invite.ToSessionId,
                ExpiresAt = float.PositiveInfinity,
            };
            reservationsById[reservation.Id] = reservation;
            candidateBySession.Remove(invite.FromSessionId);
            candidateBySession.Remove(invite.ToSessionId);
            affected.Add(invite.FromSessionId);
            affected.Add(invite.ToSessionId);
            Debug.Log($"{LOG_PREFIX} Reservation created. reservationId={reservation.Id}, inviteId={invite.Id}, p1={reservation.Player1SessionId}, p2={reservation.Player2SessionId}");
            return true;
        }

        void FinishInvite(string duelSessionId, string inviteId, string status, string message, HashSet<string> affected) {
            if (!invitesById.TryGetValue(inviteId, out var invite)) {
                return;
            }

            if (invite.FromSessionId != duelSessionId && invite.ToSessionId != duelSessionId) {
                return;
            }

            invite.Status = status;
            SetPendingMessage(invite.FromSessionId, message);
            SetPendingMessage(invite.ToSessionId, message);
            affected.Add(invite.FromSessionId);
            affected.Add(invite.ToSessionId);
            Debug.Log($"{LOG_PREFIX} Invite finished. inviteId={inviteId}, status={status}");
        }

        bool TryConsumeReservation(string duelSessionId, string reservationId, HashSet<string> affected) {
            if (!TryGetReservationFor(duelSessionId, reservationId, out var reservation)) {
                SetPendingMessage(duelSessionId, "Reservation expired.");
                affected.Add(duelSessionId);
                return true;
            }

            if (reservation.Player1SessionId == duelSessionId) {
                reservation.Player1Consumed = true;
            }
            else if (reservation.Player2SessionId == duelSessionId) {
                reservation.Player2Consumed = true;
            }

            affected.Add(reservation.Player1SessionId);
            affected.Add(reservation.Player2SessionId);
            return true;
        }

        bool CancelReservationByIdForSession(string duelSessionId, string reservationId, string message, HashSet<string> affected) {
            if (!TryGetReservationFor(duelSessionId, reservationId, out var reservation)) {
                return false;
            }

            CancelReservation(reservation, message, affected);
            return true;
        }

        void CancelReservation(ReservationState reservation, string message, HashSet<string> affected) {
            reservationsById.Remove(reservation.Id);
            matchRequestsBySession.Remove(reservation.Player1SessionId);
            matchRequestsBySession.Remove(reservation.Player2SessionId);
            SetPendingMessage(reservation.Player1SessionId, message);
            SetPendingMessage(reservation.Player2SessionId, message);
            affected.Add(reservation.Player1SessionId);
            affected.Add(reservation.Player2SessionId);
            Debug.Log($"{LOG_PREFIX} Reservation canceled. reservationId={reservation.Id}, p1={reservation.Player1SessionId}, p2={reservation.Player2SessionId}, message={message}");
        }

        void CancelActiveReservationsForOverwrite(string message, string[] overwritingSessionIds, HashSet<string> affected) {
            var reservations = new List<ReservationState>();
            foreach (var reservation in reservationsById.Values) {
                if (Time.realtimeSinceStartup >= reservation.ExpiresAt) {
                    continue;
                }

                if (ContainsSession(overwritingSessionIds, reservation.Player1SessionId)
                    || ContainsSession(overwritingSessionIds, reservation.Player2SessionId)) {
                    reservations.Add(reservation);
                }
            }

            for (var i = 0; i < reservations.Count; i++) {
                CancelReservationForOverwrite(reservations[i], message, overwritingSessionIds, affected);
            }
        }

        void CancelReservationForOverwrite(ReservationState reservation, string message, string[] overwritingSessionIds, HashSet<string> affected) {
            reservationsById.Remove(reservation.Id);
            matchRequestsBySession.Remove(reservation.Player1SessionId);
            matchRequestsBySession.Remove(reservation.Player2SessionId);

            if (!ContainsSession(overwritingSessionIds, reservation.Player1SessionId)) {
                SetPendingMessage(reservation.Player1SessionId, message);
            }
            if (!ContainsSession(overwritingSessionIds, reservation.Player2SessionId)) {
                SetPendingMessage(reservation.Player2SessionId, message);
            }

            affected.Add(reservation.Player1SessionId);
            affected.Add(reservation.Player2SessionId);
            Debug.Log($"{LOG_PREFIX} Reservation overwritten. reservationId={reservation.Id}, p1={reservation.Player1SessionId}, p2={reservation.Player2SessionId}, message={message}");
        }

        static bool ContainsSession(string[] sessionIds, string sessionId) {
            for (var i = 0; i < sessionIds.Length; i++) {
                if (sessionIds[i] == sessionId) {
                    return true;
                }
            }

            return false;
        }

        void CancelMatchRequestBySession(string duelSessionId, string message, HashSet<string> affected) {
            if (!matchRequestsBySession.Remove(duelSessionId)) {
                return;
            }

            SetPendingMessage(duelSessionId, message);
            affected.Add(duelSessionId);
            Debug.Log($"{LOG_PREFIX} Match request canceled. session={duelSessionId}, message={message}");
        }

        void CancelPendingInvitesBySession(string duelSessionId, string message, HashSet<string> affected) {
            var inviteIds = new List<string>();
            foreach (var invite in invitesById.Values) {
                if (invite.Status == "pending"
                    && (invite.FromSessionId == duelSessionId || invite.ToSessionId == duelSessionId)) {
                    inviteIds.Add(invite.Id);
                }
            }

            for (var i = 0; i < inviteIds.Count; i++) {
                FinishInvite(duelSessionId, inviteIds[i], "canceled", message, affected);
            }
        }

        bool RegisterMatchRequest(PlayerRef player, OnlineDuelCommandPayload command, HashSet<string> affected) {
            var request = new OnlineMatchRequest(
                (Striker)command.striker,
                (Stage)command.stage,
                command.musicId ?? "",
                command.reservationId ?? "",
                command.duelSessionId);
            if (string.IsNullOrWhiteSpace(request.ReservationId)) {
                SendError(player, command.duelSessionId, "reservationId is required.");
                return true;
            }

            matchRequestsBySession[command.duelSessionId] = request;
            Debug.Log($"{LOG_PREFIX} Match request registered. session={command.duelSessionId}, reservationId={request.ReservationId}, striker={request.LocalStriker}, stage={request.CandidateStage}, musicId={request.CandidateMusicId}");

            if (!TryGetReservationFor(command.duelSessionId, request.ReservationId, out var reservation)) {
                matchRequestsBySession.Remove(command.duelSessionId);
                SendError(player, command.duelSessionId, "Reservation not found.");
                return true;
            }

            TryConsumeReservation(command.duelSessionId, request.ReservationId, affected);
            if (TryPublishReservedMatchResult(reservation)) {
                return true;
            }

            affected.Add(reservation.Player1SessionId);
            affected.Add(reservation.Player2SessionId);
            return false;
        }

        bool TryPublishReservedMatchResult(ReservationState reservation) {
            if (!reservation.Player1Consumed || !reservation.Player2Consumed) {
                return false;
            }

            if (!matchRequestsBySession.TryGetValue(reservation.Player1SessionId, out var p1Request)
                || !matchRequestsBySession.TryGetValue(reservation.Player2SessionId, out var p2Request)) {
                return false;
            }

            PublishMatchResult(p1Request, p2Request, reservation.Id);
            reservationsById.Remove(reservation.Id);
            matchRequestsBySession.Remove(p1Request.DuelSessionId);
            matchRequestsBySession.Remove(p2Request.DuelSessionId);
            return true;
        }

        void PublishMatchResult(OnlineMatchRequest player1Request, OnlineMatchRequest player2Request, string reservationId) {
            var usePlayer1Pick = string.CompareOrdinal(player1Request.DuelSessionId, player2Request.DuelSessionId) <= 0;
            var selectedStage = usePlayer1Pick ? player1Request.CandidateStage : player2Request.CandidateStage;
            var selectedMusicId = usePlayer1Pick ? player1Request.CandidateMusicId : player2Request.CandidateMusicId;

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

            Debug.Log($"{LOG_PREFIX} Match decided. p1={player1Request.DuelSessionId}, p2={player2Request.DuelSessionId}, reservationId={reservationId}, stage={selectedStage}, musicId={selectedMusicId}");
        }

        void DispatchAffected(IEnumerable<string> sessionIds, string reason) {
            var unique = new HashSet<string>();
            foreach (var sessionId in sessionIds) {
                if (!string.IsNullOrWhiteSpace(sessionId)) {
                    unique.Add(sessionId);
                }
            }

            foreach (var sessionId in unique) {
                RecomputeAndDispatch(sessionId, reason);
            }
        }

        void RecomputeAndDispatch(string sessionId, string reason) {
            if (!playerBySession.TryGetValue(sessionId, out var player)) {
                return;
            }

            var message = ConsumePendingMessage(sessionId);
            var next = ComputeView(sessionId, message);
            if (lastViewSnapshotBySession.TryGetValue(sessionId, out var previous) && previous.Equals(next)) {
                return;
            }

            var nextSeq = viewStateSeqBySession.TryGetValue(sessionId, out var previousSeq)
                ? previousSeq + 1
                : 1;
            viewStateSeqBySession[sessionId] = nextSeq;
            lastViewSnapshotBySession[sessionId] = next;

            SendEvent(player, new OnlineDuelEventPayload {
                kind = (int)OnlineDuelEventKind.ViewState,
                seq = nextSeq,
                uiMode = (int)next.UiMode,
                localSessionId = next.LocalSessionId,
                candidateSessionId = next.CandidateSessionId,
                inviteId = next.InviteId,
                inviteFromSessionId = next.InviteFromSessionId,
                inviteToSessionId = next.InviteToSessionId,
                reservationId = next.ReservationId,
                opponentSessionId = next.OpponentSessionId,
                opponentScene = next.OpponentScene,
                opponentStatus = next.OpponentStatus,
                message = next.Message,
            });

            Debug.Log($"{LOG_PREFIX} ViewState dispatched. session={sessionId}, seq={nextSeq}, uiMode={next.UiMode}, reason={reason}");
        }

        ComputedView ComputeView(string sessionId, string message) {
            if (!IsDuelCandidateEligible(sessionId)) {
                candidateBySession.Remove(sessionId);
                return ComputedView.Idle(sessionId, message);
            }

            if (TryGetActiveReservation(sessionId, out var reservation)) {
                candidateBySession.Remove(sessionId);
                var opponentSessionId = reservation.Player1SessionId == sessionId
                    ? reservation.Player2SessionId
                    : reservation.Player1SessionId;
                return new ComputedView(
                    OnlineDuelUiMode.Matched,
                    sessionId,
                    "",
                    reservation.InviteId,
                    "",
                    "",
                    reservation.Id,
                    opponentSessionId,
                    TryGetScene(opponentSessionId),
                    ResolveOpponentStatus(sessionId, reservation),
                    message ?? "");
            }

            if (TryFindPendingIncomingInvite(sessionId, out var incomingInvite)) {
                candidateBySession.Remove(sessionId);
                return new ComputedView(
                    OnlineDuelUiMode.IncomingInvite,
                    sessionId,
                    "",
                    incomingInvite.Id,
                    incomingInvite.FromSessionId,
                    incomingInvite.ToSessionId,
                    "",
                    incomingInvite.FromSessionId,
                    TryGetScene(incomingInvite.FromSessionId),
                    ResolvePresenceStatus(incomingInvite.FromSessionId),
                    message ?? "");
            }

            if (TryFindPendingOutgoingInvite(sessionId, out var outgoingInvite)) {
                candidateBySession.Remove(sessionId);
                return new ComputedView(
                    OnlineDuelUiMode.InviteSent,
                    sessionId,
                    "",
                    outgoingInvite.Id,
                    outgoingInvite.FromSessionId,
                    outgoingInvite.ToSessionId,
                    "",
                    outgoingInvite.ToSessionId,
                    TryGetScene(outgoingInvite.ToSessionId),
                    ResolvePresenceStatus(outgoingInvite.ToSessionId),
                    message ?? "");
            }

            var candidateSessionId = ResolveCandidateSessionId(sessionId);
            if (!string.IsNullOrWhiteSpace(candidateSessionId)) {
                return new ComputedView(
                    OnlineDuelUiMode.Candidate,
                    sessionId,
                    candidateSessionId,
                    "",
                    "",
                    "",
                    "",
                    candidateSessionId,
                    TryGetScene(candidateSessionId),
                    ResolvePresenceStatus(candidateSessionId),
                    message ?? "");
            }

            candidateBySession.Remove(sessionId);
            return ComputedView.Idle(sessionId, message);
        }

        string ResolveCandidateSessionId(string viewerSessionId) {
            if (!IsDuelCandidateEligible(viewerSessionId)
                || TryGetActiveReservation(viewerSessionId, out _)
                || HasPendingInvite(viewerSessionId)) {
                candidateBySession.Remove(viewerSessionId);
                return "";
            }

            if (candidateBySession.TryGetValue(viewerSessionId, out var currentCandidate)
                && IsCandidateAvailableForViewer(viewerSessionId, currentCandidate)) {
                return currentCandidate;
            }

            foreach (var candidate in presenceBySession.Values) {
                if (IsCandidateAvailableForViewer(viewerSessionId, candidate.SessionId)) {
                    candidateBySession[viewerSessionId] = candidate.SessionId;
                    return candidate.SessionId;
                }
            }

            candidateBySession.Remove(viewerSessionId);
            return "";
        }

        bool IsCandidateAvailableForViewer(string viewerSessionId, string candidateSessionId) {
            return viewerSessionId != candidateSessionId
                   && IsDuelCandidateEligible(viewerSessionId)
                   && IsDuelCandidateEligible(candidateSessionId)
                   && !TryGetActiveReservation(viewerSessionId, out _)
                   && !TryGetActiveReservation(candidateSessionId, out _)
                   && !HasPendingInvite(viewerSessionId)
                   && !HasPendingInvite(candidateSessionId);
        }

        void SetPendingMessage(string sessionId, string message) {
            if (string.IsNullOrWhiteSpace(sessionId) || string.IsNullOrWhiteSpace(message)) {
                return;
            }

            pendingViewMessageBySession[sessionId] = message;
        }

        string ConsumePendingMessage(string sessionId) {
            if (!pendingViewMessageBySession.TryGetValue(sessionId, out var message)) {
                return "";
            }

            pendingViewMessageBySession.Remove(sessionId);
            return message ?? "";
        }

        void AppendAllActiveSessions(HashSet<string> affected) {
            foreach (var presence in presenceBySession.Values) {
                if (Time.realtimeSinceStartup < presence.ExpiresAt) {
                    affected.Add(presence.SessionId);
                }
            }
        }

        void SendError(PlayerRef player, string duelSessionId, string message) {
            var nextSeq = !string.IsNullOrWhiteSpace(duelSessionId) && viewStateSeqBySession.TryGetValue(duelSessionId, out var previousSeq)
                ? previousSeq + 1
                : 1;
            if (!string.IsNullOrWhiteSpace(duelSessionId)) {
                viewStateSeqBySession[duelSessionId] = nextSeq;
                lastViewSnapshotBySession[duelSessionId] = new ComputedView(
                    OnlineDuelUiMode.Error,
                    duelSessionId,
                    "",
                    "",
                    "",
                    "",
                    "",
                    "",
                    "",
                    OnlineDuelPlayerStatus.StageSelecting,
                    message ?? "");
            }

            SendEvent(player, new OnlineDuelEventPayload {
                kind = (int)OnlineDuelEventKind.ViewState,
                seq = nextSeq,
                uiMode = (int)OnlineDuelUiMode.Error,
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

            if (expiredInvites.Count > 0) {
                var affected = new HashSet<string>();
                for (var i = 0; i < expiredInvites.Count; i++) {
                    var invite = expiredInvites[i];
                    invite.Status = "expired";
                    SetPendingMessage(invite.FromSessionId, "Invite expired.");
                    SetPendingMessage(invite.ToSessionId, "Invite expired.");
                    affected.Add(invite.FromSessionId);
                    affected.Add(invite.ToSessionId);
                }
                AppendAllActiveSessions(affected);
                DispatchAffected(affected, "ExpireInvite");
                Debug.Log($"{LOG_PREFIX} Invite expired count={expiredInvites.Count}");
            }

            var expiredPresenceSessions = new List<string>();
            foreach (var presence in presenceBySession.Values) {
                if (now >= presence.ExpiresAt) {
                    expiredPresenceSessions.Add(presence.SessionId);
                }
            }

            for (var i = 0; i < expiredPresenceSessions.Count; i++) {
                RemoveSession(expiredPresenceSessions[i], "Presence expired.");
            }

            if (expiredPresenceSessions.Count > 0) {
                Debug.Log($"{LOG_PREFIX} Presence expired count={expiredPresenceSessions.Count}");
            }
        }

        void RemoveSession(string duelSessionId, string message) {
            var affected = new HashSet<string>();
            CancelPendingInvitesBySession(duelSessionId, message, affected);
            CancelReservationsBySession(duelSessionId, message, affected);

            if (playerBySession.TryGetValue(duelSessionId, out var player)) {
                sessionByPlayer.Remove(player);
            }

            playerBySession.Remove(duelSessionId);
            presenceBySession.Remove(duelSessionId);
            matchRequestsBySession.Remove(duelSessionId);
            candidateBySession.Remove(duelSessionId);
            viewStateSeqBySession.Remove(duelSessionId);
            lastViewSnapshotBySession.Remove(duelSessionId);
            pendingViewMessageBySession.Remove(duelSessionId);
            RemoveCandidateReferencesTo(duelSessionId);
            AppendAllActiveSessions(affected);
            DispatchAffected(affected, "RemoveSession");
            Debug.Log($"{LOG_PREFIX} Session removed. session={duelSessionId}, message={message}");
        }

        void CancelReservationsBySession(string duelSessionId, string message, HashSet<string> affected) {
            var reservations = new List<ReservationState>();
            foreach (var reservation in reservationsById.Values) {
                if (reservation.Player1SessionId == duelSessionId || reservation.Player2SessionId == duelSessionId) {
                    reservations.Add(reservation);
                }
            }

            for (var i = 0; i < reservations.Count; i++) {
                CancelReservation(reservations[i], message, affected);
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
                if (candidate.ToSessionId == duelSessionId
                    && candidate.Status == "pending"
                    && Time.realtimeSinceStartup < candidate.ExpiresAt) {
                    invite = candidate;
                    return true;
                }
            }

            invite = null;
            return false;
        }

        bool TryFindPendingOutgoingInvite(string duelSessionId, out InviteState invite) {
            foreach (var candidate in invitesById.Values) {
                if (candidate.FromSessionId == duelSessionId
                    && candidate.Status == "pending"
                    && Time.realtimeSinceStartup < candidate.ExpiresAt) {
                    invite = candidate;
                    return true;
                }
            }

            invite = null;
            return false;
        }

        bool HasPendingInvite(string duelSessionId) {
            foreach (var invite in invitesById.Values) {
                if (invite.Status == "pending"
                    && Time.realtimeSinceStartup < invite.ExpiresAt
                    && (invite.FromSessionId == duelSessionId || invite.ToSessionId == duelSessionId)) {
                    return true;
                }
            }

            return false;
        }

        bool IsDuelCandidateEligible(string duelSessionId) {
            return presenceBySession.TryGetValue(duelSessionId, out var presence)
                   && presence.AppOverlayEnabled
                   && playerBySession.ContainsKey(duelSessionId)
                   && Time.realtimeSinceStartup < presence.ExpiresAt;
        }

        string TryGetScene(string duelSessionId) {
            return presenceBySession.TryGetValue(duelSessionId, out var presence) ? presence.Scene : "";
        }

        OnlineDuelPlayerStatus ResolvePresenceStatus(string duelSessionId) {
            return presenceBySession.TryGetValue(duelSessionId, out var presence)
                ? presence.Status
                : OnlineDuelPlayerStatus.StageSelecting;
        }

        OnlineDuelPlayerStatus ResolveOpponentStatus(string localSessionId, ReservationState reservation) {
            var opponentSessionId = reservation.Player1SessionId == localSessionId
                ? reservation.Player2SessionId
                : reservation.Player1SessionId;
            if (IsReservationConsumedBy(reservation, opponentSessionId)) {
                return OnlineDuelPlayerStatus.Waiting;
            }

            return ResolvePresenceStatus(opponentSessionId);
        }

        static bool IsReservationConsumedBy(ReservationState reservation, string sessionId) {
            if (reservation.Player1SessionId == sessionId) {
                return reservation.Player1Consumed;
            }
            if (reservation.Player2SessionId == sessionId) {
                return reservation.Player2Consumed;
            }

            return false;
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
                RemoveSession(sessionId, "Opponent disconnected.");
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
            candidateBySession.Clear();
            viewStateSeqBySession.Clear();
            lastViewSnapshotBySession.Clear();
            pendingViewMessageBySession.Clear();
            serverStartRequested = false;
            this.runner = null;
            Debug.Log($"{LOG_PREFIX} Shutdown. reason={shutdownReason}");
        }

        void RemoveCandidateReferencesTo(string sessionId) {
            var sessionsToClear = new List<string>();
            foreach (var pair in candidateBySession) {
                if (pair.Value == sessionId) {
                    sessionsToClear.Add(pair.Key);
                }
            }

            for (var i = 0; i < sessionsToClear.Count; i++) {
                candidateBySession.Remove(sessionsToClear[i]);
            }
        }

        class PresenceState {
            public string SessionId;
            public PlayerRef Player;
            public string Scene;
            public int SceneSyncId;
            public bool AppOverlayEnabled;
            public OnlineDuelPlayerStatus Status;
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

        record ComputedView(
            OnlineDuelUiMode UiMode,
            string LocalSessionId,
            string CandidateSessionId,
            string InviteId,
            string InviteFromSessionId,
            string InviteToSessionId,
            string ReservationId,
            string OpponentSessionId,
            string OpponentScene,
            OnlineDuelPlayerStatus OpponentStatus,
            string Message) {
            public static ComputedView Idle(string localSessionId, string message) {
                return new ComputedView(
                    OnlineDuelUiMode.Idle,
                    localSessionId ?? "",
                    "",
                    "",
                    "",
                    "",
                    "",
                    "",
                    "",
                    OnlineDuelPlayerStatus.StageSelecting,
                    message ?? "");
            }
        }

        public void OnPlayerJoined(NetworkRunner runner, PlayerRef player) {
            if (!ReferenceEquals(runner, this.runner)) {
                return;
            }

            Debug.Log($"{LOG_PREFIX} Player joined. player={player}");
        }
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
            Debug.Log($"{LOG_PREFIX} Connect request accepted.");
            request.Accept();
        }
    }
}
