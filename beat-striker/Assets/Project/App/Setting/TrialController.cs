using System;
using System.Linq;
using UnityEngine;

namespace Alice {
    public class TrialController {
        readonly PlayerSession session;
        readonly IPlayerProfileService profiles;
        readonly TrialSetting setting;
        readonly IPlayerSelectSetting players;
        readonly IBattleSelectSetting battle;
        readonly IMusicRegistry music;
        readonly IStageRegistry stages;
        readonly IAppStrikerRegistry strikers;
        public TrialController(PlayerSession session, IPlayerProfileService profiles, TrialSetting setting, IPlayerSelectSetting players, IBattleSelectSetting battle, IMusicRegistry music, IStageRegistry stages, IAppStrikerRegistry strikers) {
            this.session = session; this.profiles = profiles; this.setting = setting; this.players = players; this.battle = battle; this.music = music; this.stages = stages; this.strikers = strikers;
        }
        public void PrepareNext() {
            var profile = profiles.Get(session.GetName(0));
            var grade = Math.Clamp(profile.grade, 0, 5);
            var pool = grade == 4 ? new[] { setting.Boss } : grade == 5 ? setting.Opponents.Append(setting.Boss).ToArray() : setting.Opponents;
            var candidates = pool.Where(s => (int)s != profile.lastTrialOpponent).ToArray();
            if (candidates.Length == 0) candidates = pool;
            var opponent = candidates[UnityEngine.Random.Range(0, candidates.Length)];
            players.SelectStriker(1, opponent);
            var level = grade < 5 ? setting.GetLevel(grade) : 4;
            battle.SetAiStrength(level);
            battle.SelectStage(grade < 5 ? setting.GetStage(grade) : stages.GetAll()[UnityEngine.Random.Range(0, stages.GetAll().Count)].Stage);
            var songs = music.GetAll();
            battle.SelectMusic(songs[UnityEngine.Random.Range(0, songs.Count)].Id);
            session.Prepare(new[] { profile.Identity, new PlayerIdentity { name = strikers.GetByStriker(opponent).DisplayName, grade = (int)PlayerGrades.Cpu(level, opponent), isCpu = true, cpuLevel = level } }, trialStartGrade: grade);
        }
    }
}
