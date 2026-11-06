using System;
using System.Linq;

namespace Alice {
    public class TrialController {
        readonly PlayerSession session;
        readonly IPlayerProfileService profiles;
        readonly ITrialProgressService progress;
        readonly TrialSetting setting;
        readonly IPlayerSelectSetting players;
        readonly IBattleSelectSetting battle;
        readonly IMusicRegistry music;
        readonly IStageRegistry stages;
        readonly IAppStrikerRegistry strikers;
        public TrialController(PlayerSession session, IPlayerProfileService profiles, TrialSetting setting, IPlayerSelectSetting players, IBattleSelectSetting battle, IMusicRegistry music, IStageRegistry stages, IAppStrikerRegistry strikers, ITrialProgressService progress) {
            this.progress = progress;
            this.session = session; this.profiles = profiles; this.setting = setting; this.players = players; this.battle = battle; this.music = music; this.stages = stages; this.strikers = strikers;
        }
        public void PrepareNext() {
            var profile = profiles.Get(session.GetName(0));
            var grade = Math.Clamp(profile.grade, 0, 5);
            var isEarlyTrial = grade < (int)PlayerGrade.MezzoPiano;
            var songs = music.GetAll().Where(song => song.LengthSeconds > 0 && song.LengthSeconds < 240
                && (!isEarlyTrial || song.LengthSeconds <= 180 && song.Bpm > 0 && song.Bpm <= 120)).ToArray();
            if (songs.Length == 0) throw new InvalidOperationException("チャレンジの選曲条件を満たす曲がありません。");

            var pool = grade == 4 ? new[] { setting.Boss } : grade == 5 ? setting.Opponents.Append(setting.Boss).ToArray() : setting.Opponents;
            var candidates = pool.Where(s => (int)s != profile.lastTrialOpponent).ToArray();
            if (candidates.Length == 0) candidates = pool;
            var opponent = candidates[UnityEngine.Random.Range(0, candidates.Length)];
            players.SelectStriker(1, opponent);
            var level = grade < 5 ? setting.GetLevel(grade) : 4;
            battle.SetAiStrength(level);
            battle.SelectStage(grade < 5 ? setting.GetStage(grade) : stages.GetAll()[UnityEngine.Random.Range(0, stages.GetAll().Count)].Stage);
            battle.SelectMusic(songs[UnityEngine.Random.Range(0, songs.Length)].Id);
            session.Prepare(new[] { profile.Identity, new PlayerIdentity { name = strikers.GetByStriker(opponent).DisplayName, grade = (int)PlayerGrades.Cpu(level, opponent), isCpu = true, cpuLevel = level } }, trialStartGrade: grade, trialStartStreak: progress.GetTrialProgress(profile.name).championStreak);
        }
    }
}
