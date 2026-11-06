using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using R3;
using UnityEngine;
using VContainer.Unity;

namespace Alice {
    public interface IPlayerProfileService {
        PlayerProfile Get(string name);
        Task<PlayerProfile> SelectNameAsync(int slot, string name);
        void RememberName(int slot, string name);
        int CpuLevel { get; set; }
        IReadOnlyList<PlayerProfile> CachedRanking { get; }
        void CacheRanking(IEnumerable<PlayerProfile> profiles);
        void Record(BattleHistorySaveRequest request);
        Task SynchronizeAsync();
        Observable<Unit> Changed { get; }
    }

    public class PlayerProfileService : IPlayerProfileService, ITrialProgressService, IInitializable, ITickable, IDisposable {
        public const string GuestName = "ゲスト";
        public static string EnteredNameOrGuest(string input) => string.IsNullOrWhiteSpace(input) ? GuestName : input;
        readonly IPlayerRecordsApiClient recordsApi;
        readonly IBattleHistoryApiClient historyApi;
        readonly PlayerSession session;
        readonly PlayerRecordFileStore files;
        readonly Subject<Unit> changed = new();
        PlayerProfileCache cache;
        bool syncing, disposed;
        float nextSync;
        public Observable<Unit> Changed => changed;
        public IReadOnlyList<PlayerProfile> CachedRanking => cache.ranking.Concat(cache.profiles)
            .GroupBy(profile => profile.name).Select(group => group.OrderByDescending(profile => profile.grade).ThenBy(profile => profile.acquiredAt, StringComparer.Ordinal).First() with { }).ToArray();
        public int CpuLevel { get => cache.cpuLevel; set => Commit(cache with { cpuLevel = Math.Clamp(value, 1, 4) }); }

        public PlayerProfileService(IPlayerRecordsApiClient recordsApi, IBattleHistoryApiClient historyApi, PlayerSession session, PlayerRecordStorage storage) {
            this.recordsApi = recordsApi; this.historyApi = historyApi; this.session = session;
            files = new PlayerRecordFileStore(storage);
            cache = new PlayerProfileCache();
            if (File.Exists(files.ProfilesPath)) {
                try { cache = files.Read<PlayerProfileCache>(files.ProfilesPath); }
                catch (ArgumentException) { files.Quarantine(files.ProfilesPath); }
            }
            foreach (var path in files.BattlePaths.ToArray()) {
                var request = ReadQueuedBattle(path);
                if (request != null) cache = ApplyResult(cache, request);
            }
            Commit(cache);
            for (var slot = 0; slot < cache.slotNames.Length; slot++) session.SetName(slot, cache.slotNames[slot] ?? "");
        }

