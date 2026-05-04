using System;
using System.Collections.Generic;
using VContainer;

namespace Alice {
    public sealed class BeatOnlineCommandBuffer {
        readonly Dictionary<int, Dictionary<int, OnlineBeatNotificationSnapshot>> notificationsByBeat = new();
        readonly HashSet<int> closedBeatIndexes = new();

        [Inject]
        public BeatOnlineCommandBuffer() {
        }

        public bool TrySubmit(OnlineBeatNotificationSnapshot notification) {
            if (notification.BeatIndex < 0 || closedBeatIndexes.Contains(notification.BeatIndex)) {
                return false;
            }

            if (!notificationsByBeat.TryGetValue(notification.BeatIndex, out var notificationsByPlayer)) {
                notificationsByPlayer = new Dictionary<int, OnlineBeatNotificationSnapshot>();
                notificationsByBeat[notification.BeatIndex] = notificationsByPlayer;
            }

            if (notificationsByPlayer.ContainsKey(notification.PlayerId)) {
                return false;
            }

            notificationsByPlayer[notification.PlayerId] = notification;
            return true;
        }

        public bool HasSubmission(int beatIndex, int playerId) {
            return notificationsByBeat.TryGetValue(beatIndex, out var notificationsByPlayer)
                && notificationsByPlayer.ContainsKey(playerId);
        }

        public bool IsReady(int beatIndex, int playerCount) {
            return notificationsByBeat.TryGetValue(beatIndex, out var notificationsByPlayer)
                && notificationsByPlayer.Count >= playerCount;
        }

        public bool TryGetNotification(int beatIndex, int playerId, out OnlineBeatNotificationSnapshot notification) {
            if (notificationsByBeat.TryGetValue(beatIndex, out var notificationsByPlayer)
                && notificationsByPlayer.TryGetValue(playerId, out notification)) {
                return true;
            }

            notification = null;
            return false;
        }

        public bool HasSubmissionAfter(int beatIndex, int playerId) {
            foreach (var pair in notificationsByBeat) {
                if (pair.Key > beatIndex && pair.Value.ContainsKey(playerId)) {
                    return true;
                }
            }

            return false;
        }

        public int FillMissingSubmissions(int playerId, int startBeatIndexInclusive, int endBeatIndexExclusive, Func<int, OnlineBeatNotificationSnapshot> createNotification) {
            var submittedCount = 0;
            for (var beatIndex = startBeatIndexInclusive; beatIndex < endBeatIndexExclusive; beatIndex++) {
                if (HasSubmission(beatIndex, playerId)) {
                    continue;
                }

                if (TrySubmit(createNotification(beatIndex))) {
                    submittedCount += 1;
                }
            }

            return submittedCount;
        }

        public void CloseBeat(int beatIndex) {
            closedBeatIndexes.Add(beatIndex);
            notificationsByBeat.Remove(beatIndex);
        }

        public void ClearBeforeBeat(int beatIndex) {
            var removeBeatIndexes = new List<int>();
            foreach (var pair in notificationsByBeat) {
                if (pair.Key < beatIndex) {
                    removeBeatIndexes.Add(pair.Key);
                }
            }

            foreach (var removeBeatIndex in removeBeatIndexes) {
                notificationsByBeat.Remove(removeBeatIndex);
            }

            closedBeatIndexes.RemoveWhere(closedBeatIndex => closedBeatIndex < beatIndex);
        }

        public void Clear() {
            notificationsByBeat.Clear();
            closedBeatIndexes.Clear();
        }
    }
}
