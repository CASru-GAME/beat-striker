using R3;
using UnityEngine;

namespace Alice {
    public class SettingsDialogPresenter : System.IDisposable {
        readonly SettingsDialog view;
        readonly IAudioSetting audioSetting;
        readonly ICursorMoveSetting cursorMoveSetting;
        readonly IGamePadRegistry gamePadRegistry;
        readonly CompositeDisposable subscriptions = new();

        public SettingsDialogPresenter(
            SettingsDialog view,
            IAudioSetting audioSetting,
            ICursorMoveSetting cursorMoveSetting,
            IGamePadRegistry gamePadRegistry) {
            this.view = view;
            this.audioSetting = audioSetting;
            this.cursorMoveSetting = cursorMoveSetting;
            this.gamePadRegistry = gamePadRegistry;

            gamePadRegistry.OnAnyButtonDown
                .Where(e => view.gameObject.activeInHierarchy && e.Button == GamePadButton.South && !view.IsTimeingAdjustActive)
                .Subscribe(_ => this.view.SetVisible(false))
                .AddTo(subscriptions);

            this.view.CursorSpeedDecreaseRequested
                .Subscribe(_ => UpdateCursorSpeed(-1f))
                .AddTo(subscriptions);

            this.view.CursorSpeedIncreaseRequested
                .Subscribe(_ => UpdateCursorSpeed(1f))
                .AddTo(subscriptions);

            this.view.BgmVolumeDecreaseRequested
                .Subscribe(_ => UpdateBgmVolume(-1f))
                .AddTo(subscriptions);

            this.view.BgmVolumeIncreaseRequested
                .Subscribe(_ => UpdateBgmVolume(1f))
                .AddTo(subscriptions);

            this.view.GamePadSlotClicked
                .Subscribe(request => gamePadRegistry.HandlePlayerSlotClick(request.CursorPlayerId, request.TargetPlayerId))
                .AddTo(subscriptions);

            cursorMoveSetting.CursorSpeed
                .Subscribe(this.view.SetCursorSpeedValue)
                .AddTo(subscriptions);

            audioSetting.VolumeBalance
                .Subscribe(balance => this.view.SetBgmVolumeValue(balance.BgmVolume))
                .AddTo(subscriptions);

            audioSetting.BeatOffset
                .Subscribe(offset => this.view.SetBeatOffsetValue(offset.BeatTimeOffset))
                .AddTo(subscriptions);

            for (var i = 0; i < 3; i++) {
                var slotIndex = i;
                gamePadRegistry.Get(slotIndex).HasGamePad
                    .Subscribe(hasGamePad => this.view.SetGamePadConnected(slotIndex, hasGamePad))
                    .AddTo(subscriptions);
            }

            this.view.SetCursorSpeedValue(cursorMoveSetting.CursorSpeed.CurrentValue);
            this.view.SetBgmVolumeValue(audioSetting.VolumeBalance.CurrentValue.BgmVolume);
            this.view.SetBeatOffsetValue(audioSetting.BeatOffset.CurrentValue.BeatTimeOffset);
        }

        public void Dispose() {
            subscriptions.Dispose();
        }

        void UpdateCursorSpeed(float direction) {
            var range = view.CursorSpeedRange;
            var current = cursorMoveSetting.CursorSpeed.CurrentValue;
            var next = Snap(Mathf.Clamp(current + direction * range.Step, range.Min, range.Max), range);
            cursorMoveSetting.SetCursorSpeed(next);
        }

        void UpdateBgmVolume(float direction) {
            var range = view.BgmVolumeRange;
            var current = audioSetting.VolumeBalance.CurrentValue.BgmVolume;
            var next = Snap(Mathf.Clamp(current + direction * range.Step, range.Min, range.Max), range);
            var volumeBalance = audioSetting.VolumeBalance.CurrentValue;
            audioSetting.SetVolumeBalance(new VolumeBalance(volumeBalance.MasterVolume, next, volumeBalance.SeVolume));
        }

        float Snap(float value, ValueRangeSetting range) {
            var stepCount = Mathf.Round((value - range.Min) / range.Step);
            return range.Min + stepCount * range.Step;
        }
    }
}
