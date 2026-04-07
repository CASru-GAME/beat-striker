using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace Alice {
    public class BattleTransitionView : AppTransitionPresenter {
        [Header("Portraits")]
        [SerializeField] Image leftPortraitImage;
        [SerializeField] Image rightPortraitImage;
        [SerializeField] RectTransform leftPortraitTransform;
        [SerializeField] RectTransform rightPortraitTransform;

        [Header("Anchors")]
        [SerializeField] RectTransform leftStartAnchor;
        [SerializeField] RectTransform rightStartAnchor;

        [Header("Root")]
        [SerializeField] RectTransform transitionRoot;

        [Header("VS")]
        [SerializeField] CanvasGroup vsCanvasGroup;
        [SerializeField] RectTransform vsTransform;

        [Header("Audio")]
        [SerializeField] AudioSource audioSource;
        [SerializeField] AudioClip transitionStartClip;
        [SerializeField] AudioClip mergeClip;
        [SerializeField] AudioClip transitionEndClip;

        [Header("Timings")]
        [SerializeField] float convergeDuration = 0.35f;
        [SerializeField] float vsAppearDuration = 0.16f;
        [SerializeField] float vsDisappearExpandDuration = 0.06f;
        [SerializeField] float vsDisappearShrinkDuration = 0.1f;
        [SerializeField] float holdDuration = 0.35f;
        [SerializeField] float separateDuration = 0.3f;

        [Header("Tuning")]
        [SerializeField] float vsAppearStartScale = 0.15f;
        [SerializeField] float vsDisappearOvershootScale = 1.08f;
        [SerializeField] float mergeShakeDuration = 0.08f;
        [SerializeField] float mergeShakeAmplitude = 8f;
        [SerializeField] float mergeShakeFrequency = 24f;

        BattleTransitionPresenter presenter;
        Vector2 leftMergePosition;
        Vector2 rightMergePosition;
        Vector2 transitionRootBasePosition;

        void Awake() {
            leftMergePosition = leftPortraitTransform.anchoredPosition;
            rightMergePosition = rightPortraitTransform.anchoredPosition;
            transitionRootBasePosition = transitionRoot.anchoredPosition;
            ResetVisualState();
        }

        public void Bind(BattleTransitionPresenter presenter) {
            this.presenter = presenter;
        }

        public void SetPortraits(Sprite left, Sprite right) {
            leftPortraitImage.sprite = left;
            rightPortraitImage.sprite = right;
        }

        public async Task PlayTransitionOutAsync() {
            CancelAllTweens();
            ResetVisualState();
            PlayClip(transitionStartClip);

            var leftStart = ResolveLocalPosition(leftStartAnchor);
            var rightStart = ResolveLocalPosition(rightStartAnchor);

            leftPortraitTransform.anchoredPosition = leftStart;
            rightPortraitTransform.anchoredPosition = rightStart;

            await Task.WhenAll(
                AnimateAnchoredPosition(leftPortraitTransform, leftStart, leftMergePosition, convergeDuration, LeanTweenType.easeOutCubic),
                AnimateAnchoredPosition(rightPortraitTransform, rightStart, rightMergePosition, convergeDuration, LeanTweenType.easeOutCubic));

            PlayClip(mergeClip);

            await ShakeTransitionRootAsync();

            await ShowVsAsync();

            if (holdDuration > 0f) {
                await DelayAsync(holdDuration);
            }
        }

        public async Task PlayTransitionInAsync() {
            CancelAllTweens();
            var leftStart = leftMergePosition;
            var rightStart = rightMergePosition;
            var leftGoal = ResolveLocalPosition(leftStartAnchor);
            var rightGoal = ResolveLocalPosition(rightStartAnchor);

            leftPortraitTransform.anchoredPosition = leftStart;
            rightPortraitTransform.anchoredPosition = rightStart;
            transitionRoot.anchoredPosition = transitionRootBasePosition;

            await Task.WhenAll(
                HideVsAsync(),
                AnimateAnchoredPosition(leftPortraitTransform, leftStart, leftGoal, separateDuration, LeanTweenType.easeInCubic),
                AnimateAnchoredPosition(rightPortraitTransform, rightStart, rightGoal, separateDuration, LeanTweenType.easeInCubic));

            PlayClip(transitionEndClip);
        }

        protected override Task PresentTransitionOut(TransitionContext context) {
            return presenter.PresentTransitionOutAsync(context);
        }

        protected override Task PresentTransitionIn(TransitionContext context) {
            return presenter.PresentTransitionInAsync(context);
        }

        void ResetVisualState() {
            leftPortraitTransform.anchoredPosition = ResolveLocalPosition(leftStartAnchor);
            rightPortraitTransform.anchoredPosition = ResolveLocalPosition(rightStartAnchor);
            leftPortraitTransform.localScale = Vector3.one;
            rightPortraitTransform.localScale = Vector3.one;
            vsCanvasGroup.alpha = 1f;
            vsTransform.localScale = Vector3.one * vsAppearStartScale;
            transitionRoot.anchoredPosition = transitionRootBasePosition;
        }

        void CancelAllTweens() {
            LeanTween.cancel(leftPortraitTransform.gameObject);
            LeanTween.cancel(rightPortraitTransform.gameObject);
            LeanTween.cancel(vsCanvasGroup.gameObject);
            LeanTween.cancel(vsTransform.gameObject);
            LeanTween.cancel(transitionRoot.gameObject);
        }

        Vector2 ResolveLocalPosition(RectTransform marker) {
            var parent = (RectTransform)leftPortraitTransform.parent;
            return parent.InverseTransformPoint(marker.position);
        }

        void PlayClip(AudioClip clip) {
            audioSource.clip = clip;
            audioSource.Play();
        }

        async Task ShowVsAsync() {
            Debug.Log($"[ShowVsAsync Start] vsTransform.localScale before reset: {vsTransform.localScale}, vsAppearStartScale: {vsAppearStartScale}");
            vsTransform.localScale = Vector3.one * vsAppearStartScale;
            Debug.Log($"[ShowVsAsync] vsTransform.localScale after reset: {vsTransform.localScale}");

            await AnimateScale(vsTransform, Vector3.one * vsAppearStartScale, Vector3.one, vsAppearDuration, LeanTweenType.easeOutBack);
        }

        async Task HideVsAsync() {
            var fromScale = vsTransform.localScale;
            var overshootScale = Vector3.one * vsDisappearOvershootScale;
            await AnimateScale(vsTransform, fromScale, overshootScale, vsDisappearExpandDuration, LeanTweenType.easeOutQuad);

            await AnimateScale(vsTransform, overshootScale, Vector3.zero, vsDisappearShrinkDuration, LeanTweenType.easeInCubic);
        }

        async Task ShakeTransitionRootAsync() {
            var completionSource = new TaskCompletionSource<bool>();
            LeanTween.value(transitionRoot.gameObject, 0f, 1f, mergeShakeDuration)
                .setEase(LeanTweenType.easeOutQuad)
                .setOnUpdate((float value) => {
                    var decay = 1f - value;
                    var angle = value * mergeShakeFrequency * Mathf.PI * 2f;
                    var offset = new Vector2(
                        Mathf.Sin(angle),
                        Mathf.Sin(angle * 1.37f + 0.5f)) * (mergeShakeAmplitude * decay);
                    transitionRoot.anchoredPosition = transitionRootBasePosition + offset;
                })
                .setOnComplete(() => {
                    transitionRoot.anchoredPosition = transitionRootBasePosition;
                    completionSource.TrySetResult(true);
                });

            await completionSource.Task;
        }

        static Task AnimateAnchoredPosition(RectTransform target, Vector2 from, Vector2 to, float duration, LeanTweenType ease) {
            var completionSource = new TaskCompletionSource<bool>();
            LeanTween.value(target.gameObject, from, to, duration)
                .setEase(ease)
                .setOnUpdate((Vector2 value) => target.anchoredPosition = value)
                .setOnComplete(() => completionSource.TrySetResult(true));
            return completionSource.Task;
        }

        static Task AnimateScale(RectTransform target, Vector3 from, Vector3 to, float duration, LeanTweenType ease) {
            var completionSource = new TaskCompletionSource<bool>();
            LeanTween.value(target.gameObject, from, to, duration)
                .setEase(ease)
                .setOnUpdate((Vector3 value) => target.localScale = value)
                .setOnComplete(() => completionSource.TrySetResult(true));
            return completionSource.Task;
        }

        static Task DelayAsync(float seconds) {
            var completionSource = new TaskCompletionSource<bool>();
            LeanTween.delayedCall(seconds, () => completionSource.TrySetResult(true));
            return completionSource.Task;
        }
    }
}
