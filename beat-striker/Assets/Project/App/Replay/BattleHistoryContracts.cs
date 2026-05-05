using System;
using UnityEngine;

namespace Alice {
    [Serializable]
    public class BattleHistoryListResponse {
        public BattleHistorySummary[] items = Array.Empty<BattleHistorySummary>();
    }

    [Serializable]
    public class BattleHistoryCreateResponse {
        public bool ok;
        public string id;
    }

    [Serializable]
    public class BattleHistorySummary {
        public string id;
        public string[] playerNames = Array.Empty<string>();
        public string stage;
        public string musicId;
        public string musicName;
        public string[] strikerNames = Array.Empty<string>();
        public int winnerPlayerId;
        public string playedAt;
        public string appVersion;
        public bool hasReplay;
    }

    [Serializable]
    public class BattleHistoryDetail {
        public string id;
        public string[] playerNames = Array.Empty<string>();
        public string stage;
        public string stageName;
        public string musicId;
        public string musicName;
        public string[] strikerNames = Array.Empty<string>();
        public int[] strikerIds = Array.Empty<int>();
        public int winnerPlayerId;
        public int[] roundWinCounts = Array.Empty<int>();
        public string playedAt;
        public string appVersion;
        public ReplayPayload replayPayload;
    }

    [Serializable]
    public class BattleHistorySaveRequest {
        public string[] playerNames = Array.Empty<string>();
        public string stage;
        public string stageName;
        public string musicId;
        public string musicName;
        public string[] strikerNames = Array.Empty<string>();
        public int[] strikerIds = Array.Empty<int>();
        public int winnerPlayerId;
        public int[] roundWinCounts = Array.Empty<int>();
        public string playedAt;
        public string appVersion;
        public ReplayPayload replayPayload;
    }

    [Serializable]
    public class ReplayPayload {
        public int schemaVersion = 1;
        public string stage;
        public string musicId;
        public int[] strikerIds = Array.Empty<int>();
        public string appVersion;
        public ReplayRoundPayload[] rounds = Array.Empty<ReplayRoundPayload>();
    }

    [Serializable]
    public class ReplayRoundPayload {
        public int roundNumber;
        public ReplayBeatNotificationPayload[] beatNotifications = Array.Empty<ReplayBeatNotificationPayload>();
        public ReplayPreBeatStatePayload[] preBeatStates = Array.Empty<ReplayPreBeatStatePayload>();
    }

    [Serializable]
    public class ReplayBeatNotificationPayload {
        public int playerId;
        public int beatIndex;
        public float time;
        public int kind;
        public int zone;
        public int button;
        public float directionX;
        public float directionY;
    }

    [Serializable]
    public class ReplayPreBeatStatePayload {
        public int playerId;
        public int applyBeatIndex;
        public float hitPoint;
        public float specialPoint;
        public Vector3 position;
        public string statePathId;
        public float playbackTime;
    }
}
