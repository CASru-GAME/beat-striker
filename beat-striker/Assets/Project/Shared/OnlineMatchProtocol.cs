using System;
using System.Text;
using Fusion.Sockets;
using UnityEngine;

namespace Alice {
    public static class OnlineMatchProtocol {
        public const int MaxMatchPayloadLogChars = 512;

        public static readonly ReliableKey RequestKey = ReliableKey.FromInts(0x4253, 1, 1);
        public static readonly ReliableKey ResultKey = ReliableKey.FromInts(0x4253, 1, 2);

        public static byte[] SerializeRequest(OnlineMatchRequest request) {
            var payload = new MatchRequestPayload {
                striker = (int)request.LocalStriker,
                stage = (int)request.CandidateStage,
                musicId = request.CandidateMusicId,
                reservationId = request.ReservationId,
                duelSessionId = request.DuelSessionId,
            };
            return Encoding.UTF8.GetBytes(JsonUtility.ToJson(payload));
        }

        public static byte[] SerializeResult(OnlineMatchResult result) {
            var payload = new MatchResultPayload {
                localStriker = (int)result.LocalStriker,
                opponentStriker = (int)result.OpponentStriker,
                stage = (int)result.Stage,
                musicId = result.MusicId,
                localIsPlayer1 = result.LocalIsPlayer1,
            };
            return Encoding.UTF8.GetBytes(JsonUtility.ToJson(payload));
        }

        public static OnlineMatchRequest DeserializeRequest(ArraySegment<byte> data) {
            var json = Decode(data);
            var payload = JsonUtility.FromJson<MatchRequestPayload>(json);
            return new OnlineMatchRequest(
                (Striker)payload.striker,
                (Stage)payload.stage,
                payload.musicId,
                payload.reservationId ?? "",
                payload.duelSessionId ?? "");
        }

        public static OnlineMatchResult DeserializeResult(ArraySegment<byte> data) {
            if (!TryDeserializeResult(data, out var result, out var preview, out var failure)) {
                throw new InvalidOperationException($"DeserializeResult failed. preview={preview}, reason={failure}");
            }

            return result;
        }

        public static bool TryDeserializeResult(
            ArraySegment<byte> data,
            out OnlineMatchResult result,
            out string utf8Preview,
            out string failureMessage) {
            result = default;
            utf8Preview = "";
            failureMessage = null;
            try {
                var json = Decode(data);
                utf8Preview = TruncateForLog(json);
                var payload = JsonUtility.FromJson<MatchResultPayload>(json);
                result = new OnlineMatchResult(
                    (Striker)payload.localStriker,
                    (Striker)payload.opponentStriker,
                    (Stage)payload.stage,
                    payload.musicId ?? "",
                    payload.localIsPlayer1);
                return true;
            }
            catch (Exception exception) {
                failureMessage = exception.Message;
                return false;
            }
        }

        public static string TruncateForLog(string value) {
            if (string.IsNullOrEmpty(value)) {
                return "";
            }

            return value.Length <= MaxMatchPayloadLogChars
                ? value
                : value.Substring(0, MaxMatchPayloadLogChars) + "…";
        }

        static string Decode(ArraySegment<byte> data) {
            if (data.Array == null) {
                throw new InvalidOperationException("Reliable data payload is empty.");
            }

            return Encoding.UTF8.GetString(data.Array, data.Offset, data.Count);
        }

        [Serializable]
        class MatchRequestPayload {
            public int striker;
            public int stage;
            public string musicId;
            public string reservationId;
            public string duelSessionId;
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
