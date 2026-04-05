using UnityEngine;

namespace Alice {
    public class BattlePlayerView : MonoBehaviour {
        [SerializeField] int playerId;
        [SerializeField] AliceHpBarView hpBarUI;
        [SerializeField] AliceSpecialBarView specialBarUI;
        [SerializeField] AliceComboView comboView;
        [SerializeField] AliceRingView beatRingPrefab;
        [SerializeField] Transform beatRingParent;

        public int PlayerId => playerId;
        public AliceHpBarView HpBarUI => hpBarUI;
        public AliceSpecialBarView SpecialBarUI => specialBarUI;
        public AliceComboView ComboView => comboView;
        public AliceRingView BeatRingPrefab => beatRingPrefab;
        public Transform BeatRingParent => beatRingParent;
    }
}
