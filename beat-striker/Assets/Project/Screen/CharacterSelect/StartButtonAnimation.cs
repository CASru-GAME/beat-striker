using UnityEngine;
using UnityEngine.UI;
using Core;
using R3;
using System;
using System.Collections.Generic;

public class StartButtonAnimation : MonoBehaviour {
    const string LOG_PREFIX = "[StartButtonAnimation]";
    [Header("Line References")]
    public RectTransform whiteLineAbove; // 赤いImage（上）
    public RectTransform whiteLineUnder; // 青いImage（下）

    [Header("Text References")]
    public CanvasGroup textCanvasGroup; // STARTテキストのCanvasGroup

    [Header("Animation Settings")]
    public float lineDuration = 0.5f; // ラインの移動時間
    public float lineDistance = 500f; // ラインの移動距離
    public float textFadeDuration = 0.5f; // テキストのフェード時間
    public bool loopTextFade = true; // テキストのフェードをループするか

    [Header("Click Feedback")]
    public Characterselectbutton[] characterSelectButton; // Characterselectbutton参照
    [Range(0f, 1f)]
    public float clickSoundVolume = 1f; // YuusyaImageクリック時の音量
    public RectTransform clickTarget; // へこませる対象（黒い画像のRectTransform）
    public float scaleDownAmount = 0.95f; // へこむサイズ（1.0が元のサイズ）
    public float scaleDuration = 0.1f; // へこむアニメーションの時間

    [Header("Button Control")]
    public Botan[] blackImageButtons; // 黒い画像のボタン（Botanコンポーネント）
    public AudioClip blackImageClickSound; // 黒い画像がクリックされた時の効果音
    [Range(0f, 1f)]
    public float blackImageClickSoundVolume = 1f; // 黒い画像クリック時の音量

    [Header("Online Waiting Popup")]
    [SerializeField] RectTransform onlineWaitingPopupRoot;
    [SerializeField] CanvasGroup onlineWaitingPopupCanvasGroup;
    [SerializeField] AudioClip onlineWaitingShownSound;
    [SerializeField] float onlineWaitingFadeDuration = 0.2f;
    [SerializeField] float onlineWaitingScaleDuration = 0.2f;
    [SerializeField] Vector3 onlineWaitingHiddenScale = Vector3.zero;

    private Vector2 aboveStartPos;
    private Vector2 aboveEndPos;
    private Vector2 underStartPos;
    private Vector2 underEndPos;

    private bool animationPlayed = false;
    private int animationToken = 0;
    private bool blackImageSoundEnabled = false; // 黒い画像の音が有効かどうか
    private float lastClickTime = -999f; // 最後にクリックした時間
    private float clickDebounceTime = 0.2f; // クリック間隔（秒）
    private bool isOnlineWaitingPopupVisible;
    private int waitingPopupAnimationToken;
    private readonly List<Botan> runtimeBlackImageButtons = new();
    private readonly Subject<Unit> startRequested = new();

    public bool IsStartInputReady => blackImageSoundEnabled;
    public Observable<Unit> OnStartRequested => startRequested;

    public Botan backgroundBotan; // 背景のBotanコンポーネント

