using System.Threading.Tasks;
using System.Linq;
using System;
using UnityEngine;
using TMPro;

namespace Alice {
    public class BattlePresenterView : MonoBehaviour {
        [Header("References")]
        [SerializeField] StageCamera stageCamera;
        [SerializeField] BattleRoundStartView roundStartPresenter;
        [SerializeField] BattleFadeView fadePresenter;
        [SerializeField] BattleResultTextView resultTextPresenter;
        [SerializeField] AttentionTextView attentionTextView;
        [SerializeField] BattleSuspendMenuView suspendMenuPresenter;

        [Header("Opening")]
        [SerializeField] AudioClip beatSound;
        [SerializeField] TMP_Text remainingBeatCountText;
        [SerializeField] float remainingBeatCountUpdateDelaySeconds;
        [SerializeField] RectTransform[] battleUiSlideTargets;
        [SerializeField] float battleUiSlideDuration = 0.2f;
        [SerializeField] float battleUiHiddenTopMargin = 80f;
        [SerializeField] float openingAfterSlideDelaySeconds = 0.3f;

        [Header("Round Transition")]
        [SerializeField] float roundEndBeforeFadeInDelaySeconds = 0.2f;

        [Header("Beat Judge Audio")]
        [SerializeField] AudioClip judgeSuccessSound;
        [SerializeField] AudioClip judgeMissSound;
        [SerializeField] AudioClip roundWinTrophySound;

        [Header("Round Win Trophy")]
        [Tooltip("ラウンド勝利トロフィーを生成する親RectTransform。Canvas配下を指定し、表示順や座標系を統一することで中央演出と左右整列の配置ズレを防ぎます。")]
        [SerializeField] RectTransform roundWinTrophyRoot;
        [Tooltip("勝利時に複製して使うトロフィーUIプレハブ。RectTransform基準の見た目を作り込み、中央表示時と縮小後の双方で破綻しないサイズを設定してください。")]
        [SerializeField] RectTransform roundWinTrophyPrefab;
        [Tooltip("トロフィー演出の初期表示位置となる中央アンカー。勝利直後に大きく表示される起点で、試合進行中の他UIと重なり過ぎない位置に調整します。")]
        [SerializeField] RectTransform roundWinTrophyCenterAnchor;
        [Tooltip("1P側トロフィー整列の開始位置。ここを先頭として勝利数に応じて右方向へ並ぶため、左下UIとの余白を確保しつつ基準点を配置してください。")]
        [SerializeField] RectTransform roundWinTrophyPlayer1StartAnchor;
        [Tooltip("2P側トロフィー整列の開始位置。ここを先頭として勝利数に応じて左方向へ並ぶため、右下UIとの干渉を避けて基準点を設定してください。")]
        [SerializeField] RectTransform roundWinTrophyPlayer2StartAnchor;
        [Tooltip("中央で強調表示する際のトロフィー拡大率。勝利演出の視認性を決める値で、大き過ぎると画面占有が増えるため他UIとのバランスで調整します。")]
        [SerializeField] float roundWinTrophyCenterScale = 2.2f;
        [Tooltip("左右下へ着地した後のトロフィー縮小率。勝利数を複数並べた際の密度や見やすさに影響するため、間隔値と合わせて最終サイズを整えてください。")]
        [SerializeField] float roundWinTrophyStackScale = 0.8f;
        [Tooltip("トロフィー出現時に0スケールから中央スケールへ拡大する時間（秒）。短いと勢いが出て長いと重厚感が増すため、演出テンポに合わせて調整します。")]
        [SerializeField] float roundWinTrophySpawnDurationSeconds = 0.28f;
        [Tooltip("出現時にY軸で何周回転させるかを指定する値。整数で設定し、0なら回転なし、1以上で立体感のあるスピン演出を追加できます。")]
        [SerializeField] int roundWinTrophySpawnSpinCount = 2;
        [Tooltip("中央で大きく表示したまま待機する時間（秒）。勝利の手応えを感じる演出尺で、短いと印象が弱まり長いと試合テンポが落ちるため適切に調整します。")]
        [SerializeField] float roundWinTrophyCenterHoldSeconds = 0.35f;
        [Tooltip("中央から左右下の整列位置へ移動しつつ縮小する演出時間（秒）。ラウンド遷移と並行進行するため、テンポと視認性の中間点に設定してください。")]
        [SerializeField] float roundWinTrophyMoveDurationSeconds = 0.3f;
        [Tooltip("整列時に隣り合うトロフィー同士の水平間隔。勝利数増加時の可読性を左右し、狭すぎる重なりや広すぎるはみ出しを防ぐ基準値になります。")]
        [SerializeField] float roundWinTrophyStackSpacing = 100f;
        [Tooltip("バトル終了時にトロフィー全体を画面外下へ退場させる時間（秒）。他UIのフェードやスライドと調和するように、少し短めの値を基準に調整します。")]
        [SerializeField] float roundWinTrophyExitDurationSeconds = 0.32f;
        [Tooltip("画面外下へ隠す際の余白量。大きいほど完全に見切れやすくなり、解像度差がある環境でもトロフィー残りを防ぎやすくなります。")]
        [SerializeField] float roundWinTrophyHiddenBottomMargin = 80f;
        [Tooltip("整列後トロフィーがビート時に一瞬拡大する倍率。1.0で無効、1.05〜1.12程度で控えめな脈動になります。")]
        [SerializeField] float roundWinTrophyBeatExpandScaleMultiplier = 1.1f;
        [Tooltip("ビート時に拡大する時間（秒）。短いほどキレのある脈動になります。")]
        [SerializeField] float roundWinTrophyBeatExpandDuration = 0f;
        [Tooltip("ビート時に元スケールへ戻る時間（秒）。拡大より少し長めにすると自然な戻りになります。")]
        [SerializeField] float roundWinTrophyBeatReturnDuration = 0.08f;

