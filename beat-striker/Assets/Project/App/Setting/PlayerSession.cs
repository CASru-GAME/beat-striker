using System;
using System.Collections.Generic;
using System.Linq;
using R3;

namespace Alice {
    public enum BattlePlayMode { Local, Trial, Online, Replay, Tutorial }
    internal record PlayerFeedbackCandidate(int Slot, string Name);

    public class PlayerSession : IDisposable {
        readonly Subject<Unit> changed = new();
        bool disposed;
        readonly Dictionary<int, string> slotNames = new();
        PlayerIdentity[] participants = Array.Empty<PlayerIdentity>();
        (AppScene Destination, PlayerFeedbackCandidate[] Candidates)? pendingFeedback;
        public BattlePlayMode Mode { get; private set; }
        public string BattleId { get; private set; } = "";
        public int TrialTargetGrade { get; private set; } = -1;
        public int TrialStartGrade { get; private set; } = -1;
        public int TrialStartStreak { get; private set; }
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
                TrialStartStreak = 0;
            }
            Mode = mode; changed.OnNext(Unit.Default);
        }
        public void Prepare(PlayerIdentity[] identities, string battleId = null, int trialStartGrade = -1, int trialStartStreak = 0) {
            participants = Array.ConvertAll(identities, identity => identity with { });
            BattleId = battleId ?? Guid.NewGuid().ToString("N");
            TrialStartGrade = trialStartGrade;
            TrialStartStreak = trialStartStreak;
            TrialTargetGrade = IsTrial && trialStartGrade < 5 ? trialStartGrade + 1 : -1;
            TrialPlayerName = IsTrial ? identities[0].name : "";
            changed.OnNext(Unit.Default);
        }
        internal void QueueFeedback(AppScene destination) {
            pendingFeedback = (destination, participants
                .Select((player, slot) => new { player, slot })
                .Where(entry => !entry.player.isCpu && !string.IsNullOrWhiteSpace(entry.player.name) && entry.player.name != PlayerProfileService.GuestName)
                .GroupBy(entry => entry.player.name, StringComparer.Ordinal)
                .Select(group => new PlayerFeedbackCandidate(group.First().slot, group.Key))
                .ToArray());
        }
        internal PlayerFeedbackCandidate[] TakeFeedback(AppScene destination) {
            var pending = pendingFeedback;
            pendingFeedback = null;
            return pending.HasValue && pending.Value.Destination == destination
                ? pending.Value.Candidates : Array.Empty<PlayerFeedbackCandidate>();
        }
        public void Dispose() {
            if (disposed) return;
            disposed = true;
            changed.Dispose();
        }
    }
}
