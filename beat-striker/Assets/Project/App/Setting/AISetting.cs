using System;
using System.Collections.Generic;
using R3;
using UnityEngine;

namespace Alice {
    [Serializable]
    public struct LearningCharacter {
        public Striker Striker;
        public AiBrain BrainPrefab;
    }

    public interface IAISetting {
        ReadOnlyReactiveProperty<bool> IsLearning { get; }
        ReadOnlyReactiveProperty<bool> UseSelfPlay { get; }
        Striker LearningPlayer1Striker { get; }
        AiBrain LearningPlayer1BrainPrefab { get; }
        IReadOnlyList<LearningCharacter> LearningOpponents { get; }
        Striker GetLearningOpponentStriker(int roundIndex);
        AiBrain GetLearningOpponentBrain(int roundIndex);
        void SetLearning(bool isLearning);
        void SetSelfPlay(bool useSelfPlay);
    }

    public class AISetting : MonoBehaviour, IAISetting {
        [SerializeField] bool isLearning;
        [Tooltip("ONにすると、学習時に2P側が1Pと同じキャラクターになり、セルフプレイ(AI同士の対戦)が有効になります。")]
        [SerializeField] bool useSelfPlay;
        [SerializeField] LearningCharacter learningPlayer1 = new() { Striker = Striker.Hero };
        [SerializeField] List<LearningCharacter> learningOpponents = new() { new() { Striker = Striker.Wizard } };

        readonly ReactiveProperty<bool> isLearningProperty = new(false);
        readonly ReactiveProperty<bool> useSelfPlayProperty = new(false);

        public ReadOnlyReactiveProperty<bool> IsLearning => isLearningProperty;
        public ReadOnlyReactiveProperty<bool> UseSelfPlay => useSelfPlayProperty;
        public Striker LearningPlayer1Striker => learningPlayer1.Striker;
        public AiBrain LearningPlayer1BrainPrefab => learningPlayer1.BrainPrefab;
        public IReadOnlyList<LearningCharacter> LearningOpponents => learningOpponents;

        void Awake() {
            isLearningProperty.OnNext(isLearning);
            useSelfPlayProperty.OnNext(useSelfPlay);
        }

        public Striker GetLearningOpponentStriker(int roundIndex) {
            if (learningOpponents == null || learningOpponents.Count == 0) {
                return learningPlayer1.Striker;
            }

            var normalizedIndex = ((roundIndex % learningOpponents.Count) + learningOpponents.Count) % learningOpponents.Count;
            return learningOpponents[normalizedIndex].Striker;
        }

        public AiBrain GetLearningOpponentBrain(int roundIndex) {
            if (learningOpponents == null || learningOpponents.Count == 0) {
                return null;
            }

            var normalizedIndex = ((roundIndex % learningOpponents.Count) + learningOpponents.Count) % learningOpponents.Count;
            return learningOpponents[normalizedIndex].BrainPrefab;
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