        Vector2[] battleUiDefaultAnchoredPositions;
        float[] battleUiHiddenTopAnchoredYs;
        TaskCompletionSource<bool> uiSlideCompletionSource;
        int remainingBeatCountDelayTweenId = -1;
        readonly System.Collections.Generic.List<RectTransform> roundWinTrophyViews = new();
        readonly System.Collections.Generic.HashSet<RectTransform> roundWinTrophyStackedViews = new();
        public event Action OnViewBeatTiming;

        public StageCamera StageCamera => stageCamera;
        public BattleRoundStartView RoundStartPresenter => roundStartPresenter;
        public BattleResultTextView ResultTextPresenter => resultTextPresenter;
        public AttentionTextView AttentionTextView => attentionTextView;
        public BattleFadeView FadePresenter => fadePresenter;
        public BattleSuspendMenuView SuspendMenuPresenter => suspendMenuPresenter;
        public AudioClip BeatSound => beatSound;

        void Awake() {
            CacheBattleUiLayout();
        }

        public void SetBattleUiHiddenAboveImmediately() {
            CacheBattleUiLayout();
            LeanTween.cancel(gameObject);

            for (var i = 0; i < battleUiSlideTargets.Length; i++) {
                battleUiSlideTargets[i].anchoredPosition = new Vector2(battleUiDefaultAnchoredPositions[i].x, battleUiHiddenTopAnchoredYs[i]);
            }
        }

        public Task SlideBattleUiInAsync() {
            return SlideBattleUiAsync(useHiddenTarget: false);
        }

        public Task SlideBattleUiOutAsync() {
            return SlideBattleUiAsync(useHiddenTarget: true);
        }

        public Task WaitAfterSlideBattleUiInAsync() {
            return DelayAsync(openingAfterSlideDelaySeconds);
        }

        public Task WaitBeforeRoundEndFadeInAsync() {
            return DelayAsync(roundEndBeforeFadeInDelaySeconds);
        }

        public void RequestViewBeatPulse() {
            stageCamera.RequestViewBeatPulse();
            PlayRoundWinTrophyBeatExpand();
            OnViewBeatTiming?.Invoke();
        }