        public void Initialize() { _ = SynchronizeAsync(); }
        public void Tick() { if (Time.unscaledTime < nextSync) return; nextSync = Time.unscaledTime + 30; _ = SynchronizeAsync(); }
        public static bool TryNormalizeName(string input, out string name) {
            name = input?.Trim() ?? "";
            return name.Length > 0 && !name.Any(char.IsControl) && new StringInfo(name).LengthInTextElements <= 10;
        }
        static PlayerProfile NewProfile(string name) {
            if (!TryNormalizeName(name, out var normalized) || normalized != name) throw new ArgumentException("名前は1〜10文字で入力してください。");
            var now = DateTime.UtcNow.ToString("O");
            return new PlayerProfile { name = name, acquiredAt = now, createdAt = now };
        }
        public PlayerProfile Get(string name) {
            var existing = cache.profiles.FirstOrDefault(p => p.name == name);
            if (existing == null) {
                existing = NewProfile(name);
                Commit(cache with { profiles = cache.profiles.Append(existing).ToArray() });
            }
            // Consumers receive snapshots; only this service can change durable progression.
            return existing with { };
        }
        public void RememberName(int slot, string name) {
            if (slot < 0 || slot >= cache.slotNames.Length) throw new ArgumentOutOfRangeException(nameof(slot));
            if (!TryNormalizeName(name, out var normalized)) throw new ArgumentException("名前は1〜10文字で入力してください。");
            var slots = (string[])cache.slotNames.Clone(); slots[slot] = normalized;
            var profiles = cache.profiles.Any(p => p.name == normalized) ? cache.profiles : cache.profiles.Append(NewProfile(normalized)).ToArray();
            Commit(cache with { slotNames = slots, profiles = profiles });
            session.SetName(slot, normalized);
        }
        public async Task<PlayerProfile> SelectNameAsync(int slot, string name) {
            RememberName(slot, name);
            var local = Get(session.GetName(slot));
            try {
                var remote = await recordsApi.GetProfileAsync(local);
                if (!disposed) MergeAndSave(remote);
            } catch (ApiRequestException exception) { Debug.Log($"[PlayerProfiles] Using local record: {exception.Message}"); }
            return Get(local.name);
        }
        void MergeAndSave(PlayerProfile remote) {
            var local = cache.profiles.FirstOrDefault(p => p.name == remote.name) ?? NewProfile(remote.name);
            var merged = local with { };
            if (remote.grade > local.grade || remote.grade == local.grade && Earlier(remote.acquiredAt, local.acquiredAt)) {
                merged.grade = remote.grade; merged.acquiredAt = remote.acquiredAt;
            }
            if (Earlier(remote.createdAt, local.createdAt)) merged.createdAt = remote.createdAt;
            if (Earlier(local.lastTrialAt, remote.lastTrialAt) || string.IsNullOrEmpty(local.lastTrialAt) && !string.IsNullOrEmpty(remote.lastTrialAt)) {
                merged.lastTrialAt = remote.lastTrialAt; merged.lastTrialOpponent = remote.lastTrialOpponent;
            }
            if (merged != local || !cache.profiles.Any(p => p.name == remote.name)) Commit(ReplaceProfile(cache, merged));
        }
        static bool Earlier(string a, string b) => DateTimeOffset.TryParse(a, out var first) && (!DateTimeOffset.TryParse(b, out var second) || first < second);
        static PlayerProfileCache ReplaceProfile(PlayerProfileCache source, PlayerProfile profile) => source with {
            profiles = source.profiles.Where(p => p.name != profile.name).Append(profile).ToArray()
        };
        public void CacheRanking(IEnumerable<PlayerProfile> profiles) => Commit(cache with { ranking = profiles.Select(p => p with { }).ToArray() });
        public TrialProgress GetTrialProgress(string name) =>
            (cache.trialProgress.FirstOrDefault(p => p.name == name) ?? new TrialProgress { name = name }) with { };
        public void CompleteTrialIntroduction(string name) {
            var progress = GetTrialProgress(name) with { introductionSeen = true };
            Commit(ReplaceTrialProgress(cache, progress));
        }
        static PlayerProfileCache ReplaceTrialProgress(PlayerProfileCache source, TrialProgress progress) => source with {
            trialProgress = source.trialProgress.Where(p => p.name != progress.name).Append(progress).ToArray()
        };
        public void Record(BattleHistorySaveRequest request) {
            PlayerRecordFileStore.ValidateBattle(request);
            var path = files.BattlePath(request.battleId);
            files.Write(path, request);
            var next = ApplyResult(cache, request);
            try { Commit(next); }
            // The outbox already durably contains this result even if the profile cache cannot be replaced.
            catch (IOException) { cache = next; changed.OnNext(Unit.Default); throw; }
            catch (UnauthorizedAccessException) { cache = next; changed.OnNext(Unit.Default); throw; }
            _ = SynchronizeAsync();
        }
        static PlayerProfileCache ApplyResult(PlayerProfileCache source, BattleHistorySaveRequest request) {
            if (request.mode != "Trial" || request.participants.Length != 2) return source;
            var existing = source.profiles.FirstOrDefault(p => p.name == request.participants[0].name) ?? NewProfile(request.participants[0].name);
            var profile = existing with { };
            if (request.winnerPlayerId == 0 && request.trialTargetGrade > profile.grade && request.trialTargetGrade <= 5) {
                profile.grade = request.trialTargetGrade; profile.acquiredAt = request.playedAt;
            } else if (request.winnerPlayerId == 0 && request.trialTargetGrade == profile.grade && Earlier(request.playedAt, profile.acquiredAt)) profile.acquiredAt = request.playedAt;
            if (string.IsNullOrEmpty(profile.lastTrialAt) || !Earlier(request.playedAt, profile.lastTrialAt)) {
                profile.lastTrialOpponent = request.strikerIds[1]; profile.lastTrialAt = request.playedAt;
            }
            var next = ReplaceProfile(source, profile);
            var progress = source.trialProgress.FirstOrDefault(p => p.name == profile.name) ?? new TrialProgress { name = profile.name };
            // The outbox is applied again during recovery and upload. Count each result only once.
            if (progress.lastBattleId == request.battleId || !string.IsNullOrEmpty(progress.lastResultAt) && !Earlier(progress.lastResultAt, request.playedAt)) return next;
            return ReplaceTrialProgress(next, progress with {
                introductionSeen = true,
                championStreak = request.trialStartGrade == 5 && request.winnerPlayerId == 0 ? progress.championStreak + 1 : 0,
                lastBattleId = request.battleId,
                lastResultAt = request.playedAt
            });
        }
        BattleHistorySaveRequest ReadQueuedBattle(string path) {
            try { return files.Read<BattleHistorySaveRequest>(path); }
            catch (ArgumentException) { files.Quarantine(path); return null; }
        }
        async Task SynchronizeProfilesAsync() {
            foreach (var profile in cache.profiles.ToArray()) {
                if (disposed) return;
                try {
                    var remote = await recordsApi.GetProfileAsync(profile with { });
                    if (disposed) return;
                    MergeAndSave(remote);
                } catch (ApiRequestException exception) when (!exception.IsTransient) {
                    Debug.Log($"[PlayerProfiles] Profile synchronization rejected; local record retained: {exception.Message}");
                }
            }
        }
        public async Task SynchronizeAsync() {
            if (syncing || disposed) return;
            syncing = true;
            try {
                await SynchronizeProfilesAsync();
                foreach (var path in files.BattlePaths.ToArray()) {
                    if (disposed) return;
                    var request = ReadQueuedBattle(path);
                    if (request == null) continue;
                    // Persist recovered progression before removing its durable outbox copy.
                    Commit(ApplyResult(cache, request));
                    try { await historyApi.SaveAsync(request); }
                    catch (ApiRequestException exception) when (!exception.IsTransient) {
                        Debug.Log($"[PlayerProfiles] Battle {request.battleId} rejected; retained for retry: {exception.Message}");
                        continue;
                    }
                    if (disposed) return;
                    files.Delete(path);
                }
                await SynchronizeProfilesAsync();
            } catch (Exception exception) { Debug.Log($"[PlayerProfiles] Records remain queued: {exception.Message}"); }
            finally { syncing = false; }
        }
        void Commit(PlayerProfileCache next) {
            files.Write(files.ProfilesPath, next);
            cache = next;
            changed.OnNext(Unit.Default);
        }
        public void Dispose() { disposed = true; changed.Dispose(); }
    }
}
