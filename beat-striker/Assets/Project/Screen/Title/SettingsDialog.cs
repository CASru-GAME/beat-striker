using R3;
using TMPro;
using UnityEngine;

namespace Alice {
    public record ValueRangeSetting(float Min, float Max, float Step);

    public class SettingsDialog : MonoBehaviour {
        [System.Serializable]
        class ValueRangeSettingEntry {
            [SerializeField] float min = 0f;
            [SerializeField] float max = 1f;
            [SerializeField] float step = 0.1f;

            public ValueRangeSetting ToValueRangeSetting() {
                return new ValueRangeSetting(min, max, step);
            }
        }

        readonly Subject<Unit> opened = new();
        readonly Subject<Unit> closed = new();
        readonly Subject<Unit> timingAdjustRequested = new();
        readonly Subject<Unit> cursorSpeedDecreaseRequested = new();
        readonly Subject<Unit> cursorSpeedIncreaseRequested = new();
        readonly Subject<Unit> bgmVolumeDecreaseRequested = new();
        readonly Subject<Unit> bgmVolumeIncreaseRequested = new();
        readonly Subject<Unit> controllerResetRequested = new();

        [Header("Buttons")]
        [SerializeField] ActionEmitter timingAdjustEmitter;
        [SerializeField] AudioClip backSound;
        [SerializeField] ActionEmitter cursorSpeedLeftEmitter;
        [SerializeField] ActionEmitter cursorSpeedRightEmitter;
        [SerializeField] ActionEmitter bgmVolumeLeftEmitter;
        [SerializeField] ActionEmitter bgmVolumeRightEmitter;
        [SerializeField] ActionEmitter controllerResetEmitter;

        [Header("Value Labels")]
        [SerializeField] TextMeshProUGUI cursorSpeedValueLabel;
        [SerializeField] TextMeshProUGUI bgmVolumeValueLabel;
        [SerializeField] TextMeshProUGUI beatOffsetValueLabel;

        [Header("Ranges")]
        [SerializeField] ValueRangeSettingEntry cursorSpeedRange = new();
        [SerializeField] ValueRangeSettingEntry bgmVolumeRange = new();

        [Header("Timing Adjustment Placeholder")]
        [SerializeField] TimeingAdjustView timeingAdjustView;

        public Observable<Unit> Opened => opened;
        public Observable<Unit> Closed => closed;
        public Observable<Unit> TimingAdjustRequested => timingAdjustRequested;
        public Observable<Unit> CursorSpeedDecreaseRequested => cursorSpeedDecreaseRequested;
        public Observable<Unit> CursorSpeedIncreaseRequested => cursorSpeedIncreaseRequested;
        public Observable<Unit> BgmVolumeDecreaseRequested => bgmVolumeDecreaseRequested;
        public Observable<Unit> BgmVolumeIncreaseRequested => bgmVolumeIncreaseRequested;
        public Observable<Unit> ControllerResetRequested => controllerResetRequested;
        public bool IsTimeingAdjustActive => timeingAdjustView != null && timeingAdjustView.gameObject.activeSelf;

        public ValueRangeSetting CursorSpeedRange => cursorSpeedRange.ToValueRangeSetting();
        public ValueRangeSetting BgmVolumeRange => bgmVolumeRange.ToValueRangeSetting();

        void Awake() {
            timingAdjustEmitter.OnClickEvent.Subscribe(_ => timingAdjustRequested.OnNext(Unit.Default)).AddTo(this);
            cursorSpeedLeftEmitter.OnClickEvent.Subscribe(_ => cursorSpeedDecreaseRequested.OnNext(Unit.Default)).AddTo(this);
            cursorSpeedRightEmitter.OnClickEvent.Subscribe(_ => cursorSpeedIncreaseRequested.OnNext(Unit.Default)).AddTo(this);
            bgmVolumeLeftEmitter.OnClickEvent.Subscribe(_ => bgmVolumeDecreaseRequested.OnNext(Unit.Default)).AddTo(this);
            bgmVolumeRightEmitter.OnClickEvent.Subscribe(_ => bgmVolumeIncreaseRequested.OnNext(Unit.Default)).AddTo(this);

            controllerResetEmitter.OnClickEvent.Subscribe(_ => controllerResetRequested.OnNext(Unit.Default)).AddTo(this);
        }

        void OnEnable() {
            opened.OnNext(Unit.Default);
        }

        void OnDisable() {
            closed.OnNext(Unit.Default);
        }

        public void SetVisible(bool visible) {
            gameObject.SetActive(visible);
        }
        public void PlayBackSound() => backSound.PlayAtApp();

        public void SetCursorSpeedValue(float value) {
            cursorSpeedValueLabel.text = value.ToString("0.0");
        }

        public void SetBgmVolumeValue(float value) {
            bgmVolumeValueLabel.text = value.ToString("0.0");
        }

        public void SetBeatOffsetValue(float value) {
            var roundedValue = Mathf.Round(value * 100f) / 100f;
            beatOffsetValueLabel.text = roundedValue.ToString("0.00");
        }

        public TimeingAdjustView TimingAdjustView => timeingAdjustView;

        public void ShowTimingAdjustPlaceholder() {
            timeingAdjustView.gameObject.SetActive(true);
        }

        public void HideTimingAdjustPlaceholder() {
            if (timeingAdjustView != null) {
                timeingAdjustView.gameObject.SetActive(false);
            }
        }
    }
}
