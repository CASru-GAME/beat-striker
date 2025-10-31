using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class ResultPanelButton : MonoBehaviour
{
    [Header("References")]
    public GameObject blackImage; // BlackImageオブジェクト
    public GameObject lineObject; // Lineオブジェクト
    public RectTransform lineMask; // Lineのマスク用RectTransform（オプション）
    public GameObject scoreAbovePanel; // ScoreabovePanel
    public GameObject scoreUnderPanel; // ScoreunderPanel
    public GameObject comboAbovePanel; // ComboabovePanel
    public GameObject comboUnderPanel; // CombounderPanel
    public GameObject playerWinnerPanel; // PlayerWinnerPanel
    public GameObject playerLoserPanel; // PlayerLoserPanel
    
    [Header("Sound Effects")]
    public AudioClip buttonClickSound; // 赤いImageを押した時の効果音
    public float buttonClickSoundDelay = 0f; // ボタンクリック音の遅延
    public AudioClip blackImageSound; // 黒いImageが動く時の効果音
    public float blackImageSoundDelay = 0f; // BlackImage音の遅延（BlackImageアニメーション開始からの時間）
    public AudioClip lineSound; // 白い線が動く時の効果音
    public float lineSoundDelay = 0f; // Line音の遅延（Lineアニメーション開始からの時間）
    
    [Header("Animation Settings")]
    public float blackImageScaleDuration = 0.5f; // BlackImage拡大時間
    public float blackImageDelay = 0f; // BlackImage出現の遅延
    public float lineDelay = 0.3f; // Line出現の遅延（BlackImage開始からの時間）
    public float lineExpandDuration = 0.5f; // Line拡大時間
    public bool useScaleAnimation = true; // trueならスケール、falseならマスク
    
    [Header("SCORE Animation")]
    public float scoreDelay = 0.2f; // SCORE出現の遅延（Line開始からの時間）
    public float scoreSlideDuration = 0.5f; // SCOREスライド時間
    public float scoreSlideDistance = 200f; // SCOREスライド距離
    
    [Header("COMBO Animation")]
    public float comboDelay = 0.3f; // COMBO出現の遅延（SCORE開始からの時間）
    public float comboSlideDuration = 0.5f; // COMBOスライド時間
    public float comboSlideDistance = 300f; // COMBOスライド距離
    
    [Header("Icon Animation")]
    public float iconDelay = 0.1f; // Icon出現の遅延（COMBO開始からの時間）
    public float iconPopupDuration = 0.6f; // Iconポップアップ時間
    public float iconOvershoot = 1.1f; // Iconオーバーシュート倍率
    
    private Button button;
    private AudioSource audioSource;
    private CanvasGroup blackImageCanvasGroup;
    private RectTransform blackImageRect;
    private RectTransform lineRect;
    private Vector3 lineOriginalPosition;
    private bool hasPlayed = false; // アニメーションが既に再生されたかどうか
    
    // 各パネルのRectTransformと初期位置
    private RectTransform scoreAboveRect;
    private RectTransform scoreUnderRect;
    private RectTransform comboAboveRect;
    private RectTransform comboUnderRect;
    private RectTransform playerWinnerRect;
    private RectTransform playerLoserRect;
    
    private Vector3 scoreAboveOriginalPos;
    private Vector3 scoreUnderOriginalPos;
    private Vector3 comboAboveOriginalPos;
    private Vector3 comboUnderOriginalPos;

    void Start()
    {
        button = GetComponent<Button>();
        audioSource = GetComponent<AudioSource>();
        
        // AudioSourceが無ければ追加
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
        
        Debug.Log($"ResultPanelButton Start - Button: {button != null}");
        
        // BlackImageの初期設定
        if (blackImage != null)
        {
            Debug.Log("BlackImage found");
            blackImageRect = blackImage.GetComponent<RectTransform>();
            blackImageCanvasGroup = blackImage.GetComponent<CanvasGroup>();
            if (blackImageCanvasGroup == null)
            {
                blackImageCanvasGroup = blackImage.AddComponent<CanvasGroup>();
            }
            
            // 初期状態：非表示、スケール0
            blackImageCanvasGroup.alpha = 0f;
            blackImageRect.localScale = Vector3.zero;
        }
        else
        {
            Debug.LogWarning("BlackImage is not assigned!");
        }
        
        // Lineの初期設定
        if (lineObject != null)
        {
            Debug.Log("LineObject found");
            lineRect = lineObject.GetComponent<RectTransform>();
            lineOriginalPosition = lineRect.localPosition;
            
            // 初期状態：スケールを横方向0に
            if (useScaleAnimation)
            {
                lineRect.localScale = new Vector3(0f, 1f, 1f);
            }
            
            // 非表示
            lineObject.SetActive(false);
        }
        else
        {
            Debug.LogWarning("LineObject is not assigned!");
        }
        
        // SCOREパネルの初期設定
        InitializePanel(scoreAbovePanel, ref scoreAboveRect, ref scoreAboveOriginalPos, new Vector3(0f, scoreSlideDistance, 0f)); // 上から下（上の画面外からスタート）
        InitializePanel(scoreUnderPanel, ref scoreUnderRect, ref scoreUnderOriginalPos, new Vector3(0f, -scoreSlideDistance, 0f)); // 下から上（下の画面外からスタート）
        
        // COMBOパネルの初期設定
        InitializePanel(comboAbovePanel, ref comboAboveRect, ref comboAboveOriginalPos, new Vector3(-comboSlideDistance, 0f, 0f));
        InitializePanel(comboUnderPanel, ref comboUnderRect, ref comboUnderOriginalPos, new Vector3(comboSlideDistance, 0f, 0f));
        
        // Iconパネルの初期設定
        InitializeIconPanel(playerWinnerPanel, ref playerWinnerRect);
        InitializeIconPanel(playerLoserPanel, ref playerLoserRect);
        
        // ボタンクリックイベント
        if (button != null)
        {
            button.onClick.AddListener(OnButtonClick);
            Debug.Log("Button onClick listener added");
        }
        else
        {
            Debug.LogError("Button component not found!");
        }
    }
    
    void InitializePanel(GameObject panel, ref RectTransform rect, ref Vector3 originalPos, Vector3 offset)
    {
        if (panel != null)
        {
            rect = panel.GetComponent<RectTransform>();
            originalPos = rect.localPosition;
            rect.localPosition = originalPos + offset;
            panel.SetActive(false);
        }
    }
    
    void InitializeIconPanel(GameObject panel, ref RectTransform rect)
    {
        if (panel != null)
        {
            rect = panel.GetComponent<RectTransform>();
            rect.localScale = Vector3.zero;
            panel.SetActive(false);
        }
    }

    void OnButtonClick()
    {
        // 既にアニメーションが再生されている場合は何もしない
        if (hasPlayed)
        {
            Debug.Log("Animation already played, ignoring click");
            return;
        }
        
        hasPlayed = true;
        Debug.Log("Button clicked!");
        
        // ボタンクリック効果音（遅延付き）
        if (buttonClickSoundDelay > 0)
        {
            LeanTween.delayedCall(buttonClickSoundDelay, () => PlaySound(buttonClickSound));
        }
        else
        {
            PlaySound(buttonClickSound);
        }
        
        // BlackImageの拡大アニメーション
        if (blackImage != null && blackImageRect != null)
        {
            Debug.Log("Starting BlackImage animation");
            
            // BlackImage効果音（BlackImageアニメーション開始時からの遅延）
            float blackSoundTime = blackImageDelay + blackImageSoundDelay;
            if (blackSoundTime > 0)
            {
                LeanTween.delayedCall(blackSoundTime, () => PlaySound(blackImageSound));
            }
            else
            {
                PlaySound(blackImageSound);
            }
            
            blackImage.SetActive(true);
            
            // フェードイン＆スケールアニメーション
            LeanTween.cancel(blackImage);
            
            // アルファを1にフェードイン
            LeanTween.alphaCanvas(blackImageCanvasGroup, 1f, blackImageScaleDuration)
                .setDelay(blackImageDelay)
                .setEase(LeanTweenType.easeOutQuad);
            
            // スケールを0から1に拡大
            LeanTween.scale(blackImageRect, Vector3.one, blackImageScaleDuration)
                .setDelay(blackImageDelay)
                .setEase(LeanTweenType.easeOutBack);
        }
        
        // Lineのスプリット（中央から左右に広がる）アニメーション
        if (lineObject != null && lineRect != null)
        {
            Debug.Log("Starting Line split animation");
            
            // 遅延後に表示してスプリット開始
            LeanTween.delayedCall(blackImageDelay + lineDelay, () =>
            {
                Debug.Log("Line animation delayed call executed");
                
                // Line効果音（Lineアニメーション開始時からの遅延）
                if (lineSoundDelay > 0)
                {
                    LeanTween.delayedCall(lineSoundDelay, () => PlaySound(lineSound));
                }
                else
                {
                    PlaySound(lineSound);
                }
                
                lineObject.SetActive(true);
                
                LeanTween.cancel(lineObject);
                
                if (useScaleAnimation)
                {
                    // スケールアニメーション：X軸を0から1に拡大（中央から左右に広がる）
                    lineRect.localScale = new Vector3(0f, 1f, 1f);
                    LeanTween.scaleX(lineObject, 1f, lineExpandDuration)
                        .setEase(LeanTweenType.easeOutQuad);
                }
                else if (lineMask != null)
                {
                    // マスクアニメーション：幅を0から元の幅に拡大
                    float originalWidth = lineMask.sizeDelta.x;
                    lineMask.sizeDelta = new Vector2(0f, lineMask.sizeDelta.y);
                    LeanTween.value(lineObject, 0f, originalWidth, lineExpandDuration)
                        .setOnUpdate((float val) =>
                        {
                            lineMask.sizeDelta = new Vector2(val, lineMask.sizeDelta.y);
                        })
                        .setEase(LeanTweenType.easeOutQuad);
                }
            });
        }
        else
        {
            Debug.LogWarning("LineObject or LineRect is null!");
        }
        
        // SCOREアニメーション
        float scoreStartTime = blackImageDelay + lineDelay + scoreDelay;
        AnimateScorePanels(scoreStartTime);
        
        // COMBOアニメーション
        float comboStartTime = scoreStartTime + comboDelay;
        AnimateComboPanels(comboStartTime);
        
        // Iconアニメーション
        float iconStartTime = comboStartTime + iconDelay;
        AnimateIconPanels(iconStartTime);
    }
    
    void AnimateScorePanels(float delay)
    {
        // ScoreabovePanel: 上から下
        if (scoreAbovePanel != null && scoreAboveRect != null)
        {
            LeanTween.delayedCall(delay, () =>
            {
                Debug.Log("ScoreabovePanel animation start");
                scoreAbovePanel.SetActive(true);
                LeanTween.moveLocal(scoreAbovePanel, scoreAboveOriginalPos, scoreSlideDuration)
                    .setEase(LeanTweenType.easeOutQuad);
            });
        }
        
        // ScoreunderPanel: 下から上
        if (scoreUnderPanel != null && scoreUnderRect != null)
        {
            LeanTween.delayedCall(delay, () =>
            {
                Debug.Log("ScoreunderPanel animation start");
                scoreUnderPanel.SetActive(true);
                LeanTween.moveLocal(scoreUnderPanel, scoreUnderOriginalPos, scoreSlideDuration)
                    .setEase(LeanTweenType.easeOutQuad);
            });
        }
    }
    
    void AnimateComboPanels(float delay)
    {
        // ComboabovePanel: 左から右
        if (comboAbovePanel != null && comboAboveRect != null)
        {
            LeanTween.delayedCall(delay, () =>
            {
                Debug.Log("ComboabovePanel animation start");
                comboAbovePanel.SetActive(true);
                LeanTween.moveLocal(comboAbovePanel, comboAboveOriginalPos, comboSlideDuration)
                    .setEase(LeanTweenType.easeOutQuad);
            });
        }
        
        // CombounderPanel: 右から左
        if (comboUnderPanel != null && comboUnderRect != null)
        {
            LeanTween.delayedCall(delay, () =>
            {
                Debug.Log("CombounderPanel animation start");
                comboUnderPanel.SetActive(true);
                LeanTween.moveLocal(comboUnderPanel, comboUnderOriginalPos, comboSlideDuration)
                    .setEase(LeanTweenType.easeOutQuad);
            });
        }
    }
    
    void AnimateIconPanels(float delay)
    {
        // PlayerWinnerPanel: ポップアップ（Scale 0 → 1.1 → 1）
        if (playerWinnerPanel != null && playerWinnerRect != null)
        {
            LeanTween.delayedCall(delay, () =>
            {
                Debug.Log("PlayerWinnerPanel animation start");
                playerWinnerPanel.SetActive(true);
                playerWinnerRect.localScale = Vector3.zero;
                
                // Scale 0 → iconOvershoot (1.1)
                LeanTween.scale(playerWinnerPanel, Vector3.one * iconOvershoot, iconPopupDuration * 0.6f)
                    .setEase(LeanTweenType.easeOutQuad)
                    .setOnComplete(() =>
                    {
                        // Scale iconOvershoot → 1
                        LeanTween.scale(playerWinnerPanel, Vector3.one, iconPopupDuration * 0.4f)
                            .setEase(LeanTweenType.easeInQuad);
                    });
            });
        }
        
        // PlayerLoserPanel: ポップアップ（Scale 0 → 1.1 → 1）
        if (playerLoserPanel != null && playerLoserRect != null)
        {
            LeanTween.delayedCall(delay, () =>
            {
                Debug.Log("PlayerLoserPanel animation start");
                playerLoserPanel.SetActive(true);
                playerLoserRect.localScale = Vector3.zero;
                
                // Scale 0 → iconOvershoot (1.1)
                LeanTween.scale(playerLoserPanel, Vector3.one * iconOvershoot, iconPopupDuration * 0.6f)
                    .setEase(LeanTweenType.easeOutQuad)
                    .setOnComplete(() =>
                    {
                        // Scale iconOvershoot → 1
                        LeanTween.scale(playerLoserPanel, Vector3.one, iconPopupDuration * 0.4f)
                            .setEase(LeanTweenType.easeInQuad);
                    });
            });
        }
    }
    
    void PlaySound(AudioClip clip)
    {
        if (audioSource != null && clip != null)
        {
            audioSource.PlayOneShot(clip);
        }
    }
    
    // リセット用（必要に応じて）
    public void ResetAnimation()
    {
        hasPlayed = false; // フラグをリセット
        
        if (blackImage != null)
        {
            LeanTween.cancel(blackImage);
            blackImageCanvasGroup.alpha = 0f;
            blackImageRect.localScale = Vector3.zero;
            blackImage.SetActive(false);
        }
        
        if (lineObject != null)
        {
            LeanTween.cancel(lineObject);
            lineObject.SetActive(false);
            if (useScaleAnimation)
            {
                lineRect.localScale = new Vector3(0f, 1f, 1f);
            }
            lineRect.localPosition = lineOriginalPosition;
        }
    }
}
