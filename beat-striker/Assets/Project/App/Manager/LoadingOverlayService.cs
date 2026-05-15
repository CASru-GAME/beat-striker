using System;
using System.Collections.Generic;
using UnityEngine;
using VContainer;

namespace Alice {
    public interface ILoadingOverlayService {
        IDisposable Begin(string message = null);
    }

    public class LoadingOverlayService : ILoadingOverlayService {
        const float ContinuousHideGraceSeconds = 0.15f;

        readonly LoadingView loadingView;
        readonly List<string> messageStack = new();
        int activeRequestCount;
        int stateVersion;
        bool isVisible;
        float accumulatedActiveSeconds;
        float activeSegmentStartTime = -1f;

        [Inject]
        public LoadingOverlayService(LoadingView loadingView) {
            this.loadingView = loadingView;
        }

        public IDisposable Begin(string message = null) {
            messageStack.Add(loadingView.ResolveDisplayMessage(message));
            activeRequestCount += 1;
            if (activeRequestCount == 1) {
                loadingView.SetMessage(messageStack[^1]);
                activeSegmentStartTime = Time.realtimeSinceStartup;
                stateVersion += 1;
                var beginVersion = stateVersion;
                _ = ShowIfNeededAfterDelayAsync(beginVersion);
            }
            else {
                loadingView.SetMessage(messageStack[^1]);
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
            loadingView.SetMessage(messageStack[^1]);
            await loadingView.ShowAsync();
        }

        async Awaitable EndAsync() {
            if (activeRequestCount <= 0) {
                return;
            }

            activeRequestCount -= 1;
            messageStack.RemoveAt(messageStack.Count - 1);
            if (activeRequestCount > 0) {
                loadingView.SetMessage(messageStack[^1]);
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