    void Awake() {
        if (characterSelectButton == null) {
            characterSelectButton = Array.Empty<Characterselectbutton>();
        }
        if (blackImageButtons == null) {
            blackImageButtons = Array.Empty<Botan>();
        }

        if (backgroundBotan != null) {
            backgroundBotan.gameObject.SetActive(false); // 最初は無効
        }

        onlineWaitingPopupRoot.gameObject.SetActive(false);
        onlineWaitingPopupRoot.localScale = onlineWaitingHiddenScale;
        onlineWaitingPopupCanvasGroup.alpha = 0f;

        // 初期位置を保存
        aboveEndPos = whiteLineAbove.anchoredPosition;
        aboveStartPos = aboveEndPos + new Vector2(-lineDistance, 0); // 左側
        whiteLineAbove.anchoredPosition = aboveStartPos;

        underEndPos = whiteLineUnder.anchoredPosition;
        underStartPos = underEndPos + new Vector2(lineDistance, 0); // 右側
        whiteLineUnder.anchoredPosition = underStartPos;

        // Textを透明に
        if (textCanvasGroup != null) {
            textCanvasGroup.alpha = 0f;
        }

        // 黒い画像のボタンを無効化（Botanコンポーネントのみ）
        runtimeBlackImageButtons.Clear();
        for (var i = 0; i < blackImageButtons.Length; i++) {
            var button = blackImageButtons[i];
            if (button == null) {
                continue;
            }

            runtimeBlackImageButtons.Add(button);
        }

        Debug.Log($"Black image buttons array length: {runtimeBlackImageButtons.Count}");
        for (int i = 0; i < runtimeBlackImageButtons.Count; i++) {
            var button = runtimeBlackImageButtons[i];
            Debug.Log($"Registering button {i}: {button.gameObject.name}");
            button.enabled = false;
            // 効果音イベントを登録（無効中は発火しない）
            int index = i; // ローカルコピー
            button.OnClickEvent.Subscribe((data) => OnBlackImageClicked(data, index));
        }
    }

    void OnDestroy() {
        startRequested.Dispose();
    }

    public void SetAllStrikersSelected(bool allSelected) {
        if (allSelected) {
            // アニメーションがまだ再生されていない場合はアニメーション実行
            if (!animationPlayed) {
                // クリックフィードバック（効果音とへこみ）
                foreach (var b in characterSelectButton) {
                    if (b == null) {
                        continue;
                    }

                    b.PlayClickFeedback(clickTarget, scaleDownAmount, scaleDuration);
                }

                animationPlayed = true;
                AnimateLines();
            }
        }
        else {
            // アニメーションを逆向きに再生して非表示状態に戻す
            if (animationPlayed) {
                animationPlayed = false;
                HideLines();
            }
        }
    }

    public void SetOnlineWaitingPopupVisible(bool visible) {
        if (isOnlineWaitingPopupVisible == visible) {
            return;
        }

        isOnlineWaitingPopupVisible = visible;
        var token = ++waitingPopupAnimationToken;
        LeanTween.cancel(onlineWaitingPopupRoot.gameObject);
        LeanTween.cancel(onlineWaitingPopupCanvasGroup.gameObject);

        if (visible) {
            onlineWaitingPopupRoot.gameObject.SetActive(true);
            onlineWaitingPopupRoot.localScale = onlineWaitingHiddenScale;
            onlineWaitingPopupCanvasGroup.alpha = 0f;
            onlineWaitingShownSound.PlayAtApp();

            LeanTween.scale(onlineWaitingPopupRoot, Vector3.one, onlineWaitingScaleDuration)
                .setEase(LeanTweenType.easeOutBack);
            LeanTween.alphaCanvas(onlineWaitingPopupCanvasGroup, 1f, onlineWaitingFadeDuration)
                .setEase(LeanTweenType.easeInOutQuad);
            return;
        }

        LeanTween.scale(onlineWaitingPopupRoot, onlineWaitingHiddenScale, onlineWaitingScaleDuration)
            .setEase(LeanTweenType.easeInBack);
        LeanTween.alphaCanvas(onlineWaitingPopupCanvasGroup, 0f, onlineWaitingFadeDuration)
            .setEase(LeanTweenType.easeInOutQuad)
            .setOnComplete(() => {
                if (token != waitingPopupAnimationToken || isOnlineWaitingPopupVisible) {
                    return;
                }

                onlineWaitingPopupRoot.gameObject.SetActive(false);
            });
    }
    
    void AnimateLines() {
        var token = ++animationToken;
        LeanTween.cancel(whiteLineAbove.gameObject);
        LeanTween.cancel(whiteLineUnder.gameObject);
        LeanTween.cancel(textCanvasGroup.gameObject);

        // 赤いLine（左から右へ）
        LeanTween.move(whiteLineAbove, aboveEndPos, lineDuration)
            .setEase(LeanTweenType.easeOutQuad);

        // 青いLine（右から左へ）
        LeanTween.move(whiteLineUnder, underEndPos, lineDuration)
            .setEase(LeanTweenType.easeOutQuad)
            .setOnComplete(() => ShowText(token));
    }

