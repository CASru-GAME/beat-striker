using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Alice {
    // Owns the durable replacement protocol; progression rules belong to PlayerProfileService.
    internal sealed class PlayerRecordFileStore {
        readonly string directory;
        public string ProfilesPath => Path.Combine(directory, "profiles.json");
        public PlayerRecordFileStore(PlayerRecordStorage storage) {
            directory = storage.Directory;
            Directory.CreateDirectory(directory);
            RecoverReplacements();
        }
        public IEnumerable<string> BattlePaths => Directory.GetFiles(directory, "battle-*.json").OrderBy(File.GetCreationTimeUtc);
        public static bool IsBattleId(string id) => id != null && id.Length == 32 && id.All(c => c >= '0' && c <= '9' || c >= 'a' && c <= 'f');
        public string BattlePath(string id) {
            if (!IsBattleId(id)) throw new ArgumentException("A battle ID must contain 32 lowercase hexadecimal characters.");
            return Path.Combine(directory, $"battle-{id}.json");
        }
        public T Read<T>(string path) where T : class {
            var json = File.ReadAllText(path).Trim();
            if (!json.StartsWith("{", StringComparison.Ordinal) || !json.EndsWith("}", StringComparison.Ordinal)) throw new ArgumentException("Incomplete record JSON.");
            T result;
            if (typeof(T) == typeof(PlayerProfileCache)) {
                // Missing fields must not turn a truncated cache into an apparently valid empty cache.
                var cache = new PlayerProfileCache { slotNames = null, profiles = null, ranking = null, cpuLevel = 0 };
                JsonUtility.FromJsonOverwrite(json, cache);
                result = (T)(object)cache;
            } else result = JsonUtility.FromJson<T>(json);
            ValidateRecord(result);
            return result;
        }
        static bool ValidName(string value) => PlayerProfileService.TryNormalizeName(value, out var name) && name == value;
        static bool ValidTimestamp(string value) => value != null
            && Regex.IsMatch(value, @"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d{1,7})?(?:Z|[+-]\d{2}:\d{2})\z", RegexOptions.CultureInvariant)
            && DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out _);
        static bool ValidProfile(PlayerProfile profile) => profile != null && ValidName(profile.name) && profile.grade >= 0 && profile.grade <= 5
            && ValidTimestamp(profile.createdAt) && ValidTimestamp(profile.acquiredAt)
            && (string.IsNullOrEmpty(profile.lastTrialAt) || ValidTimestamp(profile.lastTrialAt)) && profile.lastTrialOpponent >= -1;
        static bool ValidText(string value, bool allowEmpty = false) => value != null && Encoding.UTF8.GetByteCount(value) <= 512 && (allowEmpty || !string.IsNullOrWhiteSpace(value));
        static bool ValidTextPair(string[] values) => values?.Length == 2 && values.All(value => ValidText(value, true));
        public static void ValidateBattle(BattleHistorySaveRequest battle) {
            if (battle == null || !IsBattleId(battle.battleId) || (battle.mode != "Local" && battle.mode != "Trial" && battle.mode != "Online")
                || battle.participants?.Length != 2 || battle.participants.Any(player => player == null || !ValidName(player.name) || player.grade < 0 || player.grade > 5
                    || player.isCpu && (player.cpuLevel < 1 || player.cpuLevel > 4))
                || !ValidTextPair(battle.playerNames) || !battle.participants.Select(player => player.name).SequenceEqual(battle.playerNames)
                || !ValidTextPair(battle.strikerNames) || !ValidText(battle.stage) || !ValidText(battle.musicId) || !ValidText(battle.appVersion)
                || battle.stageName != null && !ValidText(battle.stageName, true) || battle.musicName != null && !ValidText(battle.musicName, true)
                || battle.strikerIds?.Length != 2 || battle.roundWinCounts?.Length != 2 || battle.winnerPlayerId < -1 || battle.winnerPlayerId > 1 || !ValidTimestamp(battle.playedAt)) {
                throw new ArgumentException("Invalid battle record data.");
            }
            if (battle.mode == "Trial" && (battle.trialStartGrade < 0 || battle.trialStartGrade > 5
                || battle.trialTargetGrade != (battle.trialStartGrade < 5 ? battle.trialStartGrade + 1 : -1)
                || battle.participants[0].isCpu || !battle.participants[1].isCpu)) throw new ArgumentException("Invalid trial progression data.");
            var replay = battle.replayPayload;
            if (replay == null || replay.schemaVersion != 1 || replay.stage != battle.stage || replay.musicId != battle.musicId || replay.appVersion != battle.appVersion
                || replay.strikerIds?.Length != 2 || replay.rounds == null) throw new ArgumentException("Invalid battle replay data.");
        }
        static void ValidateRecord(object record) {
            if (record is BattleHistorySaveRequest battle) { ValidateBattle(battle); return; }
            if (record is PlayerProfileCache cache && cache.slotNames?.Length == 4 && cache.slotNames.All(name => string.IsNullOrEmpty(name) || ValidName(name))
                && cache.profiles != null && cache.ranking != null && cache.profiles.All(ValidProfile) && cache.ranking.All(ValidProfile)
                && cache.trialProgress != null && cache.trialProgress.All(p => p != null && ValidName(p.name) && p.championStreak >= 0
                    && (string.IsNullOrEmpty(p.lastResultAt) || ValidTimestamp(p.lastResultAt))
                    && (string.IsNullOrEmpty(p.lastBattleId) || IsBattleId(p.lastBattleId)))
                && cache.cpuLevel >= 1 && cache.cpuLevel <= 4) return;
            throw new ArgumentException("Invalid player record data.");
        }
        bool IsComplete(string path, string target) {
            try {
                if (Path.GetFileName(target) == "profiles.json") Read<PlayerProfileCache>(path);
                else {
                    var battle = Read<BattleHistorySaveRequest>(path);
                    if (BattlePath(battle.battleId) != target) return false;
                }
                return true;
            } catch (ArgumentException) { return false; }
        }
        void RecoverReplacements() {
            var targets = Directory.GetFiles(directory, "*.json.pending").Select(p => p.Substring(0, p.Length - ".pending".Length))
                .Concat(Directory.GetFiles(directory, "*.json.previous").Select(p => p.Substring(0, p.Length - ".previous".Length))).Distinct().ToArray();
            foreach (var target in targets) {
                var pending = target + ".pending";
                var previous = target + ".previous";
                foreach (var candidate in new[] { pending, target, previous }) {
                    if (File.Exists(candidate) && !IsComplete(candidate, target)) Quarantine(candidate);
                }
                if (File.Exists(pending)) {
                    if (File.Exists(target)) {
                        if (File.Exists(previous)) File.Delete(previous);
                        File.Move(target, previous);
                    }
                    File.Move(pending, target);
                }
                if (!File.Exists(target) && File.Exists(previous)) File.Move(previous, target);
                if (File.Exists(target)) CleanupPrevious(previous);
            }
            FlushBrowserStorage();
        }
        public void Quarantine(string path) {
            var retained = path + ".invalid-" + Guid.NewGuid().ToString("N");
            File.Move(path, retained);
            Debug.Log($"[PlayerProfiles] An unreadable record was retained for recovery: {Path.GetFileName(retained)}");
            FlushBrowserStorage();
        }
        public void Write(string path, object record) {
            ValidateRecord(record);
            var pending = path + ".pending";
            var previous = path + ".previous";
            File.WriteAllText(pending, JsonUtility.ToJson(record));
            if (File.Exists(previous)) File.Delete(previous);
            if (File.Exists(path)) File.Move(path, previous);
            try { File.Move(pending, path); }
            catch { if (File.Exists(previous)) File.Move(previous, path); throw; }
            // The new file is committed. Cleanup must not turn a successful commit into a failure.
            CleanupPrevious(previous);
            FlushBrowserStorage();
        }
        static void CleanupPrevious(string previous) {
            try { if (File.Exists(previous)) File.Delete(previous); }
            catch (IOException exception) { Debug.Log($"[PlayerProfiles] Previous record cleanup deferred: {exception.Message}"); }
            catch (UnauthorizedAccessException exception) { Debug.Log($"[PlayerProfiles] Previous record cleanup deferred: {exception.Message}"); }
        }
        public void Delete(string path) { File.Delete(path); FlushBrowserStorage(); }
        static void FlushBrowserStorage() {
#if UNITY_WEBGL && !UNITY_EDITOR
            BeatStrikerSyncPlayerRecords();
#endif
        }
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] static extern void BeatStrikerSyncPlayerRecords();
#endif
    }
}
