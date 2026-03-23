using R3;
using Core.App.Presenters.Scene.Types;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

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

        private IBattleFlow battleFlow;
        private CompositeDisposable disposables = new();

        // Start is called once before the first execution of Update after the MonoBehaviour is created
        void Awake() {
            Debug.Log("BattleCanvas Awake");
            BattleFinishText.gameObject.SetActive(false);
            BattleStartText.gameObject.SetActive(false);
            RoundNumberText.gameObject.SetActive(false);

            // 暗転パネルを初期状態で透明にする
            if (fadePanel != null) {
                fadePanel.alpha = 0f;
                fadePanel.gameObject.SetActive(false);
            }
        }

        [Inject]
        public void Construct(IBattleFlow battleFlow) {
            this.battleFlow = battleFlow;
        }

        void Start() {
            battleFlow.RoundStarted
                .Subscribe(OnRoundStarted)
                .AddTo(disposables);

            battleFlow.BattleFinished
                .Subscribe(_ => OnBattleFinished())
                .AddTo(disposables);

            battleFlow.OutroStarted
                .Subscribe(_ => OnOutroStarted())
                .AddTo(disposables);
        }

        void OnDestroy() {
            disposables.Dispose();
        }

        // Update is called once per frame
        void Update() {

        }

        void OnRoundStarted(int roundNumber) {
            Debug.Log("Round Started Animation");

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
                battleFlow.NotifyRoundStartAnimationFinished();
            });
        }

            void OnBattleFinished() {
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
                battleFlow.NotifyRoundFinishAnimationFinished();
                return;
            }

            fadePanel.gameObject.SetActive(true);
            fadePanel.alpha = 0f;

            // フェードイン（暗転）
            LeanTween.alphaCanvas(fadePanel, 1f, fadePanelDuration)
                .setEase(LeanTweenType.easeInOutQuad)
                .setOnComplete(() => {
                    // 完全に暗くなったタイミングで次のRound準備を開始
                        battleFlow.NotifyRoundFinishAnimationFinished();

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

        void OnOutroStarted() {
            Debug.Log("Battle All Finished");
            BattleFinishText.text = $"Game Set";
            BattleFinishText.gameObject.SetActive(true);

            // Game Set効果音再生
            PlaySound(finishSound);

            LeanTween.delayedCall(1f, () => {
                BattleFinishText.gameObject.SetActive(false);
            });
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

    }
}