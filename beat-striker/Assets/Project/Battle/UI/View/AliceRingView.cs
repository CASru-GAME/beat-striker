using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Alice {
    public class AliceRingView : MonoBehaviour {
        enum VisualMode {
            Hidden,
            Active,
        }

        const int UNASSIGNED_PLAYER_ID = -1;

        [SerializeField] Image[] centerRing;
        [SerializeField] Image[] rings;
        [SerializeField] TextMeshProUGUI judgeTextPrefab;
        [SerializeField] RectTransform judgeTextForwardReference;
        [SerializeField] float ringRadiusPerSecond = 1f;
        [SerializeField] float windowScale = 3f;
        [SerializeField] float judgeTextFadeDuration = 0.6f;
        [SerializeField] float judgeTextDropDistance = 48f;
        [SerializeField] float beatPulseScale = 1.1f;
        [SerializeField] float beatPulseExpandDuration = 0.06f;
        [SerializeField] float beatPulseShrinkDuration = 0.1f;
        [SerializeField] AudioClip successSound, excellentSound, missSound;
        [SerializeField] Color[] colors;

        int playerId = UNASSIGNED_PLAYER_ID;
        float ringFirstAlpha;
        float centerRingFirstAlpha;
        bool battleViewActive;
        float[] beatTimeline = Array.Empty<float>();
        float currentViewPlaybackTime;
        Vector3 currentWorldPosition;
        Vector3 currentLookDirection = Vector3.right;
        bool hasWorldPosition;
        Vector2 judgeTextForwardAnchoredPosition;
        float judgeTextForwardSignedZ;
        Vector3 initialLocalScale;
        Vector3[] centerRingInitialLocalScales = Array.Empty<Vector3>();
        Vector3[] ringInitialLocalScales = Array.Empty<Vector3>();
        float currentPulseScale = 1f;
        bool playerVisualReady;
        VisualMode visualMode = VisualMode.Hidden;
        readonly List<TextMeshProUGUI> activeJudgeTexts = new();

        public void NotifyBeatPassed() {
            PlayBeatPulse();
        }

        void Awake() {
            CacheInitialState();
            judgeTextForwardAnchoredPosition = judgeTextForwardReference.anchoredPosition;
            judgeTextForwardSignedZ = Mathf.DeltaAngle(0f, judgeTextForwardReference.localEulerAngles.z);
            judgeTextPrefab.gameObject.SetActive(false);
            DeactivateInternal();
        }

        void OnEnable() {
            visualMode = VisualMode.Hidden;
            HideVisualImmediate();

            if (battleViewActive && playerVisualReady) {
                RefreshVisualMode();
            }
        }

        void OnDisable() {
            DeactivateInternal();
        }

        public void ActivateBattleView(int playerId) {
            if (playerId < 0) {
                DeactivateInternal();
                return;
            }

            this.playerId = playerId;
            ApplyPlayerColors();
            playerVisualReady = true;
            battleViewActive = true;
            RefreshVisualMode();
        }

        public void DeactivateBattleView() {
            DeactivateInternal();
        }

        void DeactivateInternal() {
            battleViewActive = false;
            playerVisualReady = false;
            playerId = UNASSIGNED_PLAYER_ID;
            hasWorldPosition = false;
            LeanTween.cancel(gameObject);
            transform.localScale = initialLocalScale;
            currentPulseScale = 1f;
            ClearActiveJudgeTexts();
            visualMode = VisualMode.Hidden;
            HideVisualImmediate();
        }

        public void SetBeatTimeline(float[] beats) {
            beatTimeline = beats ?? Array.Empty<float>();
            if (battleViewActive) {
                RefreshVisualMode();
            }
        }

        public void SetViewPlaybackTime(float playbackTime) {
            currentViewPlaybackTime = playbackTime;
        }

        public void SetPosition(Vector3 worldPosition) {
            currentWorldPosition = worldPosition;
            hasWorldPosition = true;

            if (battleViewActive && playerVisualReady) {
                var cam = Camera.main;
                if (cam != null) {
                    transform.position = cam.WorldToScreenPoint(currentWorldPosition);
                }
            }
        }

        public void SetLookDirection(Vector3 lookDirection) {
            if (lookDirection.sqrMagnitude <= 0f) return;
            currentLookDirection = lookDirection;
        }

        void CacheInitialState() {
            initialLocalScale = transform.localScale;

            centerRingInitialLocalScales = new Vector3[centerRing.Length];
            for (var i = 0; i < centerRing.Length; i++) {
                centerRingInitialLocalScales[i] = centerRing[i].transform.localScale;
            }

            ringInitialLocalScales = new Vector3[rings.Length];
            for (var i = 0; i < rings.Length; i++) {
                ringInitialLocalScales[i] = rings[i].transform.localScale;
            }

            ringFirstAlpha = rings[0].color.a;
            centerRingFirstAlpha = centerRing[0].color.a;
        }

        Color GetPlayerColor() {
            return colors[playerId % colors.Length];
        }

        void ApplyPlayerColors() {
            var playerColor = GetPlayerColor();

            for (var i = 0; i < centerRing.Length; i++) {
                var centerColor = playerColor;
                centerColor.a = centerRingFirstAlpha;
                centerRing[i].color = centerColor;
            }

            for (var i = 0; i < rings.Length; i++) {
                var ringColor = playerColor;
                ringColor.a = ringFirstAlpha;
                rings[i].color = ringColor;
            }
        }

        void SetBattleVisualActive(bool active) {
            for (var i = 0; i < centerRing.Length; i++) {
                centerRing[i].gameObject.SetActive(active);
            }

            for (var i = 0; i < rings.Length; i++) {
                rings[i].gameObject.SetActive(active);
            }
        }

        void SetImageAlpha(Image image, float alpha) {
            var color = image.color;
            color.a = alpha;
            image.color = color;
        }

        bool IsSameRgb(Color a, Color b) {
            return Mathf.Abs(a.r - b.r) < 0.001f
                && Mathf.Abs(a.g - b.g) < 0.001f
                && Mathf.Abs(a.b - b.b) < 0.001f;
        }

        void EnsurePlayerColorRgb() {
            var expected = GetPlayerColor();

            for (var i = 0; i < centerRing.Length; i++) {
                var current = centerRing[i].color;
                if (IsSameRgb(current, expected)) continue;

                current.r = expected.r;
                current.g = expected.g;
                current.b = expected.b;
                centerRing[i].color = current;
            }

            for (var i = 0; i < rings.Length; i++) {
                var current = rings[i].color;
                if (IsSameRgb(current, expected)) continue;

                current.r = expected.r;
                current.g = expected.g;
                current.b = expected.b;
                rings[i].color = current;
            }
        }

        void SetVisualMode(VisualMode mode) {
            if (visualMode == mode && mode == VisualMode.Active) return;

            visualMode = mode;
            if (mode == VisualMode.Hidden) {
                HideVisualImmediate();
                return;
            }

            RestoreVisualAfterBeats();
            SetBattleVisualActive(true);
        }

        void HideVisualImmediate() {
            for (var i = 0; i < centerRing.Length; i++) {
                SetImageAlpha(centerRing[i], 0f);
            }

            for (var i = 0; i < rings.Length; i++) {
                SetImageAlpha(rings[i], 0f);
            }

            SetBattleVisualActive(false);
        }

        void RestoreVisualAfterBeats() {
            var playerColor = GetPlayerColor();

            for (var i = 0; i < centerRing.Length; i++) {
                var color = playerColor;
                color.a = centerRingFirstAlpha;
                centerRing[i].color = color;
            }

            for (var i = 0; i < rings.Length; i++) {
                var color = rings[i].color;
                color.r = playerColor.r;
                color.g = playerColor.g;
                color.b = playerColor.b;
                rings[i].color = color;
            }
        }

        void ClearActiveJudgeTexts() {
            for (var i = activeJudgeTexts.Count - 1; i >= 0; i--) {
                var activeText = activeJudgeTexts[i];
                LeanTween.cancel(activeText.gameObject);
                Destroy(activeText.gameObject);
            }

            activeJudgeTexts.Clear();
        }

        bool TryGetFirstRenderableBeatIndex(float now, out int firstUpcoming) {
            firstUpcoming = -1;
            if (!playerVisualReady) return false;
            if (!hasWorldPosition) return false;
            if (beatTimeline.Length == 0) return false;

            firstUpcoming = GetFirstUpcomingBeatIndex(beatTimeline, now);
            return firstUpcoming >= 0;
        }

        bool RefreshVisualMode() {
            if (!battleViewActive) {
                SetVisualMode(VisualMode.Hidden);
                return false;
            }

            var hasRenderableBeat = TryGetFirstRenderableBeatIndex(currentViewPlaybackTime, out _);
            SetVisualMode(hasRenderableBeat ? VisualMode.Active : VisualMode.Hidden);
            return hasRenderableBeat;
        }

        void PlayJudgeSound(BeatJudgeZone zone) {
            if (zone == BeatJudgeZone.Excellent) {
                PlayClipIfAvailable(excellentSound, successSound);
                return;
            }

            if (zone == BeatJudgeZone.Good) {
                PlayClipIfAvailable(successSound, excellentSound);
                return;
            }

            PlayClipIfAvailable(missSound);
        }

        void PlayClipIfAvailable(params AudioClip[] clips) {
            for (var i = 0; i < clips.Length; i++) {
                var clip = clips[i];
                if (clip == null) continue;

                AudioSource.PlayClipAtPoint(clip, Vector3.zero);
                return;
            }
        }

        void FlashCenterRing() {
            for (var i = 0; i < centerRing.Length; i++) {
                var color = centerRing[i].color;
                color.a = 1f;
                centerRing[i].color = color;
                LeanTween.alpha(centerRing[i].rectTransform, centerRingFirstAlpha, 0.3f);
            }
        }

        public void NotifyBeatRequested(BeatJudgeZone zone) {
            if (!battleViewActive) return;
            if (!playerVisualReady) return;
            if (playerId < 0) return;

            PlayJudgeSound(zone);
            FlashCenterRing();
            SpawnJudgeText(ToJudgeLabel(zone));
        }

        string ToJudgeLabel(BeatJudgeZone zone) {
            if (zone == BeatJudgeZone.Excellent) {
                return "excellent";
            }

            if (zone == BeatJudgeZone.Good) {
                return "good";
            }

            return "miss";
        }

        void SpawnJudgeText(string label) {
            var instance = Instantiate(judgeTextPrefab, transform, false);
            activeJudgeTexts.Add(instance);

            instance.gameObject.SetActive(true);
            instance.text = label;

            var judgeColor = GetPlayerColor();
            judgeColor.a = 1f;
            instance.color = judgeColor;

            var cam = Camera.main;
            var mirrored = cam != null
                ? Vector3.Dot(currentLookDirection, cam.transform.right) < 0f
                : currentLookDirection.x < 0f;
            var startAnchoredPosition = mirrored
                ? new Vector2(-judgeTextForwardAnchoredPosition.x, judgeTextForwardAnchoredPosition.y)
                : judgeTextForwardAnchoredPosition;
            instance.rectTransform.anchoredPosition = startAnchoredPosition;

            instance.rectTransform.localScale = Vector3.one;
            var startZ = mirrored ? -judgeTextForwardSignedZ : judgeTextForwardSignedZ;
            instance.rectTransform.localRotation = Quaternion.Euler(0f, 0f, startZ);

            var targetAnchoredPosition = startAnchoredPosition + Vector2.down * judgeTextDropDistance;
            LeanTween.value(instance.gameObject, startAnchoredPosition, targetAnchoredPosition, judgeTextFadeDuration)
                .setEase(LeanTweenType.easeInSine)
                .setOnUpdate((Vector2 position) => {
                    instance.rectTransform.anchoredPosition = position;
                });

            LeanTween.value(instance.gameObject, 1f, 0f, judgeTextFadeDuration)
                .setOnUpdate((float alpha) => {
                    var currentColor = instance.color;
                    currentColor.a = alpha;
                    instance.color = currentColor;
                })
                .setOnComplete(() => {
                    activeJudgeTexts.Remove(instance);
                    Destroy(instance.gameObject);
                });
        }

        void Update() {
            if (!battleViewActive || !playerVisualReady || playerId < 0) {
                visualMode = VisualMode.Hidden;
                HideVisualImmediate();
                return;
            }

            var now = currentViewPlaybackTime;
            if (!TryGetFirstRenderableBeatIndex(now, out var firstUpcoming)) {
                SetVisualMode(VisualMode.Hidden);
                return;
            }

            SetVisualMode(VisualMode.Active);
            EnsurePlayerColorRgb();

            var screenPos = Camera.main.WorldToScreenPoint(currentWorldPosition);
            transform.position = screenPos;

            for (var i = 0; i < centerRing.Length; i++) {
                centerRing[i].transform.localScale = centerRingInitialLocalScales[i] * currentPulseScale;
            }

            for (var i = 0; i < rings.Length; i++) {
                var targetIndex = firstUpcoming + i;
                if (targetIndex < 0 || targetIndex >= beatTimeline.Length) {
                    SetImageAlpha(rings[i], 0f);
                    continue;
                }

                var nextBeatTime = beatTimeline[targetIndex];
                if (float.IsNaN(nextBeatTime)) {
                    SetImageAlpha(rings[i], 0f);
                    continue;
                }

                var timeSpan = nextBeatTime - now;
                if (timeSpan < 0f) timeSpan = 0f;

                var scale = ringRadiusPerSecond * timeSpan + 1f;
                rings[i].transform.localScale = ringInitialLocalScales[i] * (scale * currentPulseScale);

                var alpha = ringFirstAlpha * Mathf.Clamp01(windowScale - scale);
                SetImageAlpha(rings[i], alpha);
            }
        }

        int GetFirstUpcomingBeatIndex(float[] beats, float now) {
            for (var i = 0; i < beats.Length; i++) {
                if (beats[i] >= now) {
                    return i;
                }
            }

            return -1;
        }

        void PlayBeatPulse() {
            if (!battleViewActive) return;

            LeanTween.cancel(gameObject);
            currentPulseScale = 1f;

            LeanTween.value(gameObject, 1f, beatPulseScale, beatPulseExpandDuration)
                .setEase(LeanTweenType.easeOutQuad)
                .setOnUpdate((float scale) => {
                    currentPulseScale = scale;
                })
                .setOnComplete(() => {
                    LeanTween.value(gameObject, currentPulseScale, 1f, beatPulseShrinkDuration)
                        .setEase(LeanTweenType.easeInQuad)
                        .setOnUpdate((float scale) => {
                            currentPulseScale = scale;
                        });
                });
        }
    }
}
