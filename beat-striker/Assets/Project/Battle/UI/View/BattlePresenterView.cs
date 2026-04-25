using System.Threading.Tasks;
using System.Linq;
using UnityEngine;

namespace Alice {
    public class BattlePresenterView : MonoBehaviour {
        [SerializeField] StageCamera stageCamera;
        [SerializeField] BeatExpandView beatExpandView;
        [SerializeField] BattleRoundStartView roundStartPresenter;
        [SerializeField] BattleResultTextView resultTextPresenter;
        [SerializeField] AttentionTextView attentionTextView;
        [SerializeField] BattleFadeView fadePresenter;
        [SerializeField] BattleSuspendMenuView suspendMenuPresenter;
        [SerializeField] AudioClip beatSound;
        [SerializeField] RectTransform[] battleUiSlideTargets;
        [SerializeField] float battleUiSlideDuration = 0.2f;
        [SerializeField] float battleUiHiddenTopMargin = 80f;
        [SerializeField] float openingAfterSlideDelaySeconds = 0.3f;

        Vector2[] battleUiDefaultAnchoredPositions;
        float[] battleUiHiddenTopAnchoredYs;
        TaskCompletionSource<bool> uiSlideCompletionSource;

        public StageCamera StageCamera => stageCamera;
        public BeatExpandView BeatExpandView => beatExpandView;
        public BattleRoundStartView RoundStartPresenter => roundStartPresenter;
        public BattleResultTextView ResultTextPresenter => resultTextPresenter;
        public AttentionTextView AttentionTextView => attentionTextView;
        public BattleFadeView FadePresenter => fadePresenter;
        public BattleSuspendMenuView SuspendMenuPresenter => suspendMenuPresenter;
        public AudioClip BeatSound => beatSound;

        void Awake() {
            CacheBattleUiLayout();
        }

        public void SetBattleUiHiddenAboveImmediately() {
            CacheBattleUiLayout();
            LeanTween.cancel(gameObject);

            for (var i = 0; i < battleUiSlideTargets.Length; i++) {
                battleUiSlideTargets[i].anchoredPosition = new Vector2(battleUiDefaultAnchoredPositions[i].x, battleUiHiddenTopAnchoredYs[i]);
            }
        }

        public Task SlideBattleUiInAsync() {
            return SlideBattleUiAsync(useHiddenTarget: false);
        }

        public Task SlideBattleUiOutAsync() {
            return SlideBattleUiAsync(useHiddenTarget: true);
        }

        public Task WaitAfterSlideBattleUiInAsync() {
            return DelayAsync(openingAfterSlideDelaySeconds);
        }

        Task SlideBattleUiAsync(bool useHiddenTarget) {
            CacheBattleUiLayout();
            uiSlideCompletionSource?.TrySetCanceled();
            uiSlideCompletionSource = new TaskCompletionSource<bool>();
            LeanTween.cancel(gameObject);

            if (battleUiSlideTargets.Length == 0) {
                uiSlideCompletionSource.TrySetResult(true);
                return uiSlideCompletionSource.Task;
            }

            var completedCount = 0;
            for (var i = 0; i < battleUiSlideTargets.Length; i++) {
                var index = i;
                var targetRect = battleUiSlideTargets[index];
                var startAnchoredY = targetRect.anchoredPosition.y;
                var targetAnchoredY = useHiddenTarget
                    ? battleUiHiddenTopAnchoredYs[index]
                    : battleUiDefaultAnchoredPositions[index].y;

                LeanTween.value(gameObject, startAnchoredY, targetAnchoredY, battleUiSlideDuration)
                    .setEase(LeanTweenType.easeInOutQuad)
                    .setOnUpdate((float y) => {
                        targetRect.anchoredPosition = new Vector2(battleUiDefaultAnchoredPositions[index].x, y);
                    })
                    .setOnComplete(() => {
                        completedCount += 1;
                        if (completedCount == battleUiSlideTargets.Length) {
                            uiSlideCompletionSource?.TrySetResult(true);
                        }
                    });
            }

            return uiSlideCompletionSource.Task;
        }

        void CacheBattleUiLayout() {
            if (battleUiDefaultAnchoredPositions != null) {
                return;
            }

            battleUiSlideTargets = battleUiSlideTargets.Where(x => x != null).ToArray();

            battleUiDefaultAnchoredPositions = new Vector2[battleUiSlideTargets.Length];
            battleUiHiddenTopAnchoredYs = new float[battleUiSlideTargets.Length];

            for (var i = 0; i < battleUiSlideTargets.Length; i++) {
                var target = battleUiSlideTargets[i];
                var rootCanvas = target.GetComponentInParent<Canvas>().rootCanvas;
                var rootCanvasRect = (RectTransform)rootCanvas.transform;
                var rootHeight = rootCanvasRect.rect.height;
                var defaultPosition = target.anchoredPosition;
                battleUiDefaultAnchoredPositions[i] = defaultPosition;
                battleUiHiddenTopAnchoredYs[i] = defaultPosition.y + rootHeight + target.rect.height + battleUiHiddenTopMargin;
            }
        }

        static Task DelayAsync(float seconds) {
            if (seconds <= 0f) {
                return Task.CompletedTask;
            }

            var completionSource = new TaskCompletionSource<bool>();
            LeanTween.delayedCall(seconds, () => completionSource.TrySetResult(true));
            return completionSource.Task;
        }
    }
}
