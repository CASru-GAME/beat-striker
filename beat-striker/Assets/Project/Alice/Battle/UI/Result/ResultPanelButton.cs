using UnityEngine;
using UnityEngine.UI;
using System.Threading.Tasks;
using Alice;

[RequireComponent(typeof(Button))]
[RequireComponent(typeof(AudioSource))]
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
    [Range(0f, 1f)]
    public float buttonClickSoundVolume = 1f; // ボタンクリック音の音量
    public float buttonClickSoundDelay = 0f; // ボタンクリック音の遅延
    public AudioClip blackImageSound; // 黒いImageが動く時の効果音
    [Range(0f, 1f)]
    public float blackImageSoundVolume = 1f; // BlackImage音の音量
    public float blackImageSoundDelay = 0f; // BlackImage音の遅延（BlackImageアニメーション開始からの時間）
    public AudioClip lineSound; // 白い線が動く時の効果音
    [Range(0f, 1f)]
    public float lineSoundVolume = 1f; // Line音の音量
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
    public float winnerHideDelay = 0.6f; // Winner表示を消し始めるまでの遅延
    public float winnerHideDuration = 0.4f; // Winner表示を消す時間
    
    [Header("GoBack Button Animation")]
    public GameObject goBackSceneImage; // 白いImage（GoBackSceneImage）
    public GameObject nextText; // Textオブジェクト（Next）
    public float goBackDelay = 0.2f; // GoBackボタン出現の遅延（Icon開始からの時間）
    public float goBackScaleDuration = 0.5f; // GoBackボタンスケール時間
    public float nextTextDelay = 0.5f; // Nextテキスト出現の遅延（GoBackアニメーション完了からの時間）
    public float nextTextFadeDuration = 0.8f; // NextテキストフェードIN/OUT時間（1サイクル）
    public float nextTextMinAlpha = 0.3f; // Nextテキストの最小透明度
    public float nextTextMaxAlpha = 1f; // Nextテキストの最大透明度
    
    [Header("Auto Start")]
    public bool autoStart = true; // 自動的にアニメーションを開始するか
    public float autoStartDelay = 0.5f; // 自動開始の遅延時間
    
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
    private CanvasGroup playerWinnerCanvasGroup;
    
    private Vector3 scoreAboveOriginalPos;
    private Vector3 scoreUnderOriginalPos;
    private Vector3 comboAboveOriginalPos;
    private Vector3 comboUnderOriginalPos;
    
    // GoBackボタン関連
    private RectTransform goBackRect;
    private Button goBackButton;
    private CanvasGroup nextTextCanvasGroup;
    private TaskCompletionSource<bool> phase1CompletionSource;
    private bool hasPlayedPhase1;
    private bool hasPlayedPhase2;
    private bool isInitialized;

    void Start()
    {
        InitializeIfNeeded();
    }

    void InitializeIfNeeded()
    {
        if (isInitialized)
        {
            return;
        }

        button = GetComponent<Button>();
        audioSource = GetComponent<AudioSource>();
        
        // AudioSourceが無ければ追加
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
        
        Debug.Log($"ResultPanelButton Start - Button: {button != null}");
        Debug.Log($"ResultPanelButton GameObject: {gameObject.name}, Active: {gameObject.activeInHierarchy}");
        
        // Canvas確認
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas != null) {
            Debug.Log($"Canvas found: {canvas.name}, Enabled: {canvas.enabled}, RenderMode: {canvas.renderMode}, SortOrder: {canvas.sortingOrder}");
        } else {
            Debug.LogWarning("No Canvas found in parent hierarchy!");
        }
        
        // 親階層を確認
        Transform current = transform;
        while (current != null) {
            Debug.Log($"Hierarchy: {current.name}, Active: {current.gameObject.activeInHierarchy}, ActiveSelf: {current.gameObject.activeSelf}");
            current = current.parent;
        }
        
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
        
        // GoBackボタンの初期設定
        if (goBackSceneImage != null)
        {
            goBackRect = goBackSceneImage.GetComponent<RectTransform>();
            goBackButton = goBackSceneImage.GetComponent<Button>();
            goBackRect.localScale = Vector3.zero;
            goBackSceneImage.SetActive(false);
            
            // ボタンを無効化
            if (goBackButton != null)
            {
                goBackButton.interactable = false;
            }
        }
        
        // Nextテキストの初期設定
        if (nextText != null)
        {
            nextTextCanvasGroup = nextText.GetComponent<CanvasGroup>();
            if (nextTextCanvasGroup == null)
            {
                nextTextCanvasGroup = nextText.AddComponent<CanvasGroup>();
            }
            nextTextCanvasGroup.alpha = 0f;
        }
        
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
        
        // 自動開始
        if (autoStart)
        {
            Debug.Log($"Auto-starting animation after {autoStartDelay} seconds");
            LeanTween.delayedCall(autoStartDelay, () => {
                if (!hasPlayed)
                {
                    Debug.Log("Auto-starting animation now");
                    StartAnimation();
                }
            });
        }

        isInitialized = true;
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

            if (panel == playerWinnerPanel)
            {
                playerWinnerCanvasGroup = panel.GetComponent<CanvasGroup>();
                if (playerWinnerCanvasGroup == null)
                {
                    playerWinnerCanvasGroup = panel.AddComponent<CanvasGroup>();
                }
                playerWinnerCanvasGroup.alpha = 1f;
            }
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
        
        Debug.Log("Button clicked!");
        StartAnimation();
    }

    public void RestartFromFlow()
    {
        ResetAnimation();
        StartPhase1FromFlow();
        ContinueToPhase2FromFlow();
    }

    public void StartPhase1FromFlow()
    {
        InitializeIfNeeded();
        phase1CompletionSource?.TrySetCanceled();
        phase1CompletionSource = new TaskCompletionSource<bool>();
        hasPlayedPhase1 = false;
        hasPlayedPhase2 = false;
        StartPhase1Animation();
    }

    public Task WaitForPhase1CompletedAsync()
    {
        return phase1CompletionSource?.Task ?? Task.CompletedTask;
    }

    public void ContinueToPhase2FromFlow()
    {
        InitializeIfNeeded();
        if (!hasPlayedPhase1 || hasPlayedPhase2)
        {
            return;
        }

        hasPlayedPhase2 = true;
        StartPhase2Animation();
    }
    
    void StartAnimation()
    {
        InitializeIfNeeded();
        StartPhase1FromFlow();
        ContinueToPhase2FromFlow();
    }

    void StartPhase1Animation()
    {
        hasPlayed = true;
        Debug.Log("Starting result phase1 animation");
        
        // ボタンクリック効果音（遅延付き）
        if (buttonClickSoundDelay > 0)
        {
            LeanTween.delayedCall(buttonClickSoundDelay, () => PlaySound(buttonClickSound, buttonClickSoundVolume));
        }
        else
        {
            PlaySound(buttonClickSound, buttonClickSoundVolume);
        }
        
        float phase1EndTime = 0f;

        LeanTween.delayedCall(gameObject, phase1EndTime, () =>
        {
            hasPlayedPhase1 = true;
            phase1CompletionSource?.TrySetResult(true);
        });
    }

    void StartPhase2Animation()
    {
        Debug.Log("Starting result phase2 animation");

        AnimateBlackImage(blackImageDelay);

        // LineはEast入力後(2段階目)で再生
        float lineStartTime = blackImageDelay + lineDelay;
        AnimateLinePanels(lineStartTime);

        // SCOREアニメーション
        float scoreStartTime = lineStartTime + scoreDelay;
        AnimateScorePanels(scoreStartTime);

        // COMBOアニメーション
        float comboStartTime = scoreStartTime + comboDelay;
        AnimateComboPanels(comboStartTime);

        // Iconアニメーション
        float iconStartTime = comboStartTime + iconDelay;
        AnimateIconPanels(iconStartTime);

        // GoBackボタンアニメーション
        float goBackStartTime = iconStartTime + goBackDelay;
        AnimateGoBackButton(goBackStartTime);
    }

    void AnimateBlackImage(float delay)
    {
        if (blackImage != null && blackImageRect != null)
        {
            float blackSoundTime = delay + blackImageSoundDelay;
            if (blackSoundTime > 0)
            {
                LeanTween.delayedCall(blackSoundTime, () => PlaySound(blackImageSound, blackImageSoundVolume));
            }
            else
            {
                PlaySound(blackImageSound, blackImageSoundVolume);
            }

            LeanTween.delayedCall(delay, () =>
            {
                blackImage.SetActive(true);
                LeanTween.cancel(blackImage);
                LeanTween.alphaCanvas(blackImageCanvasGroup, 1f, blackImageScaleDuration)
                    .setEase(LeanTweenType.easeOutQuad);
                LeanTween.scale(blackImageRect, Vector3.one, blackImageScaleDuration)
                    .setEase(LeanTweenType.easeOutBack);
            });
            return;
        }

        Debug.LogWarning("BlackImage or BlackImageRect is null!");
    }

    void AnimateLinePanels(float delay)
    {
        if (lineObject != null && lineRect != null)
        {
            LeanTween.delayedCall(delay, () =>
            {
                if (lineSoundDelay > 0)
                {
                    LeanTween.delayedCall(lineSoundDelay, () => PlaySound(lineSound, lineSoundVolume));
                }
                else
                {
                    PlaySound(lineSound, lineSoundVolume);
                }

                lineObject.SetActive(true);
                LeanTween.cancel(lineObject);

                if (useScaleAnimation)
                {
                    lineRect.localScale = new Vector3(0f, 1f, 1f);
                    LeanTween.scaleX(lineObject, 1f, lineExpandDuration)
                        .setEase(LeanTweenType.easeOutQuad);
                }
                else if (lineMask != null)
                {
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
            return;
        }

        Debug.LogWarning("LineObject or LineRect is null!");
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
                if (playerWinnerCanvasGroup != null)
                {
                    playerWinnerCanvasGroup.alpha = 1f;
                }
                
                // Scale 0 → iconOvershoot (1.1)
                LeanTween.scale(playerWinnerPanel, Vector3.one * iconOvershoot, iconPopupDuration * 0.6f)
                    .setEase(LeanTweenType.easeOutQuad)
                    .setOnComplete(() =>
                    {
                        // Scale iconOvershoot → 1
                        LeanTween.scale(playerWinnerPanel, Vector3.one, iconPopupDuration * 0.4f)
                            .setEase(LeanTweenType.easeInQuad)
                            .setOnComplete(() =>
                            {
                                if (playerWinnerCanvasGroup == null)
                                {
                                    playerWinnerPanel.SetActive(false);
                                    return;
                                }

                                LeanTween.alphaCanvas(playerWinnerCanvasGroup, 0f, winnerHideDuration)
                                    .setDelay(winnerHideDelay)
                                    .setEase(LeanTweenType.easeInQuad)
                                    .setOnComplete(() =>
                                    {
                                        playerWinnerPanel.SetActive(false);
                                    });
                            });
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
    
    void AnimateGoBackButton(float delay)
    {
        if (goBackSceneImage != null && goBackRect != null)
        {
            LeanTween.delayedCall(delay, () =>
            {
                Debug.Log("GoBackSceneImage animation start");
                goBackSceneImage.SetActive(true);
                goBackRect.localScale = Vector3.zero;
                
                // スケールアニメーション（0 → 1）
                LeanTween.scale(goBackSceneImage, Vector3.one, goBackScaleDuration)
                    .setEase(LeanTweenType.easeOutBack)
                    .setOnComplete(() =>
                    {
                        // アニメーション完了後、ボタンを有効化
                        if (goBackButton != null)
                        {
                            goBackButton.interactable = true;
                            Debug.Log("GoBackButton enabled");
                        }
                        
                        // Nextテキストをフェードイン
                        AnimateNextText();
                    });
            });
        }
    }
    
    void AnimateNextText()
    {
        if (nextText != null && nextTextCanvasGroup != null)
        {
            LeanTween.delayedCall(nextTextDelay, () =>
            {
                Debug.Log("NextText fade loop start");
                
                // 最小透明度からスタート
                nextTextCanvasGroup.alpha = nextTextMinAlpha;
                
                // フェードイン→フェードアウトをループ
                LeanTween.alphaCanvas(nextTextCanvasGroup, nextTextMaxAlpha, nextTextFadeDuration / 2f)
                    .setEase(LeanTweenType.easeInOutQuad)
                    .setLoopPingPong(); // ピンポンループ(往復)
            });
        }
    }
    
    void PlaySound(AudioClip clip, float volume = 1f)
    {
        if (audioSource != null && clip != null)
        {
            audioSource.PlayOneShot(clip, volume);
        }
    }
    
    void OnDestroy()
    {
        // イベントリスナーを解除
        if (button != null)
        {
            button.onClick.RemoveListener(OnButtonClick);
        }
    }
    
    // リセット用（必要に応じて）
    public void ResetAnimation()
    {
        hasPlayed = false; // フラグをリセット
        hasPlayedPhase1 = false;
        hasPlayedPhase2 = false;
        phase1CompletionSource?.TrySetCanceled();
        
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

        if (playerWinnerPanel != null)
        {
            LeanTween.cancel(playerWinnerPanel);
            if (playerWinnerCanvasGroup != null)
            {
                playerWinnerCanvasGroup.alpha = 1f;
            }
            playerWinnerPanel.SetActive(false);
        }
    }
}
