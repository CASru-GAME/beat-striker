using Core;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Alice {
    public record GamePadSlotClickRequest(int TargetPlayerId, int CursorPlayerId);
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

        [System.Serializable]
        class PlayerSlotView {
            [SerializeField] ActionEmitter emitter;
            [SerializeField] Graphic iconGraphic;
            [SerializeField] int playerId;

            public ActionEmitter Emitter => emitter;
            public Graphic IconGraphic => iconGraphic;
            public int PlayerId => playerId;
        }

        readonly Subject<Unit> opened = new();
        readonly Subject<Unit> closed = new();
        readonly Subject<Unit> timingAdjustRequested = new();
        readonly Subject<Unit> cursorSpeedDecreaseRequested = new();
        readonly Subject<Unit> cursorSpeedIncreaseRequested = new();
        readonly Subject<Unit> bgmVolumeDecreaseRequested = new();
        readonly Subject<Unit> bgmVolumeIncreaseRequested = new();
        readonly Subject<GamePadSlotClickRequest> gamePadSlotClicked = new();

        [Header("Buttons")]
        [SerializeField] ActionEmitter timingAdjustEmitter;
        [SerializeField] ActionEmitter cursorSpeedLeftEmitter;
        [SerializeField] ActionEmitter cursorSpeedRightEmitter;
        [SerializeField] ActionEmitter bgmVolumeLeftEmitter;
        [SerializeField] ActionEmitter bgmVolumeRightEmitter;
        [SerializeField] PlayerSlotView[] playerSlots;

        [Header("Value Labels")]
        [SerializeField] TextMeshProUGUI cursorSpeedValueLabel;
        [SerializeField] TextMeshProUGUI bgmVolumeValueLabel;
        [SerializeField] TextMeshProUGUI beatOffsetValueLabel;

        [Header("Player Slot Colors")]
        [SerializeField] Color connectedColor = Color.white;
        [SerializeField] Color disconnectedColor = new Color(0.25f, 0.25f, 0.25f, 1f);

        [Header("Ranges")]
        [SerializeField] ValueRangeSettingEntry cursorSpeedRange = new();
        [SerializeField] ValueRangeSettingEntry bgmVolumeRange = new();

        [Header("Timing Adjustment Placeholder")]
        [SerializeField] GameObject timingAdjustPlaceholderRoot;
        [SerializeField] GameObject timeingAdjustViewPrefab;

        TimeingAdjustView timeingAdjustView;

        public Observable<Unit> Opened => opened;
        public Observable<Unit> Closed => closed;
        public Observable<Unit> TimingAdjustRequested => timingAdjustRequested;
        public Observable<Unit> CursorSpeedDecreaseRequested => cursorSpeedDecreaseRequested;
        public Observable<Unit> CursorSpeedIncreaseRequested => cursorSpeedIncreaseRequested;
        public Observable<Unit> BgmVolumeDecreaseRequested => bgmVolumeDecreaseRequested;
        public Observable<Unit> BgmVolumeIncreaseRequested => bgmVolumeIncreaseRequested;
        public Observable<GamePadSlotClickRequest> GamePadSlotClicked => gamePadSlotClicked;
        public bool IsTimeingAdjustActive => timeingAdjustView != null && timeingAdjustView.gameObject.activeSelf;

        public ValueRangeSetting CursorSpeedRange => cursorSpeedRange.ToValueRangeSetting();
        public ValueRangeSetting BgmVolumeRange => bgmVolumeRange.ToValueRangeSetting();

        void Awake() {
            timingAdjustEmitter.OnClickEvent.Subscribe(_ => timingAdjustRequested.OnNext(Unit.Default)).AddTo(this);
            cursorSpeedLeftEmitter.OnClickEvent.Subscribe(_ => cursorSpeedDecreaseRequested.OnNext(Unit.Default)).AddTo(this);
            cursorSpeedRightEmitter.OnClickEvent.Subscribe(_ => cursorSpeedIncreaseRequested.OnNext(Unit.Default)).AddTo(this);
            bgmVolumeLeftEmitter.OnClickEvent.Subscribe(_ => bgmVolumeDecreaseRequested.OnNext(Unit.Default)).AddTo(this);
            bgmVolumeRightEmitter.OnClickEvent.Subscribe(_ => bgmVolumeIncreaseRequested.OnNext(Unit.Default)).AddTo(this);

            for (var i = 0; i < playerSlots.Length; i++) {
                var slot = playerSlots[i];
                slot.Emitter.OnClickEvent
                    .Subscribe(data => {
                        if (data.EventData.pointerId < 0) {
                            return;
                        }

                        gamePadSlotClicked.OnNext(new GamePadSlotClickRequest(slot.PlayerId, data.EventData.pointerId));
                    })
                    .AddTo(this);
            }
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

        public void SetGamePadConnected(int playerId, bool connected) {
            for (var i = 0; i < playerSlots.Length; i++) {
                if (playerSlots[i].PlayerId != playerId) {
                    continue;
                }

                playerSlots[i].IconGraphic.color = connected ? connectedColor : disconnectedColor;
                return;
            }
        }

        public TimeingAdjustView GetOrCreateTimeingAdjustView() {
            if (timeingAdjustView == null) {
                var timeingAdjustViewObject = Instantiate(timeingAdjustViewPrefab, timingAdjustPlaceholderRoot.transform);
                timeingAdjustView = timeingAdjustViewObject.GetComponent<TimeingAdjustView>();
                timeingAdjustView.gameObject.SetActive(false);
            }

            return timeingAdjustView;
        }

        public void ShowTimingAdjustPlaceholder() {
            GetOrCreateTimeingAdjustView().gameObject.SetActive(true);
        }

        public void HideTimingAdjustPlaceholder() {
            if (timeingAdjustView != null) {
                timeingAdjustView.gameObject.SetActive(false);
            }
        }
    }
}
