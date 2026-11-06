using System;

namespace Alice {
    public record PlayerRecordStorage(string Directory);
    [Serializable]
    public record PlayerProfile {
        public string name = "";
        public int grade;
        public string acquiredAt = "";
        public string createdAt = "";
        public int lastTrialOpponent = -1;
        public string lastTrialAt = "";
        public PlayerIdentity Identity => new() { name = name, grade = grade };
    }
    [Serializable]
    public record PlayerProfilePage {
        public PlayerProfile[] items = Array.Empty<PlayerProfile>();
        public string nextCursor = "";
    }
    [Serializable]
    public record TrialProgress {
        public string name = "";
        public bool introductionSeen;
        public int championStreak;
        public string lastBattleId = "";
        public string lastResultAt = "";
    }
    public interface ITrialProgressService {
        TrialProgress GetTrialProgress(string name);
        void CompleteTrialIntroduction(string name);
    }
    [Serializable]
    public record PlayerProfileCache {
        public string[] slotNames = new string[4];
        public PlayerProfile[] profiles = Array.Empty<PlayerProfile>();
        public PlayerProfile[] ranking = Array.Empty<PlayerProfile>();
        public int cpuLevel = 1;
        public TrialProgress[] trialProgress = Array.Empty<TrialProgress>();
    }
}
