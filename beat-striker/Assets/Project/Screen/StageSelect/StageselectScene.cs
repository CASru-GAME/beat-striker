using UnityEngine;

namespace Alice {
    public class StageselectScene : MonoBehaviour {
        [SerializeField] Backbutton backButton;
        [SerializeField] Stageselectbutton[] stageSelectButtons;

        public Backbutton BackButton => backButton;
        public Stageselectbutton[] StageSelectButtons => stageSelectButtons;
    }
}
