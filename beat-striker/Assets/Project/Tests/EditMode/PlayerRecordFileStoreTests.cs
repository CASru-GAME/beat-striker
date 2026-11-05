using System;
using System.IO;
using System.Threading.Tasks;
using Alice;
using NUnit.Framework;
using UnityEngine;

public class PlayerRecordFileStoreTests {
    string directory;
    readonly OfflineApi api = new();

    [SetUp]
    public void SetUp() {
        directory = Path.Combine(Path.GetTempPath(), "beat-striker-record-validation-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
    }

    [TearDown]
    public void TearDown() {
        Assert.That(Path.GetDirectoryName(Path.GetFullPath(directory)), Is.EqualTo(Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar)));
        if (Directory.Exists(directory)) Directory.Delete(directory, true);
    }

    [TestCase("name")]
    [TestCase("target")]
    [TestCase("kind")]
    [TestCase("replay")]
    public void InvalidOutboxIsQuarantinedBeforeProgressionIsApplied(string defect) {
        var request = Battle();
        if (defect == "name") request.participants[0].name = request.playerNames[0] = "";
        if (defect == "target") request.trialTargetGrade = 5;
        if (defect == "kind") request.participants[1].isCpu = false;
        if (defect == "replay") request.replayPayload = null;
        File.WriteAllText(BattlePath(request), JsonUtility.ToJson(request));
        using var session = new PlayerSession();
        using var profiles = Open(session);
        Assert.That(profiles.CachedRanking, Is.Empty);
        Assert.That(Directory.GetFiles(directory, "battle-*.json"), Is.Empty);
        Assert.That(Directory.GetFiles(directory, "battle-*.json.invalid-*"), Has.Length.EqualTo(1));
    }

    [Test]
    public void InvalidNewResultIsRejectedBeforeWritingOrPromoting() {
        using var session = new PlayerSession();
        using var profiles = Open(session);
        var request = Battle();
        request.trialTargetGrade = 5;
        Assert.Throws<ArgumentException>(() => profiles.Record(request));
        Assert.That(Directory.GetFiles(directory, "battle-*"), Is.Empty);
        Assert.That(profiles.CachedRanking, Is.Empty);
    }

    [TestCase("createdAt")]
    [TestCase("acquiredAt")]
    [TestCase("lastTrialAt")]
    [TestCase("slot")]
    [TestCase("cpuLevel")]
    public void InvalidCacheFieldsAreQuarantined(string defect) {
        var cache = Cache();
        if (defect == "createdAt") cache.profiles[0].createdAt = "bad";
        if (defect == "acquiredAt") cache.profiles[0].acquiredAt = "2026-11-01";
        if (defect == "lastTrialAt") cache.profiles[0].lastTrialAt = "bad";
        if (defect == "slot") cache.slotNames[0] = "名前\n改行";
        if (defect == "cpuLevel") cache.cpuLevel = 5;
        File.WriteAllText(ProfilesPath, JsonUtility.ToJson(cache));
        using var session = new PlayerSession();
        using var profiles = Open(session);
        Assert.That(profiles.CachedRanking, Is.Empty);
        Assert.That(session.GetName(0), Is.Empty);
        Assert.That(Directory.GetFiles(directory, "profiles.json.invalid-*"), Has.Length.EqualTo(1));
    }

    [TestCase("{")]
    [TestCase("{}")]
    public void ValidPreviousRestoresCorruptPrimaryWithoutLosingTheGrade(string corruptPrimary) {
        File.WriteAllText(ProfilesPath, corruptPrimary);
        File.WriteAllText(ProfilesPath + ".previous", JsonUtility.ToJson(Cache()));
        using var session = new PlayerSession();
        using var profiles = Open(session);
        Assert.That(session.GetName(0), Is.EqualTo("プレイヤー"));
        Assert.That(profiles.Get("プレイヤー").grade, Is.EqualTo(3));
        Assert.That(File.Exists(ProfilesPath + ".previous"), Is.False);
        Assert.That(Directory.GetFiles(directory, "profiles.json.invalid-*"), Has.Length.EqualTo(1));
    }

