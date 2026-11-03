using UnityEngine;

namespace Alice {
    public class SelectScene : MonoBehaviour {
        [SerializeField] Characterselectbutton[] characterSelectButtons;
        [SerializeField] StartButtonAnimation startButtonAnimation;
        [SerializeField] CharacterSelectStatusView statusView;
        [SerializeField] Backbutton backbutton;
        [SerializeField] AudioClip clickSound;
        [SerializeField] CharacterIntroSlideshow characterIntroSlideshow;

        public Characterselectbutton[] CharacterSelectButtons => characterSelectButtons;
        public StartButtonAnimation StartButtonAnimation => startButtonAnimation;
        public CharacterSelectStatusView StatusView => statusView;
        public Backbutton Backbutton => backbutton;
        public AudioClip ClickSound => clickSound;
        public CharacterIntroSlideshow CharacterIntroSlideshow => characterIntroSlideshow;
    }
}
