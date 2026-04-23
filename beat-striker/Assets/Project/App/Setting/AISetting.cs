using System;
using System.Collections.Generic;
using R3;
using UnityEngine;

namespace Alice {
    [Serializable]
    public class LearningCharacter {
        [Tooltip("この対戦相手を選択候補に含めるかどうか。")]
        public bool Enabled = true;
        public Striker Striker;
        public AiBrain BrainPrefab;
        [Tooltip("選択確率の重み。大きいほど選ばれやすい。")]
        [Min(0.01f)]
        public float Weight = 1.0f;
    }

    public interface IAISetting {
        ReadOnlyReactiveProperty<bool> IsLearning { get; }
        ReadOnlyReactiveProperty<bool> UseSelfPlay { get; }
        Striker LearningPlayer1Striker { get; }
        AiBrain LearningPlayer1BrainPrefab { get; }
        IReadOnlyList<LearningCharacter> LearningOpponents { get; }
        float EmaSmoothing { get; }
        float EmaFloorScale { get; }

        /// <summary>
        /// 有効なOpponentから、EMAスケール済み重みに基づいてランダムに1つ選択する。
        /// </summary>
        /// <param name="weightScaleProvider">OpponentリストのインデックスからそのOpponentのEMAスケール(0-1)を返す関数</param>
        /// <returns>選択されたOpponentのインデックスと LearningCharacter のペア。有効なエントリが無い場合は null。</returns>
        (int Index, LearningCharacter Character)? GetWeightedRandomOpponent(Func<int, float> weightScaleProvider);

        void SetLearning(bool isLearning);
        void SetSelfPlay(bool useSelfPlay);
    }

    public class AISetting : MonoBehaviour, IAISetting {
        [SerializeField] bool isLearning;
        [Tooltip("ONにすると、学習時に2P側が1Pと同じキャラクターになり、セルフプレイ(AI同士の対戦)が有効になります。")]
        [SerializeField] bool useSelfPlay;
        [SerializeField] LearningCharacter learningPlayer1 = new() { Enabled = true, Striker = Striker.Hero, Weight = 1f };
        [SerializeField] List<LearningCharacter> learningOpponents = new() { new() { Enabled = true, Striker = Striker.Wizard, Weight = 1f } };

        [Header("Opponent Selection - EMA")]
        [Tooltip("指数移動平均の平滑化係数。1に近いほど過去の値を重視し、変化が緩やかになります。")]
        [SerializeField, Range(0f, 1f)] float emaSmoothing = 0.95f;
        [Tooltip("EMAスケールの下限値。勝率が低くても重みがこの値以下にならないようにします。")]
        [SerializeField, Range(0f, 1f)] float emaFloorScale = 0.3f;

        readonly ReactiveProperty<bool> isLearningProperty = new(false);
        readonly ReactiveProperty<bool> useSelfPlayProperty = new(false);
        bool initialized;

        public ReadOnlyReactiveProperty<bool> IsLearning => isLearningProperty;
        public ReadOnlyReactiveProperty<bool> UseSelfPlay => useSelfPlayProperty;
        public Striker LearningPlayer1Striker => learningPlayer1.Striker;
        public AiBrain LearningPlayer1BrainPrefab => learningPlayer1.BrainPrefab;
        public IReadOnlyList<LearningCharacter> LearningOpponents => learningOpponents;
        public float EmaSmoothing => emaSmoothing;
        public float EmaFloorScale => emaFloorScale;

        void Awake() {
            InitializeDefaults();
        }

        public void InitializeDefaults() {
            if (initialized) {
                return;
            }

            isLearningProperty.OnNext(isLearning);
            useSelfPlayProperty.OnNext(useSelfPlay);
            initialized = true;
        }

        public (int Index, LearningCharacter Character)? GetWeightedRandomOpponent(Func<int, float> weightScaleProvider) {
            if (learningOpponents == null || learningOpponents.Count == 0) {
                return null;
            }

            // 有効なエントリのEMAスケール済み重みを計算
            float totalWeight = 0f;
            Span<float> scaledWeights = stackalloc float[learningOpponents.Count];

            for (int i = 0; i < learningOpponents.Count; i++) {
                if (!learningOpponents[i].Enabled) {
                    scaledWeights[i] = 0f;
                    continue;
                }

                var emaScale = weightScaleProvider(i);
                var scaledWeight = learningOpponents[i].Weight * Mathf.Max(0f, emaScale);
                scaledWeights[i] = scaledWeight;
                totalWeight += scaledWeight;
            }

            if (totalWeight <= 0f) {
                return null;
            }

            // 重み付きランダム選択
            var randomValue = UnityEngine.Random.Range(0f, totalWeight);
            float cumulative = 0f;

            for (int i = 0; i < learningOpponents.Count; i++) {
                cumulative += scaledWeights[i];
                if (randomValue <= cumulative) {
                    return (i, learningOpponents[i]);
                }
            }

            // 浮動小数点誤差のフォールバック: 最後の有効エントリを返す
            for (int i = learningOpponents.Count - 1; i >= 0; i--) {
                if (scaledWeights[i] > 0f) {
                    return (i, learningOpponents[i]);
                }
            }

            return null;
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