    [Test]
    public void InvalidPrimaryPendingAndPreviousAreAllRetainedForRecovery() {
        File.WriteAllText(ProfilesPath, "{");
        File.WriteAllText(ProfilesPath + ".pending", "{");
        File.WriteAllText(ProfilesPath + ".previous", "{");
        using var session = new PlayerSession();
        using var profiles = Open(session);
        Assert.That(profiles.CachedRanking, Is.Empty);
        Assert.That(Directory.GetFiles(directory, "*.invalid-*"), Has.Length.EqualTo(3));
    }

    [Test]
    public void CompletedFirstOutboxPendingIsRecoveredAndUsesConfigurableOpponent() {
        var request = Battle();
        request.participants[1].cpuLevel = 4;
        request.strikerIds[1] = request.replayPayload.strikerIds[1] = 4;
        File.WriteAllText(BattlePath(request) + ".pending", JsonUtility.ToJson(request));
        using var session = new PlayerSession();
        using var profiles = Open(session);
        Assert.That(profiles.Get("プレイヤー").grade, Is.EqualTo(1));
        Assert.That(File.Exists(BattlePath(request)), Is.True);
        Assert.That(Directory.GetFiles(directory, "*.invalid-*"), Is.Empty);
    }

    string ProfilesPath => Path.Combine(directory, "profiles.json");
    string BattlePath(BattleHistorySaveRequest request) => Path.Combine(directory, $"battle-{request.battleId}.json");
    PlayerProfileService Open(PlayerSession session) => new(api, api, session, new PlayerRecordStorage(directory));
    static PlayerProfileCache Cache() => new() {
        slotNames = new[] { "プレイヤー", "", "", "" },
        profiles = new[] { new PlayerProfile { name = "プレイヤー", grade = 3, createdAt = "2026-11-01T00:00:00Z", acquiredAt = "2026-11-03T00:00:00Z" } },
    };
    static BattleHistorySaveRequest Battle() => new() {
        battleId = Guid.NewGuid().ToString("N"), mode = "Trial", trialStartGrade = 0, trialTargetGrade = 1,
        participants = new[] { new PlayerIdentity { name = "プレイヤー" }, new PlayerIdentity { name = "CPU", isCpu = true, cpuLevel = 1 } },
        playerNames = new[] { "プレイヤー", "CPU" }, strikerNames = new[] { "勇者", "魔法使い" },
        strikerIds = new[] { 0, 1 }, roundWinCounts = new[] { 2, 0 }, winnerPlayerId = 0,
        playedAt = "2026-11-03T00:00:00Z", stage = "Street", musicId = "music-test", appVersion = "1.0",
        replayPayload = new ReplayPayload { stage = "Street", musicId = "music-test", appVersion = "1.0", strikerIds = new[] { 0, 1 } },
    };
    sealed class OfflineApi : IPlayerRecordsApiClient, IBattleHistoryApiClient {
        public Task<PlayerProfile> GetProfileAsync(PlayerProfile profile) => Task.FromException<PlayerProfile>(new ApiRequestException(0, "Offline"));
        public Task<PlayerProfilePage> GetRankingAsync(int grade, string cursor) => throw new NotSupportedException();
        public Task<BattleHistoryListResponse> GetHistoryAsync(string name, string cursor) => throw new NotSupportedException();
        public Task<BattleHistoryCreateResponse> SaveAsync(BattleHistorySaveRequest request) => Task.FromException<BattleHistoryCreateResponse>(new ApiRequestException(0, "Offline"));
        public Task<BattleHistorySummary[]> GetSummariesAsync(int limit) => throw new NotSupportedException();
        public Task<BattleHistoryDetail> GetDetailAsync(string id) => throw new NotSupportedException();
    }
}
