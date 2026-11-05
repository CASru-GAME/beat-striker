using UnityEngine;

namespace Alice {
    public class TrialSetting : MonoBehaviour {
        [SerializeField] Striker[] opponents = { Striker.Hero, Striker.Wizard, Striker.Warrior };
        [SerializeField] Striker boss = Striker.DemonKing;
        [SerializeField] Stage[] promotionStages = { Stage.Live, Stage.Live, Stage.Street, Stage.Street, Stage.Street };
        [SerializeField] int[] promotionLevels = { 1, 2, 3, 4, 4 };
        public Striker[] Opponents => opponents;
        public Striker Boss => boss;
        public Stage GetStage(int grade) => promotionStages[grade];
        public int GetLevel(int grade) => promotionLevels[grade];
    }
}
