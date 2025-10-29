
using System.Collections.Generic;
using System.Linq;
using Core.App.Types;

namespace Core.Battle {
    public class BattleModel : IBattleModel {

        private readonly List<PlayerId>[] deadPlayers;
        private readonly List<PlayerId> allPlayers;
        private int currentRound = 0;

        public BattleModel(int playerCount) {
            this.allPlayers = Enumerable.Range(0, playerCount).Select(i => new PlayerId(i)).ToList();
            deadPlayers = new List<PlayerId>[10];
            for (int i = 0; i < deadPlayers.Length; i++) {
                deadPlayers[i] = new List<PlayerId>(this.allPlayers.Count);
            }
        }

        public PlayerId GetWinner(int round) {
            var dead = deadPlayers[round];
            var res = allPlayers.FirstOrDefault(p => !dead.Contains(p));
            return res == null ? allPlayers[0] : res;
        }

        public int GetCurrentRound() {
            return currentRound;
        }

        public void NextRound() {
            currentRound++;
        }

        public bool IsFinished() {
            var winCounts = GetWinCounts();
            
            foreach (var kvp in winCounts) {
                if (kvp.Value >= 2) {
                    return true; 
                }
            }
            
            return false;
        }
        
        public PlayerId GetFinalWinner() {
            var winCounts = GetWinCounts();
            
            foreach (var kvp in winCounts) {
                if (kvp.Value >= 2) {
                    return kvp.Key; 
                }
            }

            return GetWinner(currentRound);
        }

        private Dictionary<PlayerId, int> GetWinCounts() {
            var winCounts = new Dictionary<PlayerId, int>();
            
            for (int r = 0; r <= currentRound; r++) {
                // そのラウンドで誰かが死んでいる(=決着済み)場合のみカウント
                if (deadPlayers[r].Count == 0) {
                    continue;
                }
                
                PlayerId winner = GetWinner(r);
                if (!winCounts.ContainsKey(winner)) {
                    winCounts[winner] = 0;
                }
                winCounts[winner]++;
            }
            
            return winCounts;
        }

        public void AddLoser(PlayerId playerId) {
            if (!deadPlayers[currentRound].Contains(playerId)) {
                deadPlayers[currentRound].Add(playerId);
            }
        }
    }
}