using System;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Alice {
    public class AliceRingUI : MonoBehaviour {
        [SerializeField] Image[] centerRing;
        [SerializeField] Image[] rings;
        [SerializeField] TextMeshProUGUI judgeText;
        [SerializeField] ParticleSystem successParticle;
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
        Vector2 judgeTextStartAnchoredPosition;

        public void NotifyBeatPassed() {
            AudioSource.PlayClipAtPoint(missSound, Vector3.zero);

            var color = centerRing[0].color;
            color.a = 1f;
            centerRing[0].color = color;

            LeanTween.alpha(centerRing[0].rectTransform, centerRingFirstAlpha, 0.3f);

            judgeText.gameObject.SetActive(true);
            judgeText.text = "pass";
            var judgeColor = judgeText.color;
            judgeColor.a = 1f;
            judgeText.color = judgeColor;
            judgeText.rectTransform.anchoredPosition = judgeTextStartAnchoredPosition;

            LeanTween.cancel(judgeText.gameObject);
            var targetAnchoredPosition = judgeTextStartAnchoredPosition + Vector2.down * judgeTextDropDistance;
            LeanTween.value(judgeText.gameObject, judgeTextStartAnchoredPosition, targetAnchoredPosition, judgeTextFadeDuration)
                .setEase(LeanTweenType.easeInSine)
                .setOnUpdate((Vector2 position) => {
                    judgeText.rectTransform.anchoredPosition = position;
                });

            LeanTween.value(judgeText.gameObject, 1f, 0f, judgeTextFadeDuration)
                .setOnUpdate((float alpha) => {
                    var currentColor = judgeText.color;
                    currentColor.a = alpha;
                    judgeText.color = currentColor;
                })
                .setOnComplete(() => {
                    judgeText.rectTransform.anchoredPosition = judgeTextStartAnchoredPosition;
                    judgeText.gameObject.SetActive(false);
                });
        }

        void Awake() {
            centerRing[0].gameObject.SetActive(false);
            judgeTextStartAnchoredPosition = judgeText.rectTransform.anchoredPosition;
            judgeText.gameObject.SetActive(false);
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

        public void SetBeatTimeline(float[] beats) {
            beatTimeline = beats ?? Array.Empty<float>();
        }

        public void SetViewPlaybackTime(float playbackTime) {
            currentViewPlaybackTime = playbackTime;
        }

        public void SetPlayerWorldPosition(Vector3 worldPosition) {
            currentWorldPosition = worldPosition;
        }

        public void NotifyBeatRequested(bool isSuccess) {
            AudioSource.PlayClipAtPoint(isSuccess ? successSound : missSound, Vector3.zero);

            var color = centerRing[0].color;
            color.a = 1f;
            centerRing[0].color = color;

            LeanTween.alpha(centerRing[0].rectTransform, centerRingFirstAlpha, 0.3f);

            judgeText.gameObject.SetActive(true);
            judgeText.text = isSuccess ? "good" : "miss";
            var judgeColor = judgeText.color;
            judgeColor.a = 1f;
            judgeText.color = judgeColor;
            judgeText.rectTransform.anchoredPosition = judgeTextStartAnchoredPosition;

            LeanTween.cancel(judgeText.gameObject);
            var targetAnchoredPosition = judgeTextStartAnchoredPosition + Vector2.down * judgeTextDropDistance;
            LeanTween.value(judgeText.gameObject, judgeTextStartAnchoredPosition, targetAnchoredPosition, judgeTextFadeDuration)
                .setEase(LeanTweenType.easeInSine)
                .setOnUpdate((Vector2 position) => {
                    judgeText.rectTransform.anchoredPosition = position;
                });

            LeanTween.value(judgeText.gameObject, 1f, 0f, judgeTextFadeDuration)
                .setOnUpdate((float alpha) => {
                    var currentColor = judgeText.color;
                    currentColor.a = alpha;
                    judgeText.color = currentColor;
                })
                .setOnComplete(() => {
                    judgeText.rectTransform.anchoredPosition = judgeTextStartAnchoredPosition;
                    judgeText.gameObject.SetActive(false);
                });

            if (!isSuccess) return;

            successParticle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            successParticle.Play(true);
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