    void ShowText(int token) {
        if (token != animationToken || !animationPlayed) {
            return;
        }

        if (textCanvasGroup == null) {
            Debug.LogWarning($"{LOG_PREFIX} ShowText skipped because textCanvasGroup is null");
            blackImageSoundEnabled = true;
            if (backgroundBotan != null) {
                backgroundBotan.gameObject.SetActive(true);
            }
            return;
        }

        if (loopTextFade) {
            // フェードイン・アウトをループ
            LeanTween.alphaCanvas(textCanvasGroup, 1f, textFadeDuration)
                .setEase(LeanTweenType.easeInOutQuad)
                .setLoopPingPong();
        }
        else {
            // 一度だけフェードイン
            LeanTween.alphaCanvas(textCanvasGroup, 1f, textFadeDuration)
                .setEase(LeanTweenType.easeInOutQuad);
        }

        // アニメーション完了後、黒い画像のボタンと音を有効化
        foreach (var button in runtimeBlackImageButtons) {
            button.enabled = true;
        }

        // 黒い画像の音を有効化
        blackImageSoundEnabled = true;
        if (backgroundBotan != null) {
            backgroundBotan.gameObject.SetActive(true);
        }
    }

    void HideLines() {
        var token = ++animationToken;
        LeanTween.cancel(whiteLineAbove.gameObject);
        LeanTween.cancel(whiteLineUnder.gameObject);
        if (textCanvasGroup != null) {
            LeanTween.cancel(textCanvasGroup.gameObject);
        }

        // 黒い画像のボタンと音を無効化
        if (backgroundBotan != null) {
            backgroundBotan.gameObject.SetActive(false);
        }
        foreach (var button in runtimeBlackImageButtons) {
            button.enabled = false;
        }
        blackImageSoundEnabled = false;

        // テキストを非表示
        if (textCanvasGroup == null) {
            ReverseAnimateLines(token);
            return;
        }

        LeanTween.alphaCanvas(textCanvasGroup, 0f, textFadeDuration)
            .setEase(LeanTweenType.easeInOutQuad)
            .setOnComplete(() => ReverseAnimateLines(token));
    }

    void ReverseAnimateLines(int token) {
        if (token != animationToken || animationPlayed) {
            return;
        }

        // 赤いLine（右から左へ、元の位置に戻す）
        LeanTween.move(whiteLineAbove, aboveStartPos, lineDuration)
            .setEase(LeanTweenType.easeOutQuad);

        // 青いLine（左から右へ、元の位置に戻す）
        LeanTween.move(whiteLineUnder, underStartPos, lineDuration)
            .setEase(LeanTweenType.easeOutQuad);
    }

    void OnBlackImageClicked(BotanEventData data, int buttonIndex) {
        Debug.Log($"Black image button {buttonIndex} clicked! Sound enabled: {blackImageSoundEnabled}, Time: {Time.time}");

        // デバウンス: 短時間での連続クリックを防ぐ
        if (Time.time - lastClickTime < clickDebounceTime) {
            Debug.Log($"Click debounced! Time since last click: {Time.time - lastClickTime}");
            return;
        }

        lastClickTime = Time.time;

        // アニメーション完了後のみ効果音を再生
        if (blackImageSoundEnabled) {
            PlaySoundAtVolume(blackImageClickSound, blackImageClickSoundVolume);
            startRequested.OnNext(Unit.Default);
        }
    }

    void PlaySoundAtVolume(AudioClip clip, float volume) {
        if (clip == null || Camera.main == null) {
            return;
        }

        GameObject tempAudio = new GameObject("TempAudio");
        tempAudio.transform.position = Camera.main.transform.position;
        AudioSource audioSource = tempAudio.AddComponent<AudioSource>();
        audioSource.clip = clip;
        audioSource.volume = volume;
        audioSource.Play();
        Destroy(tempAudio, clip.length);
    }
}
