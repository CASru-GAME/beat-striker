using System;
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
        [SerializeField] RectTransform judgeParticleForwardReference;
        [SerializeField] ParticleSystem excellentJudgeParticle;
        [SerializeField] ParticleSystem goodJudgeParticle;
        [SerializeField] ParticleSystem missJudgeParticle;
        [SerializeField] ParticleSystem excellentJudgeParticlePlain;
        [SerializeField] ParticleSystem goodJudgeParticlePlain;
        [SerializeField] ParticleSystem missJudgeParticlePlain;
        [SerializeField] float ringRadiusPerSecond = 1f;
        [SerializeField] float windowScale = 3f;
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
        Vector2 judgeParticleForwardAnchoredPosition;
        float judgeParticleForwardSignedZ;
        Vector3 initialLocalScale;
        Vector3[] centerRingInitialLocalScales = Array.Empty<Vector3>();
        Vector3[] ringInitialLocalScales = Array.Empty<Vector3>();
        bool playerVisualReady;
        VisualMode visualMode = VisualMode.Hidden;

        public void NotifyBeatPassed() {
        }

        void Awake() {
            CacheInitialState();
            if (judgeParticleForwardReference != null) {
                judgeParticleForwardAnchoredPosition = judgeParticleForwardReference.anchoredPosition;
                judgeParticleForwardSignedZ = Mathf.DeltaAngle(0f, judgeParticleForwardReference.localEulerAngles.z);
            }
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
            StopJudgeParticles();
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

        void StopJudgeParticles() {
            StopJudgeParticle(excellentJudgeParticle);
            StopJudgeParticle(goodJudgeParticle);
            StopJudgeParticle(missJudgeParticle);
            StopJudgeParticle(excellentJudgeParticlePlain);
            StopJudgeParticle(goodJudgeParticlePlain);
            StopJudgeParticle(missJudgeParticlePlain);
        }

        void StopJudgeParticle(ParticleSystem particle) {
            if (particle == null) return;
            particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
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

            FlashCenterRing();
            PlayJudgeParticle(zone);
        }

        void PlayJudgeParticle(BeatJudgeZone zone) {
            var mirroredParticle = zone switch {
                BeatJudgeZone.Excellent => excellentJudgeParticle,
                BeatJudgeZone.Good => goodJudgeParticle,
                _ => missJudgeParticle,
            };
            var plainParticle = zone switch {
                BeatJudgeZone.Excellent => excellentJudgeParticlePlain,
                BeatJudgeZone.Good => goodJudgeParticlePlain,
                _ => missJudgeParticlePlain,
            };

            PlayMirroredJudgeParticle(mirroredParticle);
            PlayPlainJudgeParticle(plainParticle);
        }

        void PlayMirroredJudgeParticle(ParticleSystem particle) {
            if (particle == null) return;
            ApplyJudgeParticleTransform(particle);
            particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            particle.Play(true);
        }

        void PlayPlainJudgeParticle(ParticleSystem particle) {
            if (particle == null) return;
            particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            particle.Play(true);
        }

        void ApplyJudgeParticleTransform(ParticleSystem particle) {
            var particleRect = particle.transform as RectTransform;
            if (particleRect == null) return;

            var baseAnchoredPosition = judgeParticleForwardReference != null
                ? judgeParticleForwardAnchoredPosition
                : particleRect.anchoredPosition;
            var baseSignedZ = judgeParticleForwardReference != null
                ? judgeParticleForwardSignedZ
                : Mathf.DeltaAngle(0f, particleRect.localEulerAngles.z);

            var cam = Camera.main;
            var mirrored = cam != null
                ? Vector3.Dot(currentLookDirection, cam.transform.right) < 0f
                : currentLookDirection.x < 0f;

            var anchoredPosition = mirrored
                ? new Vector2(-baseAnchoredPosition.x, baseAnchoredPosition.y)
                : baseAnchoredPosition;
            particleRect.anchoredPosition = anchoredPosition;

            var z = mirrored ? -baseSignedZ : baseSignedZ;
            particleRect.localRotation = Quaternion.Euler(0f, 0f, z);
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

            var cam = Camera.main;
            if (cam == null) return;
            var screenPos = cam.WorldToScreenPoint(currentWorldPosition);
            transform.position = screenPos;

            for (var i = 0; i < centerRing.Length; i++) {
                centerRing[i].transform.localScale = centerRingInitialLocalScales[i];
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
                rings[i].transform.localScale = ringInitialLocalScales[i] * scale;

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

    }
}
