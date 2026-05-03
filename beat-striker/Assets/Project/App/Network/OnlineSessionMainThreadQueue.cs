using System;
using System.Collections.Concurrent;
using UnityEngine;

namespace Alice {
    /// <summary>
    /// Fusion などのコールバックからメインスレッドで処理したい処理を積む。
    /// </summary>
    sealed class OnlineSessionMainThreadQueue : MonoBehaviour {
        readonly ConcurrentQueue<Action> queue = new();

        public void Enqueue(Action action) {
            queue.Enqueue(action);
        }

        /// <summary>
        /// マッチ待機ループなど、同一フレーム内でコールバック詰めを先に処理したいときに使う。
        /// </summary>
        public void Flush() {
            while (queue.TryDequeue(out var action)) {
                action();
            }
        }

        void Update() {
            Flush();
        }
    }
}
