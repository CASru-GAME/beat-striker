using System;
using System.Collections.Generic;
using R3;
using UnityEngine;

namespace Alice {
    public enum AiPlayMode {
        Inference,
        Test,
        Record,
        Learning,
        LearningSelfPlay,
    }

    [Serializable]
    public class LearningPlayer1Character {
        public Striker Striker;
    }

    [Serializable]
    public class LearningCharacter {
        [Tooltip("この対戦相手を選択候補に含めるかどうか。")]
        public bool Enabled = true;
        public Striker Striker;
        [Tooltip("指定時はこのブレインを優先して使用。未指定ならAIRegistryから自動解決します。")]
        public AiBrain BrainPrefab;
        [Tooltip("選択確率の重み。大きいほど選ばれやすい。")]
        [Min(0.01f)]
        public float Weight = 1.0f;
    }

    public interface IAISetting {
        ReadOnlyReactiveProperty<AiPlayMode> Mode { get; }
        bool IsInfiniteRoundMode { get; }
        bool UsesVirtualPlaybackClock { get; }
        bool EnablesAgentLearning { get; }
        bool IsDemonstrationRecordingMode { get; }
        bool UsesAiSettingStrikerSelection { get; }
        bool UsesSelfPlayOpponentSelection { get; }
        bool UsesLearningOpponentPool { get; }
        bool UsesFixedTestOpponentSequence { get; }
        string DemonstrationName { get; }
        Striker LearningPlayer1Striker { get; }
        IReadOnlyList<LearningCharacter> LearningOpponents { get; }
        AiActionSequenceItem TestOpponentSequence { get; }
        float EmaSmoothing { get; }
        float EmaFloorScale { get; }

        /// <summary>
        /// 有効なOpponentから、EMAスケール済み重みに基づいてランダムに1つ選択する。
        /// </summary>
        /// <param name="weightScaleProvider">OpponentリストのインデックスからそのOpponentのEMAスケール(0-1)を返す関数</param>
        /// <returns>選択されたOpponentのインデックスと LearningCharacter のペア。有効なエントリが無い場合は null。</returns>
        (int Index, LearningCharacter Character)? GetWeightedRandomOpponent(Func<int, float> weightScaleProvider);

        void SetMode(AiPlayMode mode);
    }

    public class AISetting : MonoBehaviour, IAISetting {
        [Tooltip("AI実行モード。推論/レコード/学習/セルフプレイ学習を切り替えます。")]
        [SerializeField] AiPlayMode mode = AiPlayMode.Inference;
        [Tooltip("Demonstration名のベース文字列。保存時にタイムスタンプが自動付与されます。")]
        [SerializeField] string demonstrationName = "Player1Demo";
        [SerializeField] LearningPlayer1Character learningPlayer1 = new() { Striker = Striker.Hero };
        [SerializeField] List<LearningCharacter> learningOpponents = new() { new() { Enabled = true, Striker = Striker.Wizard, Weight = 1f } };
        [SerializeField] AiActionSequenceItem testOpponentSequence = new() { IsRandomSequence = false };

        [Tooltip("指数移動平均の平滑化係数。1に近いほど過去の値を重視し、変化が緩やかになります。")]
        [SerializeField, Range(0f, 1f)] float emaSmoothing = 0.95f;
        [Tooltip("EMAスケールの下限値。勝率が低くても重みがこの値以下にならないようにします。")]
        [SerializeField, Range(0f, 1f)] float emaFloorScale = 0.3f;

        [SerializeField]
        string buildPath = "FighterAI.exe"; // serializedObject.FindProperty("buildPath") で使います

        readonly ReactiveProperty<AiPlayMode> modeProperty = new(AiPlayMode.Inference);
        bool initialized;

        public ReadOnlyReactiveProperty<AiPlayMode> Mode => modeProperty;
        public bool IsInfiniteRoundMode => mode is AiPlayMode.Test or AiPlayMode.Record or AiPlayMode.Learning or AiPlayMode.LearningSelfPlay;
        public bool UsesVirtualPlaybackClock => mode is AiPlayMode.Record or AiPlayMode.Learning or AiPlayMode.LearningSelfPlay;
        public bool EnablesAgentLearning => mode is AiPlayMode.Learning or AiPlayMode.LearningSelfPlay;
        public bool IsDemonstrationRecordingMode => mode == AiPlayMode.Record;
        public bool UsesAiSettingStrikerSelection => mode is AiPlayMode.Record or AiPlayMode.Learning or AiPlayMode.LearningSelfPlay;
        public bool UsesSelfPlayOpponentSelection => mode == AiPlayMode.LearningSelfPlay;
        public bool UsesLearningOpponentPool => mode is AiPlayMode.Record or AiPlayMode.Learning;
        public bool UsesFixedTestOpponentSequence => mode == AiPlayMode.Test;
        public string DemonstrationName => demonstrationName;
        public Striker LearningPlayer1Striker => learningPlayer1.Striker;
        public IReadOnlyList<LearningCharacter> LearningOpponents => learningOpponents;
        public AiActionSequenceItem TestOpponentSequence => testOpponentSequence;
        public float EmaSmoothing => emaSmoothing;
        public float EmaFloorScale => emaFloorScale;

        void Awake() {
            InitializeDefaults();
        }

        public void InitializeDefaults() {
            if (initialized) {
                return;
            }

            modeProperty.OnNext(mode);
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

        public void SetMode(AiPlayMode mode) {
            this.mode = mode;
            modeProperty.OnNext(mode);
        }

        void OnDestroy() {
            modeProperty.Dispose();
        }
    }
}
