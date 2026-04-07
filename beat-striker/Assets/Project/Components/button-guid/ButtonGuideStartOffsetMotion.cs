using UnityEngine;
using System.Collections;

[RequireComponent(typeof(RectTransform), typeof(CanvasGroup))]
public class ButtonGuideStartOffsetMotion : MonoBehaviour {
    [SerializeField] private Vector2 startOffset = new(0f, -40f);
    [SerializeField][Min(0f)] private float startDelay = 0f;
    [SerializeField][Min(0f)] private float duration = 0.25f;
    [SerializeField] private bool shouldExit = true;
    [SerializeField][Min(0f)] private float waitDurationBeforeExit = 1f;
    [SerializeField][Min(0f)] private float exitDuration = 0.2f;
    [SerializeField] private LeanTweenType easeType = LeanTweenType.easeOutCubic;

    private RectTransform rectTransform;
    private CanvasGroup canvasGroup;
    private Vector2 originalPosition;
    private float originalAlpha;
    private Coroutine sequenceCoroutine;

    void Awake() {
        rectTransform = GetComponent<RectTransform>();
        canvasGroup = GetComponent<CanvasGroup>();
        originalPosition = rectTransform.anchoredPosition;
        originalAlpha = canvasGroup.alpha;
        Reset();
    }

    void Reset() {
        rectTransform.anchoredPosition = originalPosition + startOffset;
        canvasGroup.alpha = originalAlpha;
    }


    void OnEnable() {
        sequenceCoroutine = StartCoroutine(PlaySequence());
    }

    private IEnumerator PlaySequence() {
        if (startDelay > 0f) {
            yield return new WaitForSeconds(startDelay);
        }

        yield return MoveTo(originalPosition, duration);

        if (!shouldExit) {
            yield break;
        }

        if (waitDurationBeforeExit > 0f) {
            yield return new WaitForSeconds(waitDurationBeforeExit);
        }

        var exitPosition = originalPosition + startOffset;
        yield return MoveAndFadeTo(exitPosition, 0f, exitDuration);
        this.gameObject.SetActive(false);
    }

    private IEnumerator MoveTo(Vector2 targetPosition, float motionDuration) {
        if (motionDuration <= 0f) {
            rectTransform.anchoredPosition = targetPosition;
            yield break;
        }

        var isCompleted = false;
        LeanTween.value(gameObject, rectTransform.anchoredPosition, targetPosition, motionDuration)
            .setEase(easeType)
            .setOnUpdate((Vector2 value) => { rectTransform.anchoredPosition = value; })
            .setOnComplete(() => { isCompleted = true; });

        while (!isCompleted) {
            yield return null;
        }
    }

    private IEnumerator MoveAndFadeTo(Vector2 targetPosition, float targetAlpha, float motionDuration) {
        if (motionDuration <= 0f) {
            rectTransform.anchoredPosition = targetPosition;
            canvasGroup.alpha = targetAlpha;
            yield break;
        }

        var moveCompleted = false;
        var fadeCompleted = false;

        LeanTween.value(gameObject, rectTransform.anchoredPosition, targetPosition, motionDuration)
            .setEase(easeType)
            .setOnUpdate((Vector2 value) => { rectTransform.anchoredPosition = value; })
            .setOnComplete(() => { moveCompleted = true; });

        LeanTween.value(gameObject, canvasGroup.alpha, targetAlpha, motionDuration)
            .setEase(easeType)
            .setOnUpdate((float value) => { canvasGroup.alpha = value; })
            .setOnComplete(() => { fadeCompleted = true; });

        while (!moveCompleted || !fadeCompleted) {
            yield return null;
        }
    }

    void OnDisable() {
        if (sequenceCoroutine != null) {
            StopCoroutine(sequenceCoroutine);
            sequenceCoroutine = null;
        }

        LeanTween.cancel(gameObject);
        Reset();
    }
}