using System;
using Core.Battle;
using Core.Utils;
using TMPro;
using UnityEngine;

public class BattleCanvas : MonoBehaviour {
    [SerializeField] TextMeshProUGUI BattleStartText;
    [SerializeField] TextMeshProUGUI BattleFinishText;
    [SerializeField] TextMeshProUGUI RoundNumberText; // 新しいテキスト（Round 1など）
    [SerializeField] CanvasGroup RoundNumberCanvasGroup; // RoundNumberTextのCanvasGroup
    [SerializeField] CanvasGroup fadePanel; // 暗転用のパネル
    
    [Header("Animation Timing")]
    [SerializeField] float roundFadeInDuration = 0.5f; // Roundのフェードイン時間
    [SerializeField] float roundDisplayDuration = 0.5f; // Roundの表示時間
    [SerializeField] float roundFadeOutDuration = 0.5f; // Roundのフェードアウト時間
    [SerializeField] float delayBeforeFight = 0.2f; // RoundとFightの間隔
    [SerializeField] float fadePanelDuration = 0.5f; // 暗転のフェード時間
    [SerializeField] float fadeHoldDuration = 0.5f; // 暗転を保持する時間
    [SerializeField] float delayBeforeRoundText = 0.5f; // 明転後、Roundテキスト表示までの待機時間
    
    private IBus bus;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Awake() {
        this.bus = this.GetBus();
        Debug.Log("BattleCanvas Awake");
        BattleFinishText.gameObject.SetActive(false);
        BattleStartText.gameObject.SetActive(false);
        RoundNumberText.gameObject.SetActive(false);
        
        // 暗転パネルを初期状態で透明にする
        if (fadePanel != null) {
            fadePanel.alpha = 0f;
            fadePanel.gameObject.SetActive(false);
        }
        
        bus.Subscribe<BattleMessages.OnOutroStarted>(OnOutroStarted);
        bus.Subscribe<BattleMessages.OnBattleFinished>(OnBattleFinished);
        bus.Subscribe<BattleMessages.OnRoundStarted>(OnRoundStarted);
    }

    void OnDestroy() {
        bus.Unsubscribe<BattleMessages.OnOutroStarted>(OnOutroStarted);
        bus.Unsubscribe<BattleMessages.OnBattleFinished>(OnBattleFinished);
        bus.Unsubscribe<BattleMessages.OnRoundStarted>(OnRoundStarted);
    }

    // Update is called once per frame
    void Update() {

    }

    void OnRoundStarted(BattleMessages.OnRoundStarted msg) {
        Debug.Log("Round Started Animation");
        
        // ラウンド番号を+1して表示
        int roundNumber = msg.battlemodel.GetCurrentRound() + 1;
        
        // RoundNumberTextが設定されているか確認
        if (RoundNumberText == null) {
            Debug.LogError("RoundNumberText is not assigned in Inspector!");
            ShowFightText(); // スキップしてFightを表示
            return;
        }
        
        Debug.Log($"Showing Round {roundNumber}");
        
        // 待機時間後にRoundテキストを表示
        LeanTween.delayedCall(delayBeforeRoundText, () => {
            ShowRoundText(roundNumber);
        });
    }
    
    void ShowRoundText(int roundNumber) {
        // フェーズ1: 「Round X」をフェードイン・アウト
        RoundNumberText.text = $"Round {roundNumber}";
        RoundNumberText.gameObject.SetActive(true);
        
        if (RoundNumberCanvasGroup != null) {
            RoundNumberCanvasGroup.alpha = 0f; // 初期状態：透明
            
            // フェードイン
            LeanTween.alphaCanvas(RoundNumberCanvasGroup, 1f, roundFadeInDuration)
                .setEase(LeanTweenType.easeInOutQuad)
                .setOnComplete(() => {
                    // フェードイン完了後、表示時間待ってからフェードアウト
                    LeanTween.alphaCanvas(RoundNumberCanvasGroup, 0f, roundFadeOutDuration)
                        .setDelay(roundDisplayDuration)
                        .setEase(LeanTweenType.easeInOutQuad)
                        .setOnComplete(() => {
                            RoundNumberText.gameObject.SetActive(false);
                            // 間隔を空けてからFightを表示
                            LeanTween.delayedCall(delayBeforeFight, ShowFightText);
                        });
                });
        } else {
            Debug.LogError("RoundNumberCanvasGroup is not assigned!");
            ShowFightText();
        }
    }
    
    void ShowFightText() {
        // フェーズ2: 「Fight!」を拡大縮小アニメーション
        BattleStartText.text = "Fight!";
        BattleStartText.gameObject.SetActive(true);
        
        // 初期状態：画面いっぱいのサイズ
        BattleStartText.transform.localScale = Vector3.one * 10f;
        
        // アニメーション：通常サイズに縮小
        LeanTween.scale(BattleStartText.gameObject, Vector3.one, 0.5f)
            .setEase(LeanTweenType.easeOutQuad);
        
        // 1.0秒後に消える
        LeanTween.delayedCall(1.0f, () => {
            BattleStartText.gameObject.SetActive(false);
            bus.Publish(new BattleMessages.NotifyRoundStartAnimationFinished());
        });
    }

    void OnBattleFinished(BattleMessages.OnBattleFinished msg) {
        Debug.Log("Battle Finished");
        
        BattleFinishText.gameObject.SetActive(true);
        
        // 初期状態：画面いっぱいのサイズ
        BattleFinishText.transform.localScale = Vector3.one * 10f;
        
        // アニメーション：通常サイズに縮小（遅め：0.8秒）
        LeanTween.scale(BattleFinishText.gameObject, Vector3.one, 0.8f)
            .setEase(LeanTweenType.easeOutQuad);
        
        // 1.5秒後に消えて暗転開始
        LeanTween.delayedCall(1.5f, () => {
            BattleFinishText.gameObject.SetActive(false);
            ShowFadeTransition(); // 暗転を表示
        });
    }
    
    void ShowFadeTransition() {
        if (fadePanel == null) {
            Debug.LogWarning("Fade panel is not assigned!");
            bus.Publish(new BattleMessages.NotifyRoundFinishAnimationFinished());
            return;
        }
        
        fadePanel.gameObject.SetActive(true);
        fadePanel.alpha = 0f;
        
        // フェードイン（暗転）
        LeanTween.alphaCanvas(fadePanel, 1f, fadePanelDuration)
            .setEase(LeanTweenType.easeInOutQuad)
            .setOnComplete(() => {
                // 完全に暗くなったタイミングで次のRound準備を開始
                bus.Publish(new BattleMessages.NotifyRoundFinishAnimationFinished());
                
                // 暗転を保持
                LeanTween.delayedCall(fadeHoldDuration, () => {
                    // フェードアウト（明転）
                    LeanTween.alphaCanvas(fadePanel, 0f, fadePanelDuration)
                        .setEase(LeanTweenType.easeInOutQuad)
                        .setOnComplete(() => {
                            fadePanel.gameObject.SetActive(false);
                        });
                });
            });
    }

    void OnOutroStarted(BattleMessages.OnOutroStarted msg) {
        Debug.Log("Battle All Finished");
        BattleFinishText.text = $"Battle All Finished!";
        BattleFinishText.gameObject.SetActive(true);
        LeanTween.delayedCall(1f, () => {
            BattleFinishText.gameObject.SetActive(false);
        });
    }

}