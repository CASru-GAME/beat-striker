using System.Collections.Generic;
using R3;
using UnityEngine;

namespace Alice {
    public interface IAISetting {
        ReadOnlyReactiveProperty<bool> IsLearning { get; }
        ReadOnlyReactiveProperty<bool> UseSelfPlay { get; }
        Striker LearningPlayer1Striker { get; }
        IReadOnlyList<Striker> LearningOpponentStrikers { get; }
        Striker GetLearningOpponentStriker(int roundIndex);
        void SetLearning(bool isLearning);
        void SetSelfPlay(bool useSelfPlay);
    }

    public class AISetting : MonoBehaviour, IAISetting {
        [SerializeField] bool isLearning;
        [Tooltip("ONにすると、学習時に2P側が1Pと同じキャラクターになり、セルフプレイ(AI同士の対戦)が有効になります。")]
        [SerializeField] bool useSelfPlay;
        [SerializeField] Striker learningPlayer1Striker = Striker.Hero;
        [SerializeField] List<Striker> learningOpponentStrikers = new() { Striker.Wizard };

        readonly ReactiveProperty<bool> isLearningProperty = new(false);
        readonly ReactiveProperty<bool> useSelfPlayProperty = new(false);

        public ReadOnlyReactiveProperty<bool> IsLearning => isLearningProperty;
        public ReadOnlyReactiveProperty<bool> UseSelfPlay => useSelfPlayProperty;
        public Striker LearningPlayer1Striker => learningPlayer1Striker;
        public IReadOnlyList<Striker> LearningOpponentStrikers => learningOpponentStrikers;

        void Awake() {
            isLearningProperty.OnNext(isLearning);
            useSelfPlayProperty.OnNext(useSelfPlay);
        }

        public Striker GetLearningOpponentStriker(int roundIndex) {
            if (learningOpponentStrikers.Count == 0) {
                return learningPlayer1Striker;
            }

            var normalizedIndex = ((roundIndex % learningOpponentStrikers.Count) + learningOpponentStrikers.Count) % learningOpponentStrikers.Count;
            return learningOpponentStrikers[normalizedIndex];
        }

        public void SetLearning(bool isLearning) {
            this.isLearning = isLearning;
            isLearningProperty.OnNext(isLearning);
        }

        public void SetSelfPlay(bool useSelfPlay) {
            this.useSelfPlay = useSelfPlay;
            useSelfPlayProperty.OnNext(useSelfPlay);
        }

        void OnDestroy() {
            isLearningProperty.Dispose();
            useSelfPlayProperty.Dispose();
        }
    }
}
