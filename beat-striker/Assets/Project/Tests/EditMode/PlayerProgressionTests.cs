using System;
using System.IO;
using System.Threading.Tasks;
using Alice;
using NUnit.Framework;
using R3;
using UnityEngine;

public class PlayerProgressionTests {
    string directory;
    PlayerSession session;
    PlayerProfileService profiles;
    OfflineRecords api;

    [SetUp]
    public void SetUp() {
        directory = Path.Combine(Path.GetTempPath(), "beat-striker-records-" + Guid.NewGuid().ToString("N"));
        session = new PlayerSession(); api = new OfflineRecords();
        profiles = new PlayerProfileService(api, api, session, new PlayerRecordStorage(directory));
    }
    [TearDown]
    public void TearDown() {
        profiles.Dispose(); session.Dispose();
        Assert.That(Path.GetDirectoryName(Path.GetFullPath(directory)), Is.EqualTo(Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar)));
        if (Directory.Exists(directory)) Directory.Delete(directory, true);
    }
    [TestCase("あいうえおかきくけこ", true)]
    [TestCase("あいうえおかきくけこさ", false)]
    [TestCase("   ", false)]
    [TestCase("名前\n改行", false)]
    public void NameLengthAndWhitespaceAreValidated(string input, bool valid) {
        Assert.That(PlayerProfileService.TryNormalizeName(input, out _), Is.EqualTo(valid));
    }
    [Test]
    public void SameNameSharesProgressAndRestartRestoresSlotsAndCpuLevel() {
        profiles.RememberName(0, "  プレイヤー  "); profiles.RememberName(1, "プレイヤー");
        profiles.CpuLevel = 3;
        profiles.Record(Result(0, 0));
        Assert.That(profiles.Get(session.GetName(0)), Is.EqualTo(profiles.Get(session.GetName(1))));
        profiles.Dispose(); session.Dispose(); session = new PlayerSession();
        profiles = new PlayerProfileService(api, api, session, new PlayerRecordStorage(directory));
        Assert.That(session.GetName(0), Is.EqualTo("プレイヤー"));
        Assert.That(session.GetName(1), Is.EqualTo("プレイヤー"));
        Assert.That(profiles.Get("プレイヤー").grade, Is.EqualTo(1));
        Assert.That(profiles.CpuLevel, Is.EqualTo(3));
        Assert.That(Directory.GetFiles(directory, "battle-*.json"), Has.Length.EqualTo(1));
    }
    [Test]
    public void DuplicateOutOfOrderAndLostBattlesDoNotDoublePromoteOrDemote() {
        profiles.RememberName(0, "プレイヤー");
        var result = Result(2, 0); profiles.Record(result); profiles.Record(result);
        profiles.Record(Result(0, 0)); profiles.Record(Result(3, 1));
        Assert.That(profiles.Get("プレイヤー").grade, Is.EqualTo(3));
        Assert.That(Directory.GetFiles(directory, "battle-*.json"), Has.Length.EqualTo(3));
    }
    [Test]
    public void HighestGradeRematchesPreserveFirstAcquisitionTime() {
        profiles.RememberName(0, "プレイヤー"); profiles.Record(Result(4, 0));
        var acquired = profiles.Get("プレイヤー").acquiredAt;
        profiles.Record(Result(5, 0));
        Assert.That(profiles.Get("プレイヤー").grade, Is.EqualTo(5));
        Assert.That(profiles.Get("プレイヤー").acquiredAt, Is.EqualTo(acquired));
    }
    [Test]
    public void CpuGradesMatchPromotionTargets() {
        for (var level = 1; level <= 4; level++) Assert.That((int)PlayerGrades.Cpu(level, Striker.Hero), Is.EqualTo(level));
        Assert.That(PlayerGrades.Cpu(4, Striker.DemonKing), Is.EqualTo(PlayerGrade.Fortissimo));
    }
    [Test]
    public void TrialCpuDoesNotOccupyARealControllerSlotAndRestoresHumanInput() {
        var registry = new GamePadRegistry(session);
        var first = new TestPad(); var second = new TestPad(); var cpu = new TestPad();
        registry.RequestRegister(first);
        registry.RequestRegisterBattleOverride(1, cpu);
        var joined = registry.RequestRegister(second);
        Assert.That(joined.PlayerId, Is.EqualTo(1));
        Assert.That(joined.HasGamePad.CurrentValue, Is.True);
        var received = 0;
        using var subscription = joined.OnButtonDown.Subscribe(_ => received++);
        second.Send(); Assert.That(received, Is.Zero);
        cpu.Send(); Assert.That(received, Is.EqualTo(1));
        registry.ClearBattleOverride(1);
        second.Send(); Assert.That(received, Is.EqualTo(2));
        Assert.That(joined.HasGamePad.CurrentValue, Is.True);
    }
    [Test]
    public void InterruptedProfileReplacementRecoversCompletedPendingFile() {
        profiles.RememberName(0, "プレイヤー"); profiles.Dispose();
        var path = Path.Combine(directory, "profiles.json");
        File.Copy(path, path + ".pending"); File.Move(path, path + ".previous");
        profiles = new PlayerProfileService(api, api, session, new PlayerRecordStorage(directory));
        Assert.That(session.GetName(0), Is.EqualTo("プレイヤー"));
        Assert.That(File.Exists(path), Is.True);
        Assert.That(File.Exists(path + ".previous"), Is.False);
    }
    [Test]
    public void CompletedNewOutboxPendingRecoversItsWinAfterRestart() {
        profiles.RememberName(0, "プレイヤー"); profiles.Dispose();
        var request = Result(0, 0);
        var path = Path.Combine(directory, $"battle-{request.battleId}.json");
        File.WriteAllText(path + ".pending", JsonUtility.ToJson(request));
        profiles = new PlayerProfileService(api, api, session, new PlayerRecordStorage(directory));
        Assert.That(File.Exists(path), Is.True);
        Assert.That(profiles.Get("プレイヤー").grade, Is.EqualTo(1));
    }
    [Test]
    public void FailedSettingsWriteDoesNotPublishUnsavedNameOrCpuLevel() {
        profiles.RememberName(0, "プレイヤー");
        var pending = Path.Combine(directory, "profiles.json.pending");
        Directory.CreateDirectory(pending);
        Assert.Catch(() => profiles.CpuLevel = 4);
        Assert.Catch(() => profiles.RememberName(0, "別プレイヤー"));
        Assert.That(profiles.CpuLevel, Is.EqualTo(1));
        Assert.That(session.GetName(0), Is.EqualTo("プレイヤー"));
        Directory.Delete(pending);
    }
    [Test]
    public void ReturnedProfilesCannotMutateStoredProgress() {
        profiles.RememberName(0, "プレイヤー");
        var snapshot = profiles.Get("プレイヤー"); snapshot.grade = 5;
        Assert.That(profiles.Get("プレイヤー").grade, Is.Zero);
    }
    [Test]
    public async Task RejectedBattleIsRetainedWhileLaterBattleSynchronizes() {
        profiles.RememberName(0, "プレイヤー");
        var rejected = Result(0, 0); var accepted = Result(1, 0);
        profiles.Record(rejected); profiles.Record(accepted); profiles.Dispose();
        api.Lookup = profile => Task.FromResult(profile);
        api.Upload = request => request.battleId == rejected.battleId
            ? Task.FromException<BattleHistoryCreateResponse>(new ApiRequestException(400, "Rejected fixture"))
            : Task.FromResult(new BattleHistoryCreateResponse { ok = true, id = request.battleId });
        profiles = new PlayerProfileService(api, api, session, new PlayerRecordStorage(directory));
        await profiles.SynchronizeAsync();
        Assert.That(File.Exists(Path.Combine(directory, $"battle-{rejected.battleId}.json")), Is.True);
        Assert.That(File.Exists(Path.Combine(directory, $"battle-{accepted.battleId}.json")), Is.False);
        Assert.That(profiles.Get("プレイヤー").grade, Is.EqualTo(2));
    }
    [Test]
    public async Task RemotePromotionAndEarlierCreationSurviveLaterNetworkFailure() {
        profiles.RememberName(0, "プレイヤー"); profiles.RememberName(1, "別プレイヤー"); profiles.Dispose();
        var earlier = DateTime.UtcNow.AddDays(-1).ToString("O");
        api.Lookup = profile => profile.name == "プレイヤー"
            ? Task.FromResult(profile with { grade = 3, acquiredAt = earlier, createdAt = earlier })
            : Task.FromException<PlayerProfile>(new ApiRequestException(503, "Offline fixture"));
        profiles = new PlayerProfileService(api, api, session, new PlayerRecordStorage(directory));
        await profiles.SynchronizeAsync(); profiles.Dispose();
        profiles = new PlayerProfileService(api, api, session, new PlayerRecordStorage(directory));
        Assert.That(profiles.Get("プレイヤー").grade, Is.EqualTo(3));
        Assert.That(profiles.Get("プレイヤー").createdAt, Is.EqualTo(earlier));
    }
    [Test]
    public void ChangingModeClearsPreviousBattleIdentitySnapshot() {
        session.SetMode(BattlePlayMode.Trial);
        session.Prepare(Result(0, 0).participants, trialStartGrade: 0);
        session.SetMode(BattlePlayMode.Tutorial);
        Assert.That(session.Participants, Is.Empty);
        Assert.That(session.BattleId, Is.Empty);
        Assert.That(session.TrialPlayerName, Is.Empty);
    }
    static BattleHistorySaveRequest Result(int start, int winner) => new() {
        battleId = Guid.NewGuid().ToString("N"), mode = "Trial", trialStartGrade = start,
        trialTargetGrade = start < 5 ? start + 1 : -1, winnerPlayerId = winner,
        playedAt = DateTime.UtcNow.ToString("O"), strikerIds = new[] { 0, start == 4 ? 4 : 1 },
        participants = new[] { new PlayerIdentity { name = "プレイヤー", grade = start }, new PlayerIdentity { name = "CPU", isCpu = true, cpuLevel = 4, grade = start == 4 ? 5 : 4 } },
        playerNames = new[] { "プレイヤー", "CPU" }, strikerNames = new[] { "Hero", "CPU" },
        stage = "Live", stageName = "Live", musicId = "fixture", musicName = "Fixture", appVersion = "test", roundWinCounts = new[] { 1, 0 },
        replayPayload = new ReplayPayload { stage = "Live", musicId = "fixture", appVersion = "test", strikerIds = new[] { 0, start == 4 ? 4 : 1 }, rounds = new[] { new ReplayRoundPayload { roundNumber = 1 } } },
    };
    class OfflineRecords : IPlayerRecordsApiClient, IBattleHistoryApiClient {
        readonly TaskCompletionSource<PlayerProfile> pendingProfile = new();
        public Func<PlayerProfile, Task<PlayerProfile>> Lookup;
        public Func<BattleHistorySaveRequest, Task<BattleHistoryCreateResponse>> Upload;
        public Task<PlayerProfile> GetProfileAsync(PlayerProfile profile) => Lookup == null ? pendingProfile.Task : Lookup(profile);
        public Task<PlayerProfilePage> GetRankingAsync(int grade, string cursor) => throw new NotSupportedException();
        public Task<BattleHistoryListResponse> GetHistoryAsync(string name, string cursor) => throw new NotSupportedException();
        public Task<BattleHistoryCreateResponse> SaveAsync(BattleHistorySaveRequest request) => Upload == null ? throw new NotSupportedException() : Upload(request);
        public Task<BattleHistorySummary[]> GetSummariesAsync(int limit) => throw new NotSupportedException();
        public Task<BattleHistoryDetail> GetDetailAsync(string id) => throw new NotSupportedException();
    }
    class TestPad : IGamePad {
        readonly Subject<GamePadButton> down = new();
        public Observable<Vector2> OnDirectionAsObservable => Observable.Empty<Vector2>();
        public Observable<Unit> OnDirectionCanceledAsObservable => Observable.Empty<Unit>();
        public Observable<GamePadButton> OnButtonDownAsObservable => down;
        public Observable<GamePadButton> OnButtonUpAsObservable => Observable.Empty<GamePadButton>();
        public string DeviceName => "Test controller";
        public void Send() => down.OnNext(GamePadButton.East);
        public void DestroyGamePad() { }
    }
}