        public void PlayJudgeSuccessSound() {
            if (judgeSuccessSound) {
                judgeSuccessSound.PlayAtApp(Vector3.zero);
            }
        }

        public void PlayJudgeMissSound() {
            if (judgeMissSound) {
                judgeMissSound.PlayAtApp(Vector3.zero);
            }
        }

        public void PlayRoundWinTrophySound() {
            if (roundWinTrophySound) {
                roundWinTrophySound.PlayAtApp(Vector3.zero);
            }
        }

        public void SetRemainingBeatCount(int remainingBeatCount) {
            if (!remainingBeatCountText) {
                return;
            }

            if (remainingBeatCountDelayTweenId >= 0) {
                LeanTween.cancel(remainingBeatCountDelayTweenId);
                remainingBeatCountDelayTweenId = -1;
            }

            var safeRemainingBeatCount = Mathf.Max(0, remainingBeatCount);
            var delaySeconds = Mathf.Max(0f, remainingBeatCountUpdateDelaySeconds);
            if (delaySeconds <= 0f) {
                remainingBeatCountText.text = safeRemainingBeatCount.ToString();
                return;
            }

            remainingBeatCountDelayTweenId = LeanTween.delayedCall(gameObject, delaySeconds, () => {
                    remainingBeatCountText.text = safeRemainingBeatCount.ToString();
                    remainingBeatCountDelayTweenId = -1;
                })
                .id;
        }

        public void SetRemainingBeatCountVs() {
            if (!remainingBeatCountText) {
                return;
            }

            if (remainingBeatCountDelayTweenId >= 0) {
                LeanTween.cancel(remainingBeatCountDelayTweenId);
                remainingBeatCountDelayTweenId = -1;
            }

            remainingBeatCountText.text = "VS";
        }

        public void ResetRoundWinTrophies() {
            foreach (var trophy in roundWinTrophyViews) {
                if (trophy == null) {
                    continue;
                }

                Destroy(trophy.gameObject);
            }

            roundWinTrophyViews.Clear();
            roundWinTrophyStackedViews.Clear();
        }

        public async Task PlayRoundWinTrophyAsync(int winnerPlayerId, int winnerRoundWinIndex) {
            var trophy = Instantiate(roundWinTrophyPrefab, roundWinTrophyRoot);
            roundWinTrophyViews.Add(trophy);
            trophy.anchoredPosition = roundWinTrophyCenterAnchor.anchoredPosition;
            trophy.localScale = Vector3.zero;
            trophy.localRotation = Quaternion.identity;
            trophy.SetAsLastSibling();
            await PlayRoundWinTrophySpawnAsync(trophy);

            await DelayAsync(roundWinTrophyCenterHoldSeconds);

            var targetPosition = ResolveRoundWinTrophyTargetPosition(winnerPlayerId, winnerRoundWinIndex);
            var targetScale = Vector3.one * roundWinTrophyStackScale;
            await TweenRectTransformAsync(trophy, targetPosition, targetScale, roundWinTrophyMoveDurationSeconds);
            roundWinTrophyStackedViews.Add(trophy);
        }

        public async Task HideRoundWinTrophiesToBottomAsync() {
            if (roundWinTrophyViews.Count == 0) {
                return;
            }

            var rootCanvas = roundWinTrophyRoot.GetComponentInParent<Canvas>().rootCanvas;
            var rootCanvasRect = (RectTransform)rootCanvas.transform;
            var hideY = -rootCanvasRect.rect.height - roundWinTrophyHiddenBottomMargin;
            var hideTasks = new Task[roundWinTrophyViews.Count];
            for (var i = 0; i < roundWinTrophyViews.Count; i++) {
                var trophy = roundWinTrophyViews[i];
                if (trophy == null) {
                    hideTasks[i] = Task.CompletedTask;
                    continue;
                }

                roundWinTrophyStackedViews.Remove(trophy);

                hideTasks[i] = TweenRectTransformAsync(
                    trophy,
                    new Vector2(trophy.anchoredPosition.x, hideY),
                    trophy.localScale,
                    roundWinTrophyExitDurationSeconds);
            }

            await Task.WhenAll(hideTasks);
        }

