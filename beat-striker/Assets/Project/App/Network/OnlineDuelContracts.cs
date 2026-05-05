using System;

namespace Alice {
    [Serializable]
    public class DuelPromptRequest {
        public string duelSessionId;
        public string scene;
        public string state;
    }

    [Serializable]
    public class DuelPromptResponse {
        public DuelInviteDto incomingInvite;
        public DuelPresenceDto candidate;
        public DuelReservationDto reservation;
        public DuelPresenceDto opponentPresence;
    }

    [Serializable]
    public class DuelInviteCreateRequest {
        public string fromSessionId;
        public string toSessionId;
    }

    [Serializable]
    public class DuelInviteActionRequest {
        public string duelSessionId;
    }

    [Serializable]
    public class DuelReservationConsumeRequest {
        public string duelSessionId;
    }

    [Serializable]
    public class DuelInviteResponse {
        public DuelInviteDto invite;
        public DuelReservationDto reservation;
    }

    [Serializable]
    public class DuelReservationResponse {
        public DuelReservationDto reservation;
    }

    [Serializable]
    public class DuelInviteDto {
        public string id;
        public string fromSessionId;
        public string toSessionId;
        public string status;
    }

    [Serializable]
    public class DuelPresenceDto {
        public string duelSessionId;
        public string scene;
        public string state;
    }

    [Serializable]
    public class DuelReservationDto {
        public string id;
        public string inviteId;
        public string status;
        public string[] playerSessionIds;
        public string expiresAt;
    }
}
