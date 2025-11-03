using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 斜めストライプトランジションの見た目を制御するクラス
/// </summary>
public class DiagonalStripeVisual : MonoBehaviour {
    [Header("Transition Settings")]
    public int stripeCount = 7; // ストライプの本数
    public float transitionDuration = 1f; // 全体のトランジション時間
    public float stripeDelay = 0.05f; // 各ストライプ間の遅延
    public Color stripeColor = Color.black; // ストライプの色

    [Header("Stripe Size")]
    public float stripeSpacingMultiplier = 2.5f; // ストライプ間隔の倍率
    public float stripeWidthMultiplier = 0.8f; // ストライプ幅の倍率

    [Header("Stripe Position")]
    public Vector2 rightTopOffset = new Vector2(0.5f, 1f); // 右上バーのオフセット
    public Vector2 leftBottomOffset = new Vector2(-0.5f, -1f); // 左下バーのオフセット

    [Header("References")]
    public Canvas transitionCanvas; // トランジション用Canvas
    public GameObject stripePrefab; // ストライプのPrefab（無ければ自動生成）

    private List<RectTransform> stripes = new List<RectTransform>();
    private CanvasGroup canvasGroup;

    void Awake() {
        Debug.Log("DiagonalStripeVisual Awake called");

        // Canvas設定
        if (transitionCanvas == null) {
            transitionCanvas = GetComponentInChildren<Canvas>();
            Debug.Log($"Canvas found: {transitionCanvas != null}");
        }

        if (transitionCanvas != null) {
            transitionCanvas.sortingOrder = 9999; // 最前面に表示
            canvasGroup = transitionCanvas.GetComponent<CanvasGroup>();
            if (canvasGroup == null) {
                canvasGroup = transitionCanvas.gameObject.AddComponent<CanvasGroup>();
            }
        }

        CreateStripes();
    }

    void CreateStripes() {
        Debug.Log("CreateStripes called");

        if (transitionCanvas == null) {
            Debug.LogError("transitionCanvas is null!");
            return;
        }

        // ストライプを生成
        RectTransform canvasRect = transitionCanvas.GetComponent<RectTransform>();
        float screenWidth = canvasRect.rect.width;
        float screenHeight = canvasRect.rect.height;

        Debug.Log($"Canvas size: {screenWidth} x {screenHeight}");

        // 対角線の長さを計算
        float diagonal = Mathf.Sqrt(screenWidth * screenWidth + screenHeight * screenHeight);

        // ストライプの幅（画面を均等に分割）
        float stripeSpacing = Mathf.Max(screenWidth, screenHeight) / stripeCount * stripeSpacingMultiplier;

        for (int i = 0; i < stripeCount; i++) {
            GameObject stripeObj = new GameObject($"Stripe_{i}");
            stripeObj.transform.SetParent(transitionCanvas.transform);

            RectTransform rect = stripeObj.AddComponent<RectTransform>();
            Image image = stripeObj.AddComponent<Image>();
            image.color = stripeColor;

            // 偶数:右上→左下(↙)、奇数:左下→右上(↗)
            bool isRightTop = (i % 2 == 0);

            // ストライプの幅と長さ
            float stripeWidth = stripeSpacing * stripeWidthMultiplier;
            rect.sizeDelta = new Vector2(stripeWidth, 0); // 初期は長さ0

            // 45度回転
            rect.rotation = Quaternion.Euler(0, 0, 45f);

            // ピボットを設定（伸びる起点）
            if (isRightTop) {
                // 右上から左下へ伸びる
                rect.pivot = new Vector2(0.5f, 1f); // 上端を固定
            }
            else {
                // 左下から右上へ伸びる
                rect.pivot = new Vector2(0.5f, 0f); // 下端を固定
            }

            // 配置位置（斜め45度で均等配置）
            float offset = (i - stripeCount / 2f) * stripeSpacing;

            if (isRightTop) {
                // 右上の位置（画面外）
                rect.anchoredPosition = new Vector2(screenWidth * rightTopOffset.x + offset, screenHeight * rightTopOffset.y);
            }
            else {
                // 左下の位置（画面外）
                rect.anchoredPosition = new Vector2(screenWidth * leftBottomOffset.x + offset, screenHeight * leftBottomOffset.y);
            }

            rect.gameObject.SetActive(false);
            stripes.Add(rect);
        }

        Debug.Log($"Created {stripes.Count} stripes");
    }

    /// <summary>
    /// フェードインアニメーション(ストライプが画面を覆う)
    /// </summary>
    public IEnumerator PlayFadeIn() {
        RectTransform canvasRect = transitionCanvas.GetComponent<RectTransform>();
        float screenWidth = canvasRect.rect.width;
        float screenHeight = canvasRect.rect.height;
        float diagonal = Mathf.Sqrt(screenWidth * screenWidth + screenHeight * screenHeight);

        Debug.Log("FadeIn started");

        // 各ストライプを順次伸ばす
        for (int i = 0; i < stripes.Count; i++) {
            RectTransform stripe = stripes[i];
            stripe.gameObject.SetActive(true);

            // 長さ0から対角線の2倍まで伸びる
            Vector2 startSize = new Vector2(stripe.sizeDelta.x, 0);
            Vector2 endSize = new Vector2(stripe.sizeDelta.x, diagonal * 2f);

            stripe.sizeDelta = startSize;

            // アニメーション（サイズを変更して伸びる）
            LeanTween.cancel(stripe.gameObject);
            LeanTween.value(stripe.gameObject, 0f, diagonal * 2f, transitionDuration)
                .setOnUpdate((float val) => {
                    if (stripe != null) {
                        stripe.sizeDelta = new Vector2(stripe.sizeDelta.x, val);
                    }
                })
                .setEase(LeanTweenType.easeInOutQuad);

            // 次のストライプまで少し待機
            yield return new WaitForSeconds(stripeDelay);
        }

        // 最後のストライプが完了するまで待機
        yield return new WaitForSeconds(transitionDuration);
        Debug.Log("FadeIn complete");
    }

    /// <summary>
    /// フェードアウトアニメーション(ストライプが消える)
    /// </summary>
    public IEnumerator PlayFadeOut() {
        RectTransform canvasRect = transitionCanvas.GetComponent<RectTransform>();
        float screenWidth = canvasRect.rect.width;
        float screenHeight = canvasRect.rect.height;
        float diagonal = Mathf.Sqrt(screenWidth * screenWidth + screenHeight * screenHeight);

        Debug.Log("FadeOut started");

        // 各ストライプを順次縮める
        for (int i = 0; i < stripes.Count; i++) {
            RectTransform stripe = stripes[i];

            // 対角線の2倍から0まで縮む
            float startLength = diagonal * 2f;
            float endLength = 0f;

            Debug.Log($"Stripe {i} shrinking: {startLength} -> {endLength}");

            // アニメーション（サイズを変更して縮む）
            LeanTween.cancel(stripe.gameObject);
            LeanTween.value(stripe.gameObject, startLength, endLength, transitionDuration)
                .setOnUpdate((float val) => {
                    if (stripe != null) {
                        stripe.sizeDelta = new Vector2(stripe.sizeDelta.x, val);
                    }
                })
                .setOnComplete(() => {
                    if (stripe != null) {
                        stripe.gameObject.SetActive(false);
                    }
                })
                .setEase(LeanTweenType.easeInOutQuad);

            // 次のストライプまで少し待機
            yield return new WaitForSeconds(stripeDelay);
        }

        // 最後のストライプが完了するまで待機
        yield return new WaitForSeconds(transitionDuration);
        Debug.Log("FadeOut complete");
    }
}
