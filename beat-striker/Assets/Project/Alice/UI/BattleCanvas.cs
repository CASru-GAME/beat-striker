using System;
using Core.App.Presenters.Scene.Types;
using Core.App.Installers;
using Core.App.Types;
using Core.Battle;
using Core.GamePad.Types;
using Core.Utils;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Alice {
    public class BattleCanvas : MonoBehaviour {
        [SerializeField] TextMeshProUGUI BattleStartText;
        [SerializeField] TextMeshProUGUI BattleFinishText;
        [SerializeField] TextMeshProUGUI RoundNumberText; // 新しいテキスト（Round 1など）
        [SerializeField] CanvasGroup RoundNumberCanvasGroup; // RoundNumberTextのCanvasGroup
        [SerializeField] CanvasGroup fadePanel; // 暗転用のパネル

        [Header("Sound Effects")]
        [SerializeField] AudioClip roundSound; // Round表示時の効果音
        [SerializeField] AudioClip fightSound; // Fight表示時の効果音
        [SerializeField] AudioClip finishSound; // Finish表示時の効果音
        [SerializeField] float soundVolume = 1.0f; // 効果音の音量

        [Header("Animation Timing")]
        [SerializeField] float roundFadeInDuration = 0.5f; // Roundのフェードイン時間
        [SerializeField] float roundDisplayDuration = 0.5f; // Roundの表示時間
        [SerializeField] float roundFadeOutDuration = 0.5f; // Roundのフェードアウト時間
        [SerializeField] float delayBeforeFight = 0.2f; // RoundとFightの間隔
        [SerializeField] float fightScaleDuration = 0.2f; // Fightの縮小アニメーション時間
        [SerializeField] float fightDisplayDuration = 0.7f; // Fightの表示時間
        [SerializeField] float fadePanelDuration = 0.5f; // 暗転のフェード時間
        [SerializeField] float fadeHoldDuration = 0.5f; // 暗転を保持する時間
        [SerializeField] float delayBeforeRoundText = 0.5f; // 明転後、Roundテキスト表示までの待機時間

        private IBus bus;

        [SerializeField] GameObject resultPrefab;

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
            bus.Subscribe<BattleMessages.OnResultStarted>(OnResultStarted);
        }

        void Start() {
            // BattleゲームオブジェクトからBattleInstallerを取得
            var battleObject = GameObject.Find("Battle");
            if (battleObject != null) {
                var battleInstaller = battleObject.GetComponent<BattleInstaller>();
                if (battleInstaller != null) {
                    Debug.Log("[BattleCanvas] BattleInstaller found, getting battleModel");
                    SetBattleModel(battleInstaller.battleModel);
                }
                else {
                    Debug.LogError("[BattleCanvas] BattleInstaller component not found on Battle GameObject!");
                }
            }
            else {
                Debug.LogError("[BattleCanvas] Battle GameObject not found in scene!");
            }
        }

        void OnDestroy() {
            bus.Unsubscribe<BattleMessages.OnOutroStarted>(OnOutroStarted);
            bus.Unsubscribe<BattleMessages.OnBattleFinished>(OnBattleFinished);
            bus.Unsubscribe<BattleMessages.OnRoundStarted>(OnRoundStarted);
            bus.Unsubscribe<BattleMessages.OnResultStarted>(OnResultStarted);
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

            // Round効果音再生
            PlaySound(roundSound);

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
            }
            else {
                Debug.LogError("RoundNumberCanvasGroup is not assigned!");
                ShowFightText();
            }
        }

        void ShowFightText() {
            // フェーズ2: 「Fight!」を拡大縮小アニメーション
            BattleStartText.text = "Fight!";
            BattleStartText.gameObject.SetActive(true);

            // Fight効果音再生
            PlaySound(fightSound);

            // 初期状態：画面いっぱいのサイズ
            BattleStartText.transform.localScale = Vector3.one * 10f;

            // アニメーション：通常サイズに縮小
            LeanTween.scale(BattleStartText.gameObject, Vector3.one, fightScaleDuration)
                .setEase(LeanTweenType.easeOutQuad);

            // 指定時間後に消える
            LeanTween.delayedCall(fightDisplayDuration, () => {
                BattleStartText.gameObject.SetActive(false);
                bus.Publish(new BattleMessages.NotifyRoundStartAnimationFinished());
            });
        }

        void OnBattleFinished(BattleMessages.OnBattleFinished msg) {
            Debug.Log("Battle Finished");

            BattleFinishText.gameObject.SetActive(true);

            // Finish効果音再生
            PlaySound(finishSound);

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
            BattleFinishText.text = $"Game Set";
            BattleFinishText.gameObject.SetActive(true);

            // Game Set効果音再生
            PlaySound(finishSound);

            LeanTween.delayedCall(1f, () => {
                BattleFinishText.gameObject.SetActive(false);
            });
        }

        void OnResultStarted(BattleMessages.OnResultStarted msg) {
            Debug.Log("Result Started");

            // Winテキストを消す（LeanTweenもキャンセル）
            if (BattleFinishText != null) {
                LeanTween.cancel(BattleFinishText.gameObject);
                BattleFinishText.gameObject.SetActive(false);
                Debug.Log("Win text hidden");
            }

            GameObject resultInstance = Instantiate(resultPrefab);
            if (resultInstance != null) {
                resultInstance.SetActive(true);

                // Canvas の Sort Order を高く設定して、他のUIより前面に表示
                Canvas resultCanvas = resultInstance.GetComponent<Canvas>();
                if (resultCanvas != null) {
                    resultCanvas.sortingOrder = 100;
                    Debug.Log($"Result Canvas sortingOrder set to 100");
                }

                // すべての子オブジェクトを再帰的にアクティブにする
                SetActiveRecursively(resultInstance.transform, true);

                Debug.Log($"Result prefab instantiated and activated: {resultInstance.name}");
            }
            else {
                Debug.LogError("Failed to instantiate result prefab!");
            }
        }

        void SetActiveRecursively(Transform parent, bool active) {
            foreach (Transform child in parent) {
                child.gameObject.SetActive(active);
                Debug.Log($"Setting {child.name} active: {active}");
                // 再帰的に子の子もアクティブにする
                SetActiveRecursively(child, active);
            }
        }

        void PlaySound(AudioClip clip) {
            if (clip == null) return;

            GameObject soundObject = new GameObject("TempSound");
            AudioSource audioSource = soundObject.AddComponent<AudioSource>();
            audioSource.clip = clip;
            audioSource.volume = soundVolume;
            audioSource.Play();

            Destroy(soundObject, clip.length);
        }

        private IBattleModel battleModel;

        void SetBattleModel(IBattleModel model) {
            this.battleModel = model;
        }

    }
}