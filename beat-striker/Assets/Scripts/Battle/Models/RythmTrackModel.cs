
using System;
using System.Collections.Generic;
using Core.App.Types;
using Core.Utils;
using UnityEngine;

namespace Core.Battle {
    
    public class RythmTrackModel : IRythmTrackModel {
        private float currentTime = 0f;
        private float[] beatTimes;
        private float excellentWindow;
        private float goodWindow;
        private float timeOffset;
        private int[] nextBeatIndex = new int[4];
        
        private readonly Subject<PlayerId> onMissedBeat = new();

        public RythmTrackModel(float[] beatTimes, float perfectWindow, float goodWindow, float timeOffset) {
            this.beatTimes = beatTimes;
            this.excellentWindow = perfectWindow;
            this.goodWindow = goodWindow;
            this.timeOffset = timeOffset;
            this.currentTime = timeOffset;
        }

        public IDisposable SubscribeMissedBeat(Action<PlayerId> listener) => onMissedBeat.Subscribe(listener);

        public BeatResult Beat(PlayerId playerId) {
            var pid = playerId.value;
            if (pid < 0 || pid >= nextBeatIndex.Length)
                return new BeatResult(BeatStatus.Miss);

            int playerBeatIndex = nextBeatIndex[pid];
            if (playerBeatIndex >= beatTimes.Length) return new BeatResult(BeatStatus.Miss);

            float beatTime = beatTimes[playerBeatIndex];
            float delta = currentTime - beatTime;

            if (Mathf.Abs(delta) < excellentWindow) {
                nextBeatIndex[pid]++;
                return new BeatResult(BeatStatus.Excellent);
            }
            else if (Mathf.Abs(delta) < goodWindow) {
                nextBeatIndex[pid]++;
                return new BeatResult(BeatStatus.Good);
            }

            return new BeatResult(BeatStatus.Miss);
        }

        public float GetNextBeatTime(PlayerId playerId, int offset) {
            int index = nextBeatIndex[playerId.value] + offset;
            if (index >= 0 && index < beatTimes.Length)
                return beatTimes[index];
            return float.NaN;
        }

        public float GetTime() {
            return currentTime;
        }

        public List<PlayerId> SetTime(float time) {
            currentTime = time;
            var missedPlayers = new List<PlayerId>();
            
            for (int pid = 0; pid < nextBeatIndex.Length; pid++) {
                int playerBeatIndex = nextBeatIndex[pid];
                int initialIndex = playerBeatIndex;
                
                while (true) {
                    if (playerBeatIndex >= beatTimes.Length) break;
                    if (beatTimes[playerBeatIndex] > currentTime - goodWindow) break;
                    playerBeatIndex++;
                }
                
                // 見逃したビートがあれば追加（1つでもスキップされたら）
                if (playerBeatIndex > initialIndex) {
                    var playerId = new PlayerId(pid);
                    missedPlayers.Add(playerId);
                    onMissedBeat.Fire(playerId);
                }
                
                nextBeatIndex[pid] = playerBeatIndex;
            }
            
            return missedPlayers;
        }

        public void Reset() {
            currentTime = timeOffset;
            for (int i = 0; i < nextBeatIndex.Length; i++) {
                nextBeatIndex[i] = 0;
            }
        }
    }
}
