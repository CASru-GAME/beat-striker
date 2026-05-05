using System;
using System.Text;
using Fusion.Sockets;
using UnityEngine;

namespace Alice {
    public enum OnlineDuelPhase {
        Idle,
        CandidateShown,
        IncomingInvite,
        InviteSent,
        Reserved,
        Consumed,
        EnterBattle,
        Error,
    }

    public enum OnlineDuelCommandKind {
        PresenceUpdate,
        InviteCreate,
        InviteAccept,
        InviteReject,
        InviteCancel,
        ReservationConsume,
        MatchRequest,
        Resync,
    }

    public enum OnlineDuelEventKind {
        CandidateShown,
        IncomingInvite,
        InviteUpdated,
        Reserved,
        ReservationExpired,
        MatchStatus,
        MatchResult,
        Error,
        Snapshot,
    }

    public record OnlineDuelUiState(
        OnlineDuelPhase Phase,
        string LocalSessionId,
        string CandidateSessionId,
        string InviteId,
        string InviteFromSessionId,
        string InviteToSessionId,
        string ReservationId,
        string OpponentSessionId,
        string OpponentScene,
        string Message,
        OnlineMatchResult MatchResult) {
        public static OnlineDuelUiState Idle(string localSessionId) {
            return new OnlineDuelUiState(
                OnlineDuelPhase.Idle,
                localSessionId ?? "",
                "",
                "",
                "",
                "",
                "",
                "",
                "",
                "",
                default);
        }

        public bool HasReservation => !string.IsNullOrWhiteSpace(ReservationId);
    }

    public static class OnlineDuelProtocol {
        public const int MaxDuelPayloadLogChars = 512;

        public static readonly ReliableKey CommandKey = ReliableKey.FromInts(0x4253, 3, 1);
        public static readonly ReliableKey EventKey = ReliableKey.FromInts(0x4253, 3, 2);

        public static byte[] SerializeCommand(OnlineDuelCommandPayload payload) {
            return Encoding.UTF8.GetBytes(JsonUtility.ToJson(payload));
        }

        public static byte[] SerializeEvent(OnlineDuelEventPayload payload) {
            return Encoding.UTF8.GetBytes(JsonUtility.ToJson(payload));
        }

        public static OnlineDuelCommandPayload DeserializeCommand(ArraySegment<byte> data) {
            return JsonUtility.FromJson<OnlineDuelCommandPayload>(Decode(data));
        }

        public static OnlineDuelEventPayload DeserializeEvent(ArraySegment<byte> data) {
            return JsonUtility.FromJson<OnlineDuelEventPayload>(Decode(data));
        }

        public static string TruncateForLog(string value) {
            if (string.IsNullOrEmpty(value)) {
                return "";
            }

            return value.Length <= MaxDuelPayloadLogChars
                ? value
                : value.Substring(0, MaxDuelPayloadLogChars) + "...";
        }

        static string Decode(ArraySegment<byte> data) {
            if (data.Array == null) {
                throw new InvalidOperationException("Reliable data payload is empty.");
            }

            return Encoding.UTF8.GetString(data.Array, data.Offset, data.Count);
        }
    }

    [Serializable]
    public class OnlineDuelCommandPayload {
        public int kind;
        public string duelSessionId;
        public string scene;
        public string inviteId;
        public string targetSessionId;
        public string reservationId;
        public int striker;
        public int stage;
        public string musicId;
    }

    [Serializable]
    public class OnlineDuelEventPayload {
        public int kind;
        public string localSessionId;
        public string candidateSessionId;
        public string inviteId;
        public string inviteFromSessionId;
        public string inviteToSessionId;
        public string reservationId;
        public string opponentSessionId;
        public string opponentScene;
        public string message;
        public int localStriker;
        public int opponentStriker;
        public int stage;
        public string musicId;
        public bool localIsPlayer1;
    }
}
