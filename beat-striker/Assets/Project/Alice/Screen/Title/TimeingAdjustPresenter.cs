using System;
using System.Collections.Generic;
using R3;
using UnityEngine;

namespace Alice {
    public class TimeingAdjustPresenter : IDisposable {
        readonly SettingsDialog settingsDialog;
        readonly IAudioSetting audioSetting;
        readonly IGamePadRegistry gamePadRegistry;
        readonly IAppBGMPlayer appBgmPlayer;
        readonly CompositeDisposable subscriptions = new();
        readonly CompositeDisposable viewSubscriptions = new();
        readonly List<float> sampleLags = new();
        readonly Queue<double> recentTapIntervals = new();
        readonly List<double> tapDspTimes = new();
        readonly Dictionary<int, double> beatTimes = new();

        bool isRunning;
        bool isViewSubscribed;
        bool isBgmStoppedForAdjust;
        double lastTapDspTime = -1;

        public TimeingAdjustPresenter(
            SettingsDialog settingsDialog,
            IAudioSetting audioSetting,
            IGamePadRegistry gamePadRegistry,
            IAppBGMPlayer appBgmPlayer) {
            this.settingsDialog = settingsDialog;
            this.audioSetting = audioSetting;
            this.gamePadRegistry = gamePadRegistry;
            this.appBgmPlayer = appBgmPlayer;

            settingsDialog.TimingAdjustRequested
                .Subscribe(_ => Start())
                .AddTo(subscriptions);

            gamePadRegistry.OnAnyButtonDown
                .Where(e => settingsDialog.gameObject.activeInHierarchy && isRunning && e.Button == GamePadButton.East)
                .Subscribe(_ => HandleTap())
                .AddTo(subscriptions);
        }

        public void Dispose() {
            subscriptions.Dispose();
            viewSubscriptions.Dispose();
            if (isBgmStoppedForAdjust) {
                appBgmPlayer.Resume();
                isBgmStoppedForAdjust = false;
            }
        }

        void Start() {
            if (isRunning) {
                return;
            }

            var timeingAdjustView = settingsDialog.GetOrCreateTimeingAdjustView();
            if (!isViewSubscribed) {
                isViewSubscribed = true;
                timeingAdjustView.BeatPlayed
                    .Subscribe(HandleBeatPlayed)
                    .AddTo(viewSubscriptions);

                timeingAdjustView.SessionCompleted
                    .Subscribe(_ => Complete())
                    .AddTo(viewSubscriptions);
            }

            sampleLags.Clear();
            recentTapIntervals.Clear();
            tapDspTimes.Clear();
            beatTimes.Clear();
            lastTapDspTime = -1;
            isRunning = true;
            isBgmStoppedForAdjust = true;
            appBgmPlayer.Stop();
            settingsDialog.ShowTimingAdjustPlaceholder();
            timeingAdjustView.SetCurrentTapBpm(0f);
            timeingAdjustView.StartSession();

            var firstBeatDspTime = timeingAdjustView.CurrentSessionFirstBeatDspTime;
            var beatInterval = timeingAdjustView.BeatIntervalSeconds;
            for (var i = 0; i < timeingAdjustView.TotalBeatCount; i++) {
                beatTimes[i] = firstBeatDspTime + i * beatInterval;
            }
        }

        void HandleBeatPlayed(TimeingAdjustBeatEvent beatEvent) {
            beatTimes[beatEvent.BeatIndex] = beatEvent.BeatDspTime;
        }

        void HandleTap() {
            tapDspTimes.Add(AudioSettings.dspTime);

            UpdateCurrentTapBpmDisplay();
            settingsDialog.GetOrCreateTimeingAdjustView().PlayTapPulse();
        }

        void UpdateCurrentTapBpmDisplay() {
            var now = AudioSettings.dspTime;
            if (lastTapDspTime > 0) {
                var interval = now - lastTapDspTime;
                if (interval > 0.0001) {
                    recentTapIntervals.Enqueue(interval);
                    while (recentTapIntervals.Count > 3) {
                        recentTapIntervals.Dequeue();
                    }

                    var intervalSum = 0d;
                    foreach (var recentInterval in recentTapIntervals) {
                        intervalSum += recentInterval;
                    }

                    var avgInterval = intervalSum / recentTapIntervals.Count;
                    var bpm = (float)(60d / avgInterval);
                    settingsDialog.GetOrCreateTimeingAdjustView().SetCurrentTapBpm(bpm);
                }
            }

            lastTapDspTime = now;
        }

        bool TryFindNearestBeatInWindow(double tapDspTime, int fromBeatIndex, int toBeatIndexInclusive, double halfWindow, out double beatDspTime) {
            beatDspTime = 0;

            var minDistance = double.MaxValue;
            var hasTarget = false;
            for (var i = fromBeatIndex; i <= toBeatIndexInclusive; i++) {
                if (!beatTimes.TryGetValue(i, out var candidateBeatTime)) {
                    continue;
                }

                var distance = Math.Abs(candidateBeatTime - tapDspTime);
                if (distance > halfWindow) {
                    continue;
                }

                if (distance < minDistance) {
                    minDistance = distance;
                    beatDspTime = candidateBeatTime;
                    hasTarget = true;
                }
            }

            return hasTarget;
        }

        void Complete() {
            if (!isRunning) {
                return;
            }

            isRunning = false;
            settingsDialog.GetOrCreateTimeingAdjustView().StopSession();
            settingsDialog.HideTimingAdjustPlaceholder();
            if (isBgmStoppedForAdjust) {
                appBgmPlayer.Resume();
                isBgmStoppedForAdjust = false;
            }

            var timeingAdjustView = settingsDialog.GetOrCreateTimeingAdjustView();
            sampleLags.Clear();
            var halfWindow = timeingAdjustView.BeatIntervalSeconds * 0.3f;
            var firstSampleBeatIndex = timeingAdjustView.IgnoreBeatCount;
            var lastSampleBeatIndex = timeingAdjustView.TotalBeatCount - 1;
            for (var i = 0; i < tapDspTimes.Count; i++) {
                if (!TryFindNearestBeatInWindow(tapDspTimes[i], firstSampleBeatIndex, lastSampleBeatIndex, halfWindow, out var beatDspTime)) {
                    continue;
                }

                sampleLags.Add((float)(tapDspTimes[i] - beatDspTime));
            }

            if (sampleLags.Count == 0) {
                return;
            }

            var slowestLag = float.MinValue;
            for (var i = 0; i < sampleLags.Count; i++) {
                if (sampleLags[i] > slowestLag) {
                    slowestLag = sampleLags[i];
                }
            }

            var current = audioSetting.BeatOffset.CurrentValue;
            audioSetting.SetBeatOffset(new BeatOffsetSetting(current.CommandTimeOffset, -slowestLag, slowestLag));
        }
    }
}