        Task SlideBattleUiAsync(bool useHiddenTarget) {
            CacheBattleUiLayout();
            uiSlideCompletionSource?.TrySetCanceled();
            uiSlideCompletionSource = new TaskCompletionSource<bool>();
            LeanTween.cancel(gameObject);

            if (battleUiSlideTargets.Length == 0) {
                uiSlideCompletionSource.TrySetResult(true);
                return uiSlideCompletionSource.Task;
            }

            var completedCount = 0;
            for (var i = 0; i < battleUiSlideTargets.Length; i++) {
                var index = i;
                var targetRect = battleUiSlideTargets[index];
                var startAnchoredY = targetRect.anchoredPosition.y;
                var targetAnchoredY = useHiddenTarget
                    ? battleUiHiddenTopAnchoredYs[index]
                    : battleUiDefaultAnchoredPositions[index].y;

                LeanTween.value(gameObject, startAnchoredY, targetAnchoredY, battleUiSlideDuration)
                    .setEase(LeanTweenType.easeInOutQuad)
                    .setOnUpdate(y => {
                        targetRect.anchoredPosition = new Vector2(battleUiDefaultAnchoredPositions[index].x, y);
                    })
                    .setOnComplete(() => {
                        completedCount += 1;
                        if (completedCount == battleUiSlideTargets.Length) {
                            uiSlideCompletionSource?.TrySetResult(true);
                        }
                    });
            }

            return uiSlideCompletionSource.Task;
        }

        void CacheBattleUiLayout() {
            if (battleUiDefaultAnchoredPositions != null) {
                return;
            }

            battleUiSlideTargets = battleUiSlideTargets.Where(x => x != null).ToArray();

            battleUiDefaultAnchoredPositions = new Vector2[battleUiSlideTargets.Length];
            battleUiHiddenTopAnchoredYs = new float[battleUiSlideTargets.Length];

            for (var i = 0; i < battleUiSlideTargets.Length; i++) {
                var target = battleUiSlideTargets[i];
                var rootCanvas = target.GetComponentInParent<Canvas>().rootCanvas;
                var rootCanvasRect = (RectTransform)rootCanvas.transform;
                var rootHeight = rootCanvasRect.rect.height;
                var defaultPosition = target.anchoredPosition;
                battleUiDefaultAnchoredPositions[i] = defaultPosition;
                battleUiHiddenTopAnchoredYs[i] = defaultPosition.y + rootHeight + target.rect.height + battleUiHiddenTopMargin;
            }
        }

        void PlayRoundWinTrophyBeatExpand() {
            if (roundWinTrophyStackedViews.Count == 0) {
                return;
            }

            var expandScaleMultiplier = Mathf.Max(1f, roundWinTrophyBeatExpandScaleMultiplier);
            var expandDuration = Mathf.Max(0.0001f, roundWinTrophyBeatExpandDuration);
            var returnDuration = Mathf.Max(0.0001f, roundWinTrophyBeatReturnDuration);
            var invalidViews = new System.Collections.Generic.List<RectTransform>();

            foreach (var trophy in roundWinTrophyStackedViews) {
                if (trophy == null) {
                    invalidViews.Add(trophy);
                    continue;
                }

                // 中央強調中(=整列前)や非整列状態のトロフィーはビート拡大対象から外す。
                if (!IsTrophyAtStackScale(trophy)) {
                    continue;
                }

                LeanTween.cancel(trophy.gameObject);
                var settledScale = trophy.localScale.sqrMagnitude > 0.000001f ? trophy.localScale : Vector3.one * roundWinTrophyStackScale;
                trophy.localScale = settledScale;
                var expandedScale = settledScale * expandScaleMultiplier;

                LeanTween.scale(trophy, expandedScale, expandDuration)
                    .setEase(LeanTweenType.easeOutQuad)
                    .setOnComplete(() => {
                        if (trophy == null) {
                            return;
                        }

                        LeanTween.scale(trophy, settledScale, returnDuration)
                            .setEase(LeanTweenType.easeInQuad);
                    });
            }

            foreach (var invalidView in invalidViews) {
                roundWinTrophyStackedViews.Remove(invalidView);
            }
        }

