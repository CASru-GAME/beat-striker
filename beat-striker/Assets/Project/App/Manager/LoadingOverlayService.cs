using System;
using System.Collections.Generic;
using UnityEngine;
using VContainer;

namespace Alice {
    public interface ILoadingOverlayService {
        IDisposable Begin(string message = null);
    }

    public class LoadingOverlayService : ILoadingOverlayService, IDisposable {
        const float ContinuousHideGraceSeconds = 0.15f;

        readonly LoadingView loadingView;
        readonly List<Scope> activeScopes = new();
        int stateVersion;
        bool isVisible;
        float accumulatedActiveSeconds;
        float activeSegmentStartTime = -1f;
        ThreadPriority previousLoadingPriority;
        bool loadingPriorityOverridden;
        bool disposed;

        [Inject]
        public LoadingOverlayService(LoadingView loadingView) {
            this.loadingView = loadingView;
        }

        public IDisposable Begin(string message = null) {
            if (disposed) {
                throw new ObjectDisposedException(nameof(LoadingOverlayService));
            }

            var scope = new Scope(this, loadingView.ResolveDisplayMessage(message));
            activeScopes.Add(scope);
            if (activeScopes.Count == 1) {
                previousLoadingPriority = Application.backgroundLoadingPriority;
                loadingPriorityOverridden = true;
                // 非同期ロードのメインスレッド統合を小分けにし、UIを描画する時間を確保する。
                Application.backgroundLoadingPriority = ThreadPriority.Low;
                loadingView.SetMessage(scope.Message);
                activeSegmentStartTime = Time.realtimeSinceStartup;
                stateVersion += 1;
                var beginVersion = stateVersion;
                _ = ShowIfNeededAfterDelayAsync(beginVersion);
            }
            else {
                loadingView.SetMessage(scope.Message);
            }

            return scope;
        }

        async Awaitable ShowIfNeededAfterDelayAsync(int beginVersion) {
            var remainingSeconds = loadingView.ShowDelaySeconds - GetContinuousLoadingSeconds();
            if (remainingSeconds > 0f) {
                await WaitForSecondsRealtimeAsync(remainingSeconds);
            }

            if (disposed || activeScopes.Count == 0 || beginVersion != stateVersion || isVisible) {
                return;
            }

            isVisible = true;
            loadingView.SetMessage(activeScopes[^1].Message);
            await loadingView.ShowAsync();
        }

        async Awaitable EndAsync(Scope scope) {
            if (disposed || !activeScopes.Remove(scope)) {
                return;
            }

            if (activeScopes.Count > 0) {
                loadingView.SetMessage(activeScopes[^1].Message);
                return;
            }

            AccumulateCurrentSegment();
            RestoreLoadingPriority();
            stateVersion += 1;
            var endVersion = stateVersion;
            await WaitForSecondsRealtimeAsync(ContinuousHideGraceSeconds);
            if (disposed || activeScopes.Count > 0 || endVersion != stateVersion) {
                return;
            }

            accumulatedActiveSeconds = 0f;
            if (!isVisible) {
                return;
            }

            isVisible = false;
            await loadingView.HideAsync();
        }

        public void Dispose() {
            if (disposed) {
                return;
            }

            disposed = true;
            stateVersion += 1;
            activeScopes.Clear();
            RestoreLoadingPriority();
        }

        void RestoreLoadingPriority() {
            if (!loadingPriorityOverridden) {
                return;
            }

            Application.backgroundLoadingPriority = previousLoadingPriority;
            loadingPriorityOverridden = false;
        }

        float GetContinuousLoadingSeconds() {
            if (activeScopes.Count == 0 || activeSegmentStartTime < 0f) {
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

        async Awaitable WaitForSecondsRealtimeAsync(float seconds) {
            if (seconds <= 0f) {
                return;
            }

            var endTime = Time.realtimeSinceStartup + seconds;
            while (!disposed && Time.realtimeSinceStartup < endTime) {
                await Awaitable.NextFrameAsync();
            }
        }

        sealed class Scope : IDisposable {
            readonly LoadingOverlayService owner;
            bool disposed;

            public string Message { get; }

            public Scope(LoadingOverlayService owner, string message) {
                this.owner = owner;
                Message = message;
            }

            public void Dispose() {
                if (disposed) {
                    return;
                }

                disposed = true;
                _ = owner.EndAsync(this);
            }
        }
    }
}
