using System.Collections.Generic;
using UnityEngine;

namespace Alice {
    public class BattleConfig : MonoBehaviour {
        [SerializeField] List<Transform> playerTransforms;
        [SerializeField] List<Striker> strikers;
        [SerializeField] List<StrikerPrefab> strikerEntries;
        [SerializeField] int roundsToWin = 2;

        public List<Transform> PlayerTransforms => playerTransforms;
        public List<Striker> Strikers => strikers;
        public List<StrikerPrefab> StrikerEntries => strikerEntries;
        public int RoundsToWin => roundsToWin;
    }
}