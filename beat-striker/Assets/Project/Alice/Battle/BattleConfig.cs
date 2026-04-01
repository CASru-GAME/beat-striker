using System.Collections.Generic;
using UnityEngine;

namespace Alice {
    public class BattleConfig : MonoBehaviour {
        [SerializeField] List<Transform> playerTransforms;
        [SerializeField] List<Striker> strikers;
        [SerializeField] List<StrikerPrefab> strikerEntries;
        [SerializeField] int roundsToWin = 2;
        [SerializeField] float combo1SpecialPointGain = 4f;
        [SerializeField] float specialPointGainConvergenceRate = 0.2f;
        [SerializeField] float specialPointGainConvergenceValue = 10f;

        public List<Transform> PlayerTransforms => playerTransforms;
        public List<Striker> Strikers => strikers;
        public List<StrikerPrefab> StrikerEntries => strikerEntries;
        public int RoundsToWin => roundsToWin;
        public float Combo1SpecialPointGain => combo1SpecialPointGain;
        public float SpecialPointGainConvergenceRate => specialPointGainConvergenceRate;
        public float SpecialPointGainConvergenceValue => specialPointGainConvergenceValue;
    }
}