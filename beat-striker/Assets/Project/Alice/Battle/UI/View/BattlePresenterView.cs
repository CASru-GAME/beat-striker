using UnityEngine;

namespace Alice {
    public class BattlePresenterView : MonoBehaviour {
        [SerializeField] StageCamera stageCamera;
        [SerializeField] BattleRoundStartView roundStartPresenter;
        [SerializeField] BattleResultTextView resultTextPresenter;
        [SerializeField] BattleFadeView fadePresenter;
        [SerializeField] BattleSuspendMenuView suspendMenuPresenter;
        [SerializeField] AudioClip beatSound;

        public StageCamera StageCamera => stageCamera;
        public BattleRoundStartView RoundStartPresenter => roundStartPresenter;
        public BattleResultTextView ResultTextPresenter => resultTextPresenter;
        public BattleFadeView FadePresenter => fadePresenter;
        public BattleSuspendMenuView SuspendMenuPresenter => suspendMenuPresenter;
        public AudioClip BeatSound => beatSound;
    }
}
