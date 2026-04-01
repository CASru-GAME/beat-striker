using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Alice {
    public class AliceRingView : MonoBehaviour {
        [SerializeField] Image[] centerRing;
        [SerializeField] Image[] rings;
        [SerializeField] TextMeshProUGUI judgeTextPrefab;
        [SerializeField] RectTransform judgeTextForwardReference;
        [SerializeField] float ringRadiusPerSecond = 1f;
        [SerializeField] float windowScale = 3f;
        [SerializeField] float judgeTextFadeDuration = 0.6f;
        [SerializeField] float judgeTextDropDistance = 48f;
        [SerializeField] AudioClip successSound, missSound;

        float ringFirstAlpha;
        float centerRingFirstAlpha;
        bool battleViewActive;
        float[] beatTimeline = Array.Empty<float>();
        float currentViewPlaybackTime;
        Vector3 currentWorldPosition;
        Vector3 currentLookDirection = Vector3.right;
        Vector2 judgeTextForwardAnchoredPosition;
        float judgeTextForwardSignedZ;
        readonly List<TextMeshProUGUI> activeJudgeTexts = new();

        public void NotifyBeatPassed() {
            AudioSource.PlayClipAtPoint(missSound, Vector3.zero);

            var color = centerRing[0].color;
            color.a = 1f;
            centerRing[0].color = color;

            LeanTween.alpha(centerRing[0].rectTransform, centerRingFirstAlpha, 0.3f);

            SpawnJudgeText("pass");
        }

        void Awake() {
            centerRing[0].gameObject.SetActive(false);
            judgeTextForwardAnchoredPosition = judgeTextForwardReference.anchoredPosition;
            judgeTextForwardSignedZ = Mathf.DeltaAngle(0f, judgeTextForwardReference.localEulerAngles.z);
            judgeTextPrefab.gameObject.SetActive(false);
            foreach (var ring in rings) {
                ring.gameObject.SetActive(false);
            }
        }

        void Start() {
            ringFirstAlpha = rings[0].color.a;
            centerRingFirstAlpha = centerRing[0].color.a;
        }

        public void ActivateBattleView() {
            battleViewActive = true;
            centerRing[0].gameObject.SetActive(true);
            foreach (var ring in rings) {
                ring.gameObject.SetActive(true);
            }
        }

        public void DeactivateBattleView() {
            battleViewActive = false;
            for (var i = activeJudgeTexts.Count - 1; i >= 0; i--) {
                var activeText = activeJudgeTexts[i];
                LeanTween.cancel(activeText.gameObject);
                Destroy(activeText.gameObject);
            }
            activeJudgeTexts.Clear();
            centerRing[0].gameObject.SetActive(false);
            foreach (var ring in rings) {
                ring.gameObject.SetActive(false);
            }
        }

        public void SetBeatTimeline(float[] beats) {
            beatTimeline = beats ?? Array.Empty<float>();
        }

        public void SetViewPlaybackTime(float playbackTime) {
            currentViewPlaybackTime = playbackTime;
        }

        public void SetPosition(Vector3 worldPosition) {
            currentWorldPosition = worldPosition;
        }

        public void SetLookDirection(Vector3 lookDirection) {
            if (lookDirection.sqrMagnitude <= 0f) return;
            currentLookDirection = lookDirection;
        }

        public void NotifyBeatRequested(bool isSuccess) {
            AudioSource.PlayClipAtPoint(isSuccess ? successSound : missSound, Vector3.zero);

            var color = centerRing[0].color;
            color.a = 1f;
            centerRing[0].color = color;

            LeanTween.alpha(centerRing[0].rectTransform, centerRingFirstAlpha, 0.3f);

            SpawnJudgeText(isSuccess ? "good" : "miss");

            if (!isSuccess) return;
        }

        void SpawnJudgeText(string label) {
            var instance = Instantiate(judgeTextPrefab, transform, false);
            activeJudgeTexts.Add(instance);

            instance.gameObject.SetActive(true);
            instance.text = label;

            var judgeColor = instance.color;
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
            if (!battleViewActive) return;
            if (beatTimeline.Length == 0) return;

            var screenPos = Camera.main.WorldToScreenPoint(currentWorldPosition);
            transform.position = screenPos;

            var now = currentViewPlaybackTime;
            var firstUpcoming = GetFirstUpcomingBeatIndex(beatTimeline, now);

            for (var i = 0; i < rings.Length; i++) {
                if (firstUpcoming < 0) {
                    rings[i].gameObject.SetActive(false);
                    continue;
                }

                var targetIndex = firstUpcoming + i;
                if (targetIndex < 0 || targetIndex >= beatTimeline.Length) {
                    rings[i].gameObject.SetActive(false);
                    continue;
                }

                var nextBeatTime = beatTimeline[targetIndex];

                if (float.IsNaN(nextBeatTime)) {
                    rings[i].gameObject.SetActive(false);
                    continue;
                }

                rings[i].gameObject.SetActive(true);
                var timeSpan = nextBeatTime - now;
                if (timeSpan < 0f) timeSpan = 0f;

                var scale = ringRadiusPerSecond * timeSpan + 1f;
                rings[i].transform.localScale = scale * Vector3.one;

                var alpha = ringFirstAlpha * Mathf.Clamp01(windowScale - scale);
                var color = rings[i].color;
                color.a = alpha;
                rings[i].color = color;
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
