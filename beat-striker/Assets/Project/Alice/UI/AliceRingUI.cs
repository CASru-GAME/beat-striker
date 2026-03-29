using R3;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Alice {
    public class AliceRingUI : MonoBehaviour {
        BeatConfig beatConfig;
        AudioSource audioSource;
        int playerId;
        Transform playerPosition;

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
        CompositeDisposable disposables = new();
        bool battleViewActive;
        Track selectedTrack;
        Vector2 judgeTextStartAnchoredPosition;

        public void Construct(
            int playerId,
            Transform playerPosition,
            BeatConfig beatConfig,
            AudioSource audioSource,
            IBeatPlayer beatPlayer
        ) {
            this.playerId = playerId;
            this.playerPosition = playerPosition;
            this.beatConfig = beatConfig;
            this.audioSource = audioSource;
            selectedTrack = beatConfig.SelectedTrack;

            disposables.Dispose();
            disposables = new CompositeDisposable();
            beatPlayer.OnBeatCommandRequested.Subscribe(result => OnBeat(result)).AddTo(disposables);
            beatPlayer.OnBeatPassed.Subscribe(result => OnBeatPassed(result)).AddTo(disposables);
        }

        void OnBeatPassed(IBeatPlayer.BeatResult result) {
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

        void OnDestroy() {
            disposables.Dispose();
        }

        public void ActivateBattleView() {
            battleViewActive = true;
            centerRing[0].gameObject.SetActive(true);
            foreach (var ring in rings) {
                ring.gameObject.SetActive(true);
            }
        }

        void OnBeat(IBeatPlayer.BeatResult result) {
            AudioSource.PlayClipAtPoint(result.IsSuccess ? successSound : missSound, Vector3.zero);

            var color = centerRing[0].color;
            color.a = 1f;
            centerRing[0].color = color;

            LeanTween.alpha(centerRing[0].rectTransform, centerRingFirstAlpha, 0.3f);

            judgeText.gameObject.SetActive(true);
            judgeText.text = result.IsSuccess ? "good" : "miss";
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

            if (!result.IsSuccess) return;

            successParticle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            successParticle.Play(true);
        }

        void Update() {
            if (!battleViewActive) return;
            if (selectedTrack == null) return;

            var screenPos = Camera.main.WorldToScreenPoint(playerPosition.position);
            transform.position = screenPos;

            var beats = selectedTrack.beats;
            var now = GetCurrentTrackTime();
            var firstUpcoming = GetFirstUpcomingBeatIndex(beats, now);

            for (var i = 0; i < rings.Length; i++) {
                if (firstUpcoming < 0) {
                    rings[i].gameObject.SetActive(false);
                    continue;
                }

                var targetIndex = firstUpcoming + i;
                if (targetIndex < 0 || targetIndex >= beats.Length) {
                    rings[i].gameObject.SetActive(false);
                    continue;
                }

                var nextBeatTime = beats[targetIndex];

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

        float GetCurrentTrackTime() {
            return audioSource.time + beatConfig.ViewTimeOffset;
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
