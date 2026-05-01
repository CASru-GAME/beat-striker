using System;
using R3;
using UnityEngine;

namespace Alice {
    public interface ILoadingOverlayService {
        IDisposable Begin();
    }

    public class LoadingOverlayService : ILoadingOverlayService, IDisposable {
        const float ContinuousHideGraceSeconds = 0.15f;

        readonly LoadingView loadingView;
        readonly IDisposable onlineStateSubscription;
        int activeRequestCount;
        int stateVersion;
        bool isVisible;
        float accumulatedActiveSeconds;
        float activeSegmentStartTime = -1f;

        public LoadingOverlayService(LoadingView loadingView, IAppNetworkSetting appNetworkSetting) {
            this.loadingView = loadingView;
            loadingView.SetOnlineIndicatorVisible(appNetworkSetting.IsOnline.CurrentValue);
            onlineStateSubscription = appNetworkSetting.IsOnline
                .Subscribe(isOnline => loadingView.SetOnlineIndicatorVisible(isOnline));
        }

        public void Dispose() {
            onlineStateSubscription.Dispose();
        }

        public IDisposable Begin() {
            activeRequestCount += 1;
            if (activeRequestCount == 1) {
                activeSegmentStartTime = Time.realtimeSinceStartup;
                stateVersion += 1;
                var beginVersion = stateVersion;
                _ = ShowIfNeededAfterDelayAsync(beginVersion);
            }

            return new Scope(this);
        }

        async Awaitable ShowIfNeededAfterDelayAsync(int beginVersion) {
            var remainingSeconds = loadingView.ShowDelaySeconds - GetContinuousLoadingSeconds();
            if (remainingSeconds > 0f) {
                await WaitForSecondsRealtimeAsync(remainingSeconds);
            }

            if (activeRequestCount <= 0 || beginVersion != stateVersion || isVisible) {
                return;
            }

            isVisible = true;
            await loadingView.ShowAsync();
        }

        async Awaitable EndAsync() {
            if (activeRequestCount <= 0) {
                return;
            }

            activeRequestCount -= 1;
            if (activeRequestCount > 0) {
                return;
            }

            AccumulateCurrentSegment();
            stateVersion += 1;
            var endVersion = stateVersion;
            await WaitForSecondsRealtimeAsync(ContinuousHideGraceSeconds);
            if (activeRequestCount > 0 || endVersion != stateVersion) {
                return;
            }

            accumulatedActiveSeconds = 0f;
            if (!isVisible) {
                return;
            }

            isVisible = false;
            await loadingView.HideAsync();
        }

        float GetContinuousLoadingSeconds() {
            if (activeRequestCount <= 0 || activeSegmentStartTime < 0f) {
                return accumulatedActiveSeconds;
            }

            return accumulatedActiveSeconds + (Time.realtimeSinceStartup - activeSegmentStartTime);
        }

        void AccumulateCurrentSegment() {
            if (activeSegmentStartTime < 0f) {
                return;
            }

            accumulatedActiveSeconds += Time.realtimeSinceStartup - activeSegmentStartTime;
            activeSegmentStartTime = -1f;
        }

        static async Awaitable WaitForSecondsRealtimeAsync(float seconds) {
            if (seconds <= 0f) {
                return;
            }

            var endTime = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < endTime) {
                await Awaitable.NextFrameAsync();
            }
        }

        sealed class Scope : IDisposable {
            readonly LoadingOverlayService owner;
            bool disposed;

            public Scope(LoadingOverlayService owner) {
                this.owner = owner;
            }

            public void Dispose() {
                if (disposed) {
                    return;
                }

                disposed = true;
                _ = owner.EndAsync();
            }
        }
    }
}
