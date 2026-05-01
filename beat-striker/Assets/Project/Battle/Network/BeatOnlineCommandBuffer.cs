using System.Collections.Generic;

namespace Alice {
    public sealed class BeatOnlineCommandBuffer {
        readonly Dictionary<int, Dictionary<int, OnlineBeatCommandSnapshot>> commandsByBeat = new();
        readonly HashSet<int> closedBeatIndexes = new();

        public bool TrySubmit(OnlineBeatCommandSnapshot command) {
            if (command.BeatIndex < 0 || closedBeatIndexes.Contains(command.BeatIndex)) {
                return false;
            }

            if (!commandsByBeat.TryGetValue(command.BeatIndex, out var commandsByPlayer)) {
                commandsByPlayer = new Dictionary<int, OnlineBeatCommandSnapshot>();
                commandsByBeat[command.BeatIndex] = commandsByPlayer;
            }

            if (commandsByPlayer.ContainsKey(command.PlayerId)) {
                return false;
            }

            commandsByPlayer[command.PlayerId] = command;
            return true;
        }

        public bool HasSubmission(int beatIndex, int playerId) {
            return commandsByBeat.TryGetValue(beatIndex, out var commandsByPlayer)
                && commandsByPlayer.ContainsKey(playerId);
        }

        public bool IsReady(int beatIndex, int playerCount) {
            return commandsByBeat.TryGetValue(beatIndex, out var commandsByPlayer)
                && commandsByPlayer.Count >= playerCount;
        }

        public bool TryGetCommand(int beatIndex, int playerId, out OnlineBeatCommandSnapshot command) {
            if (commandsByBeat.TryGetValue(beatIndex, out var commandsByPlayer)
                && commandsByPlayer.TryGetValue(playerId, out command)) {
                return true;
            }

            command = null;
            return false;
        }

        public void CloseBeat(int beatIndex) {
            closedBeatIndexes.Add(beatIndex);
            commandsByBeat.Remove(beatIndex);
        }

        public void ClearBeforeBeat(int beatIndex) {
            var removeBeatIndexes = new List<int>();
            foreach (var pair in commandsByBeat) {
                if (pair.Key < beatIndex) {
                    removeBeatIndexes.Add(pair.Key);
                }
            }

            foreach (var removeBeatIndex in removeBeatIndexes) {
                commandsByBeat.Remove(removeBeatIndex);
            }

            closedBeatIndexes.RemoveWhere(closedBeatIndex => closedBeatIndex < beatIndex);
        }

        public void Clear() {
            commandsByBeat.Clear();
            closedBeatIndexes.Clear();
        }
    }
}
