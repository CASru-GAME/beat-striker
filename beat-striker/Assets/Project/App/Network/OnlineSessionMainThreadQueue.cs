using System;
using System.Collections.Concurrent;
using UnityEngine;

namespace Alice {
    /// <summary>
    /// Fusion などのコールバックからメインスレッドで処理したい処理を積む。
    /// </summary>
    sealed class OnlineSessionMainThreadQueue : MonoBehaviour {
        const string LOG_PREFIX = "[OnlineSessionMainThreadQueue]";

        readonly ConcurrentQueue<Action> queue = new();

        public void Enqueue(Action action, string label) {
            queue.Enqueue(action);
            Debug.Log($"{LOG_PREFIX} Enqueue label={label}, pendingApprox={queue.Count}");
        }

        /// <summary>
        /// マッチ待機ループなど、同一フレーム内でコールバック詰めを先に処理したいときに使う。
        /// </summary>
        /// <returns>実行したアクション数（ログはここでは出さず呼び出し側でまとめる）</returns>
        public int Flush() {
            var n = 0;
            while (queue.TryDequeue(out var action)) {
                action();
                n++;
            }

            return n;
        }

        public int PendingApprox => queue.Count;

        void Update() {
            var n = Flush();
            if (n > 0) {
                Debug.Log($"{LOG_PREFIX} Update drained deferredActions={n}, pendingApproxAfter={queue.Count}");
            }
        }
    }
}
