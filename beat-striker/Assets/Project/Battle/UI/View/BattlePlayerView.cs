using UnityEngine;
using UnityEngine.UI;
using System.Threading.Tasks;

namespace Alice {
    public class BattlePlayerView : MonoBehaviour {
        [SerializeField] int playerId;
        [SerializeField] AliceHpBarView hpBarUI;
        [SerializeField] AliceSpecialBarView specialBarUI;
        [SerializeField] AliceComboView comboView;
        [SerializeField] Image strikerPortraitImage;
        [SerializeField] AliceRingView beatRingPrefab;
        [SerializeField] Transform beatRingParent;
        [SerializeField] float openingHpFillDuration = 0.5f;

        public int PlayerId => playerId;
        public AliceHpBarView HpBarUI => hpBarUI;
        public AliceSpecialBarView SpecialBarUI => specialBarUI;
        public AliceComboView ComboView => comboView;
        public Image StrikerPortraitImage => strikerPortraitImage;
        public AliceRingView BeatRingPrefab => beatRingPrefab;
        public Transform BeatRingParent => beatRingParent;

        public Task PlayOpeningHpFillAsync() {
            return hpBarUI.PlayOpeningFillAsync(openingHpFillDuration);
        }

        public void SetStrikerPortrait(Sprite portrait) {
            strikerPortraitImage.sprite = portrait;
        }
    }
}
