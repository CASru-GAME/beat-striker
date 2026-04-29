using System;
using UnityEngine;

namespace Alice {
    public interface ILoadingOverlayService {
        IDisposable Begin();
    }

    public class LoadingOverlayService : ILoadingOverlayService {
        readonly LoadingView loadingView;
        int activeRequestCount;
        int version;
        bool isVisible;

        public LoadingOverlayService(LoadingView loadingView) {
            this.loadingView = loadingView;
        }

        public IDisposable Begin() {
            activeRequestCount += 1;
            version += 1;
            var beginVersion = version;
            _ = ShowIfNeededAfterDelayAsync(beginVersion);
            return new Scope(this, beginVersion);
        }

        async Awaitable ShowIfNeededAfterDelayAsync(int beginVersion) {
            var showDelaySeconds = loadingView.ShowDelaySeconds;
            if (showDelaySeconds > 0f) {
                await Awaitable.WaitForSecondsAsync(showDelaySeconds);
            }

            if (activeRequestCount <= 0 || beginVersion != version || isVisible) {
                return;
            }

            isVisible = true;
            await loadingView.ShowAsync();
        }

        async Awaitable EndAsync(int beginVersion) {
            if (activeRequestCount <= 0) {
                return;
            }

            activeRequestCount -= 1;
            if (activeRequestCount > 0) {
                return;
            }

            version = beginVersion + 1;
            if (!isVisible) {
                return;
            }

            isVisible = false;
            await loadingView.HideAsync();
        }

        sealed class Scope : IDisposable {
            readonly LoadingOverlayService owner;
            readonly int beginVersion;
            bool disposed;

            public Scope(LoadingOverlayService owner, int beginVersion) {
                this.owner = owner;
                this.beginVersion = beginVersion;
            }

            public void Dispose() {
                if (disposed) {
                    return;
                }

                disposed = true;
                _ = owner.EndAsync(beginVersion);
            }
        }
    }
}
