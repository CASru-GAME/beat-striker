using System;
using System.Collections.Generic;
using R3;

namespace Alice {
    public enum BattlePlayMode { Local, Trial, Online, Replay, Tutorial }

    public class PlayerSession : IDisposable {
        readonly Subject<Unit> changed = new();
        bool disposed;
        readonly Dictionary<int, string> slotNames = new();
        PlayerIdentity[] participants = Array.Empty<PlayerIdentity>();
        public BattlePlayMode Mode { get; private set; }
        public string BattleId { get; private set; } = "";
        public int TrialTargetGrade { get; private set; } = -1;
        public int TrialStartGrade { get; private set; } = -1;
        public string TrialPlayerName { get; private set; } = "";
        public bool IsTrial => Mode == BattlePlayMode.Trial;
        public Observable<Unit> Changed => changed;
        public IReadOnlyList<PlayerIdentity> Participants => participants;
        public string GetName(int slot) => slotNames.TryGetValue(slot, out var name) ? name : "";
        public void SetName(int slot, string name) { slotNames[slot] = name; changed.OnNext(Unit.Default); }
        public void SetMode(BattlePlayMode mode) {
            if (Mode != mode) {
                participants = Array.Empty<PlayerIdentity>();
                BattleId = ""; TrialStartGrade = -1; TrialTargetGrade = -1; TrialPlayerName = "";
            }
            Mode = mode; changed.OnNext(Unit.Default);
        }
        public void Prepare(PlayerIdentity[] identities, string battleId = null, int trialStartGrade = -1) {
            participants = Array.ConvertAll(identities, identity => identity with { });
            BattleId = battleId ?? Guid.NewGuid().ToString("N");
            TrialStartGrade = trialStartGrade;
            TrialTargetGrade = IsTrial && trialStartGrade < 5 ? trialStartGrade + 1 : -1;
            TrialPlayerName = IsTrial ? identities[0].name : "";
            changed.OnNext(Unit.Default);
        }
        public void Dispose() {
            if (disposed) return;
            disposed = true;
            changed.Dispose();
        }
    }
}
