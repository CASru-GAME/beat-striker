
using System.Collections.Generic;
using Core.App.Types;
using UnityEngine;

namespace Core.Battle {
    
    public class RythmTrackModel : IRythmTrackModel {
        private float currentTime = 0f;
        private float[] beatTimes;
        private float excellentWindow;
        private float goodWindow;
        private int[] nextBeatIndex = new int[4];

        public RythmTrackModel(float[] beatTimes, float perfectWindow, float goodWindow, float timeOffset) {
            this.beatTimes = beatTimes;
            this.excellentWindow = perfectWindow;
            this.goodWindow = goodWindow;
            this.currentTime = timeOffset;
        }

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

        public void AddTime(float dt) {
            currentTime += dt;
            for (int pid = 0; pid < nextBeatIndex.Length; pid++) {
                int playerBeatIndex = nextBeatIndex[pid];
                while (true) {
                    if (playerBeatIndex >= beatTimes.Length) break;
                    if (beatTimes[playerBeatIndex] > currentTime - goodWindow) break;
                    playerBeatIndex++;
                }
                nextBeatIndex[pid] = playerBeatIndex;
            }
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
    }
}