        bool IsTrophyAtStackScale(RectTransform trophy) {
            var expectedScale = Mathf.Max(0f, roundWinTrophyStackScale);
            var tolerance = 0.001f;
            return Mathf.Abs(trophy.localScale.x - expectedScale) <= tolerance
                && Mathf.Abs(trophy.localScale.y - expectedScale) <= tolerance
                && Mathf.Abs(trophy.localScale.z - expectedScale) <= tolerance;
        }

        static Task DelayAsync(float seconds) {
            if (seconds <= 0f) {
                return Task.CompletedTask;
            }

            var completionSource = new TaskCompletionSource<bool>();
            LeanTween.delayedCall(seconds, () => completionSource.TrySetResult(true));
            return completionSource.Task;
        }

        Vector2 ResolveRoundWinTrophyTargetPosition(int winnerPlayerId, int winnerRoundWinIndex) {
            var safeWinnerRoundWinIndex = Mathf.Max(0, winnerRoundWinIndex);
            if (winnerPlayerId == 0) {
                var offsetX = roundWinTrophyStackSpacing * safeWinnerRoundWinIndex;
                return roundWinTrophyPlayer1StartAnchor.anchoredPosition + new Vector2(offsetX, 0f);
            }

            if (winnerPlayerId == 1) {
                var offsetX = roundWinTrophyStackSpacing * safeWinnerRoundWinIndex;
                return roundWinTrophyPlayer2StartAnchor.anchoredPosition + new Vector2(-offsetX, 0f);
            }

            throw new ArgumentOutOfRangeException(nameof(winnerPlayerId), winnerPlayerId, "winnerPlayerId must be 0 or 1.");
        }

        Task TweenRectTransformAsync(RectTransform target, Vector2 toPosition, Vector3 toScale, float durationSeconds) {
            var completionSource = new TaskCompletionSource<bool>();
            var startPosition = target.anchoredPosition;
            var startScale = target.localScale;
            var safeDuration = Mathf.Max(0f, durationSeconds);
            if (safeDuration <= 0f) {
                target.anchoredPosition = toPosition;
                target.localScale = toScale;
                completionSource.TrySetResult(true);
                return completionSource.Task;
            }

            LeanTween.value(gameObject, 0f, 1f, safeDuration)
                .setEase(LeanTweenType.easeInOutQuad)
                .setOnUpdate(t => {
                    target.anchoredPosition = Vector2.LerpUnclamped(startPosition, toPosition, t);
                    target.localScale = Vector3.LerpUnclamped(startScale, toScale, t);
                })
                .setOnComplete(() => completionSource.TrySetResult(true));

            return completionSource.Task;
        }

        Task PlayRoundWinTrophySpawnAsync(RectTransform trophy) {
            var completionSource = new TaskCompletionSource<bool>();
            var safeDuration = Mathf.Max(0f, roundWinTrophySpawnDurationSeconds);
            if (safeDuration <= 0f) {
                trophy.localScale = Vector3.one * roundWinTrophyCenterScale;
                trophy.localRotation = Quaternion.identity;
                completionSource.TrySetResult(true);
                return completionSource.Task;
            }

            var endRotationY = Mathf.Max(0, roundWinTrophySpawnSpinCount) * 360f;
            LeanTween.value(gameObject, 0f, 1f, safeDuration)
                .setEase(LeanTweenType.easeOutBack)
                .setOnUpdate(t => {
                    var scale = Mathf.LerpUnclamped(0f, roundWinTrophyCenterScale, t);
                    trophy.localScale = Vector3.one * scale;
                    trophy.localRotation = Quaternion.Euler(0f, Mathf.LerpUnclamped(0f, endRotationY, t), 0f);
                })
                .setOnComplete(() => {
                    trophy.localScale = Vector3.one * roundWinTrophyCenterScale;
                    trophy.localRotation = Quaternion.identity;
                    completionSource.TrySetResult(true);
                });

            return completionSource.Task;
        }
    }
}
