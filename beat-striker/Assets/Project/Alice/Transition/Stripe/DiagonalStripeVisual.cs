using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Alice;
using System.Threading.Tasks;

public class DiagonalStripeVisual : AppTransitionPresenter {
    [Header("Transition Settings")]
    public float transitionDuration = 1f;
    public float stripeDelay = 0.05f;

    [SerializeField] private RectTransform targetRectTransform;
    [SerializeField] private List<RectTransform> stripes = new();
    private TaskCompletionSource<bool> transitionOutCompletionSource;
    private TaskCompletionSource<bool> transitionInCompletionSource;

    void Awake() {
        targetRectTransform = ResolveTargetRectTransform();

        InitializeStripes();
    }

    protected override Task PresentTransitionOut(TransitionContext context) {
        transitionOutCompletionSource?.TrySetCanceled();
        transitionOutCompletionSource = new TaskCompletionSource<bool>();

        StartCoroutine(PlayTransitionOutCoroutine(transitionOutCompletionSource));
        return transitionOutCompletionSource.Task;
    }

    protected override Task PresentTransitionIn(TransitionContext context) {
        transitionInCompletionSource?.TrySetCanceled();
        transitionInCompletionSource = new TaskCompletionSource<bool>();

        StartCoroutine(PlayTransitionInCoroutine(transitionInCompletionSource));
        return transitionInCompletionSource.Task;
    }

    IEnumerator PlayTransitionOutCoroutine(TaskCompletionSource<bool> completionSource) {
        yield return StartCoroutine(PlayTransition(true));
        completionSource.TrySetResult(true);
    }

    IEnumerator PlayTransitionInCoroutine(TaskCompletionSource<bool> completionSource) {
        yield return StartCoroutine(PlayTransition(false));
        completionSource.TrySetResult(true);
    }

    RectTransform ResolveTargetRectTransform() {
        if (targetRectTransform != null) {
            return targetRectTransform;
        }

        var selfRectTransform = GetComponent<RectTransform>();
        if (selfRectTransform != null) {
            return selfRectTransform;
        }

        if (stripes.Count > 0) {
            return stripes[0].parent as RectTransform;
        }

        return null;
    }

    void InitializeStripes() {

        if (targetRectTransform == null || stripes.Count == 0) {
            return;
        }

        Vector2 screenSize = targetRectTransform.rect.size;
        float diagonal = screenSize.magnitude;

        float stripeWidth = diagonal / stripes.Count;

        for (int i = 0; i < stripes.Count; i++) {
            var stripe = stripes[i];

            stripe.sizeDelta = new Vector2(0, stripeWidth);
            stripe.anchoredPosition = new Vector2(stripe.anchoredPosition.x, stripe.sizeDelta.y * (i - stripes.Count / 2));
        }

        float diagonalStripeRad = Mathf.Atan2(targetRectTransform.rect.height, targetRectTransform.rect.width);
        float diagonalAngle = diagonalStripeRad * Mathf.Rad2Deg;
        targetRectTransform.eulerAngles = new Vector3(0, 0, diagonalAngle);

        float width = targetRectTransform.rect.width;
        float height = targetRectTransform.rect.height;
        float requiredWidth = Mathf.Abs(width * Mathf.Cos(diagonalStripeRad)) + Mathf.Abs(height * Mathf.Sin(diagonalStripeRad));
        float requiredHeight = Mathf.Abs(width * Mathf.Sin(diagonalStripeRad)) + Mathf.Abs(height * Mathf.Cos(diagonalStripeRad));
        
        targetRectTransform.sizeDelta = new Vector2(requiredWidth, requiredHeight);
    }

    public IEnumerator PlayTransition(bool isFadeIn) {
        if (targetRectTransform == null || stripes.Count == 0) {
            yield break;
        }

        Vector2 screenSize = targetRectTransform.rect.size;
        float diagonal = screenSize.magnitude;

        

        for (int i = 0; i < stripes.Count; i++) {
            RectTransform stripe = stripes[i];
            stripe.gameObject.SetActive(true);

            (float start, float end) = isFadeIn ? (0f, diagonal) : (diagonal, 0f);

            LeanTween.cancel(stripe.gameObject);
            LeanTween.value(stripe.gameObject, start, end, transitionDuration)
                .setOnUpdate(val => {
                    stripe.sizeDelta = new Vector2(val, stripe.sizeDelta.y);
                })
                .setEase(LeanTweenType.easeInOutQuad);

            yield return new WaitForSeconds(stripeDelay);
        }

        yield return new WaitForSeconds(transitionDuration);
        
    }

}
