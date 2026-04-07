using System.Collections;
using R3;
using TMPro;
using UnityEngine;

namespace Alice {
    public record TimeingAdjustBeatEvent(int BeatIndex, double BeatDspTime);

    public class TimeingAdjustView : MonoBehaviour {
        [SerializeField] AudioSource audioSource;
        [SerializeField] AudioClip beatSe;
        [SerializeField] float bpm = 110f;
        [SerializeField] int ignoreBeatCount = 5;
        [SerializeField] int sampleBeatCount = 20;
        [SerializeField] float startDelaySeconds = 0.5f;
        [SerializeField] float beatViewOffsetSeconds = 0.03f;
        [SerializeField] float closeDelaySeconds = 0.5f;
        [SerializeField] RectTransform tapPulseTarget;
        [SerializeField] float tapPulseScale = 1.2f;
        [SerializeField] float tapPulseDurationSeconds = 0.12f;
        [SerializeField] TextMeshProUGUI currentTapBpmLabel;

        readonly Subject<TimeingAdjustBeatEvent> beatPlayed = new();
        readonly Subject<Unit> sessionCompleted = new();

        Coroutine sessionCoroutine;
        Coroutine tapPulseCoroutine;
        Vector3 tapPulseInitialScale;
        bool hasTapPulseInitialScale;
        double currentSessionFirstBeatDspTime;

        public Observable<TimeingAdjustBeatEvent> BeatPlayed => beatPlayed;
        public Observable<Unit> SessionCompleted => sessionCompleted;
        public int IgnoreBeatCount => ignoreBeatCount;
        public int SampleBeatCount => sampleBeatCount;
        public int TotalBeatCount => ignoreBeatCount + sampleBeatCount;
        public float BeatIntervalSeconds => 60f / bpm;
        public double CurrentSessionFirstBeatDspTime => currentSessionFirstBeatDspTime;

        public void StartSession() {
            StopSession();
            gameObject.SetActive(true);
            SetCurrentTapBpm(0f);
            currentSessionFirstBeatDspTime = AudioSettings.dspTime + startDelaySeconds - beatViewOffsetSeconds;
            sessionCoroutine = StartCoroutine(SessionRoutine());
        }

        public void StopSession() {
            if (sessionCoroutine != null) {
                StopCoroutine(sessionCoroutine);
                sessionCoroutine = null;
            }

            if (audioSource.isPlaying) {
                audioSource.Stop();
            }

            if (tapPulseCoroutine != null) {
                StopCoroutine(tapPulseCoroutine);
                tapPulseCoroutine = null;
            }

            if (tapPulseTarget != null && hasTapPulseInitialScale) {
                tapPulseTarget.localScale = tapPulseInitialScale;
            }
        }

        public void PlayTapPulse() {
            if (tapPulseTarget == null) {
                return;
            }

            if (!hasTapPulseInitialScale) {
                tapPulseInitialScale = tapPulseTarget.localScale;
                hasTapPulseInitialScale = true;
            }

            if (tapPulseCoroutine != null) {
                StopCoroutine(tapPulseCoroutine);
            }

            tapPulseCoroutine = StartCoroutine(TapPulseRoutine());
        }

        public void SetCurrentTapBpm(float bpmValue) {
            if (currentTapBpmLabel == null) {
                return;
            }

            currentTapBpmLabel.text = bpmValue > 0f ? bpmValue.ToString("0.0") : "--.-";
        }

        IEnumerator TapPulseRoutine() {
            var halfDuration = tapPulseDurationSeconds * 0.5f;
            var expandedScale = tapPulseInitialScale * tapPulseScale;

            var elapsed = 0f;
            while (elapsed < halfDuration) {
                elapsed += Time.unscaledDeltaTime;
                var t = halfDuration <= 0f ? 1f : Mathf.Clamp01(elapsed / halfDuration);
                tapPulseTarget.localScale = Vector3.LerpUnclamped(tapPulseInitialScale, expandedScale, t);
                yield return null;
            }

            elapsed = 0f;
            while (elapsed < halfDuration) {
                elapsed += Time.unscaledDeltaTime;
                var t = halfDuration <= 0f ? 1f : Mathf.Clamp01(elapsed / halfDuration);
                tapPulseTarget.localScale = Vector3.LerpUnclamped(expandedScale, tapPulseInitialScale, t);
                yield return null;
            }

            tapPulseTarget.localScale = tapPulseInitialScale;
            tapPulseCoroutine = null;
        }

        IEnumerator SessionRoutine() {
            var totalBeatCount = ignoreBeatCount + sampleBeatCount;
            var beatInterval = BeatIntervalSeconds;
            var nextBeatDspTime = currentSessionFirstBeatDspTime;

            for (var i = 0; i < totalBeatCount; i++) {
                while (AudioSettings.dspTime < nextBeatDspTime) {
                    yield return null;
                }

                audioSource.PlayOneShot(beatSe);
                beatPlayed.OnNext(new TimeingAdjustBeatEvent(i, nextBeatDspTime));
                nextBeatDspTime += beatInterval;
            }

            if (closeDelaySeconds > 0f) {
                yield return new WaitForSecondsRealtime(closeDelaySeconds);
            }

            sessionCoroutine = null;
            sessionCompleted.OnNext(Unit.Default);
        }
    }
}