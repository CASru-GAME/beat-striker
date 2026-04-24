using System;
using System.Collections.Generic;
using System.IO;
using Unity.InferenceEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Demonstrations;
using Unity.MLAgents.Policies;
using Unity.MLAgents.Sensors;
using UnityEngine;

namespace Alice {
    public partial class MLAiBrain {
        [Header("ML-Agents Auto Setup")]
        [Tooltip("ML-AgentsのBehavior Name。ML-Agents側の設定と一致させる必要があります。")]
        [SerializeField] string behaviorName = "Satan";
        [Tooltip("Awake時にML-Agentsコンポーネント(BehaviorParameters等)を自動設定するかどうか。")]
        [SerializeField] bool autoConfigureOnAwake = true;
        [Tooltip("エディタ実行時にも自動セットアップロジックを動作させるかのフラグ。")]
        [SerializeField] bool autoConfigureInEditor = true;

        [Header("ML-Agents Mode")]
        [Tooltip("ONNX形式の学習済み推論モデルアセット。推論モードでAIを動作させる際に使用します。")]
        [SerializeField] ModelAsset inferenceOnnx;

        [Header("Observation Scale")]
        [Tooltip("敵との距離をObservationとして正規化(-1~1)する際の基準値。実際の距離をこの値で除算します。")]
        [SerializeField] float distanceObservationScale = 40f;
        [Tooltip("近距離用の距離観測スケール値。distanceObservationScaleより小さい値を設定することで、近距離での距離変化に敏感な観測値になります。")]
        [SerializeField] float closeDistanceObservationScale = 10f;
        [Tooltip("初期Y座標からのY方向変位をObservationとして正規化(-1~1)する際の基準値。")]
        [SerializeField] float yDisplacementObservationScale = 5f;
        [Tooltip("1ビートあたりの移動量をObservationとして正規化(-1~1)する際の基準値。")]
        [SerializeField] float beatMoveMagnitudeObservationScale = 7f;
        [Tooltip("AIのチャージ回数をObservationとして0~1に正規化するための最大回数の上限値。")]
        [SerializeField, Min(1)] int chargeCountObservationCap = 4;

        [Header("Reward - Base")]
        [Tooltip("毎ステップ(毎ビート)与えられる基本ペナルティ。無駄な行動や遅延を防ぐために負の値を設定します。")]
        [SerializeField] float stepPenalty = -0.001f;
        [Tooltip("敵にダメージを与えた際に無条件で与えられる固定の報酬値。攻撃を当てること自体を評価します。")]
        [SerializeField] float dealtDamageFixedReward = 0.07f;
        [Tooltip("与えたダメージ量に応じて与えられる報酬のスケール値。HPからの計算量をさらにスケールします。")]
        [SerializeField] float dealtDamageRewardScale = 0.07f;
        [Tooltip("直近の攻撃命中割合(Damage Rate)に比例して与えられる報酬のスケール。連続で当てるほど報酬増。")]
        [SerializeField] float dealtDamageHitRateRewardScale = 0.07f;
        [Tooltip("自身が受けたダメージ量に比例して与えられるペナルティのスケール。被弾を回避する行動を促します。")]
        [SerializeField] float receivedDamagePenaltyScale = 0.10f;

        [Header("Reward - HP Scale")]
        [Tooltip("HP増減量からダメージ報酬を計算する際に、HPをスケールダウンして評価対象のダメージ量にする基準値。")]
        [SerializeField, Min(0.0001f)] float hpRewardScale = 10f;

        [Header("Reward - Timing")]
        [Tooltip("敵の攻撃を直近で回避し、被弾しなかった場合に与えられる報酬。防御や回避行動の成功を評価します。")]
        [SerializeField] float punishAvoidedReward = 0.05f;
        [Tooltip("ダッシュ状態へ移行した際に無条件で与えられる固定報酬。ダッシュを選択する行動を促進します。")]
        [SerializeField] float enteredDashFixedReward = 0.01f;
        [Tooltip("上方向(Y+)への移動でダッシュ状態へ移行した際に追加で与えられる報酬。ジャンプダッシュ行動を促します。")]
        [SerializeField] float upwardDashReward = 0.01f;
        [Tooltip("攻撃状態へ移行した際に無条件で与えられる固定ペナルティ。無闇な攻撃選択を抑制します。")]
        [SerializeField] float enteredAttackFixedPenalty = -0.01f;
        [Tooltip("自分が攻撃を開始したのに直近2ビートで敵にダメージを与えられなかった（空振り等）場合のペナルティ。")]
        [SerializeField] float attackNoDamage2BeatPenalty = -0.03f;
        [Tooltip("敵が最近攻撃していないにもかかわらず、無駄にガード状態へ移行した場合に与えられるペナルティ。")]
        [SerializeField] float unnecessaryGuardPenalty = -0.06f;

        [Header("Reward - Charge")]
        [Tooltip("短期間にこの回数以上チャージすると使いすぎと判定されペナルティが発生する閾値の回数。")]
        [SerializeField, Min(1)] int chargeOveruseThreshold = 4;
        [Tooltip("チャージ行動を短期間に使いすぎた(閾値を超えた)場合に与えられるペナルティ。無駄な連打を防ぐ。")]
        [SerializeField] float chargeOverusePenalty = -0.03f;
        [Tooltip("チャージ回数のカウントがリセットされるまでの非チャージビート数。この期間チャージしなければ初期化。")]
        [SerializeField, Min(1)] int chargeAutoResetAfterBeats = 4;

        [Header("Reward - Distance Control")]
        [Tooltip("敵との理想的な最小距離の閾値。これより近づきすぎた場合、距離変化に応じてペナルティや報酬を適用します。")]
        [SerializeField] float minPreferredDistance = 0.8f;
        [Tooltip("理想の最小距離よりも敵に近づきすぎてしまった場合に与えられるペナルティ。不用意な接近を抑制します。")]
        [SerializeField] float tooCloseApproachPenalty = -0.01f;
        [Tooltip("敵に近づきすぎた状態から距離を取る(後退する)行動をした場合に与えられる報酬。適切な距離維持を学習。")]
        [SerializeField] float tooCloseRetreatReward = 0.008f;
        [Tooltip("敵との距離がこの閾値以上のとき、敵に近づく行動に対して報酬を与える基準距離。")]
        [SerializeField] float farApproachDistance = 5f;
        [Tooltip("遠距離から敵に近づいた場合に与えられる報酬。積極的なアプローチを促進します。")]
        [SerializeField] float farApproachReward = 0.01f;
        [Tooltip("遠距離にいるにも関わらず敵から更に遠ざかった場合に与えられるペナルティ。遠距離での離脱行動を抑制します。")]
        [SerializeField] float farRetreatPenalty = -0.01f;
        [Tooltip("遠距離キャラクター用：この距離以内にいる場合、距離維持報酬を適用する閾値。")]
        [SerializeField] float keepDistanceThreshold = 20f;
        [Tooltip("遠距離キャラクター用：閾値内で敵から遠ざかった場合に与えられる報酬。")]
        [SerializeField] float keepDistanceRetreatReward = 0f;
        [Tooltip("遠距離キャラクター用：閾値内で敵に近づいた場合に与えられるペナルティ。")]
        [SerializeField] float keepDistanceApproachPenalty = 0f;

        [Header("Reward - Movement")]
        [Tooltip("移動量の平均を算出するために記録する、過去の直近ビート数（ウィンドウサイズ）。")]
        [SerializeField, Min(1)] int movementAverageWindowBeats = 10;
        [Tooltip("ペナルティを免除される十分な平均移動量の閾値。これを上回っていれば動きが十分あるとみなします。")]
        [SerializeField] float movementAverageThreshold = 1.1f;
        [Tooltip("現在の移動量が過去の平均移動量を下回り、かつ平均移動量も閾値以下の場合のペナルティ。立ち止まり防止。")]
        [SerializeField] float movementBelowAveragePenalty = -0.02f;
        [Tooltip("全体的に動きが少ない状況で、平均を上回る移動をした場合に与えられる報酬。より活発な動きを促します。")]
        [SerializeField] float movementAboveAverageReward = 0.015f;

        [Header("Reward - Damage Rate")]
        [Tooltip("攻撃命中率（ダメージを与えたフラグ）の平均を計算するために保持する、過去の直近ビート数。")]
        [SerializeField, Min(3)] int damageHitAverageWindowBeats = 10;
        [Tooltip("一定期間の攻撃命中率の閾値。この割合以下だと低空飛行状態とみなされペナルティの対象となります。")]
        [SerializeField, Range(0f, 1f)] float damageHitAverageThreshold = 0.1f;
        [Tooltip("直近3ビートで全くダメージを与えられず、かつ全体の命中率も閾値以下の場合に与えられるペナルティ。")]
        [SerializeField] float noDamageLast3BeatsPenalty = -0.05f;

        [Header("Reward - Episode End")]
        [Tooltip("エピソード終了時に付与する報酬のスケール値。カーブ(自分HP割合 - 相手HP割合) × 時間ボーナスにこの値を掛けます。")]
        [SerializeField] float episodeEndRewardScale = 3.0f;
        [Tooltip("エピソード終了時のHP差(-1~1)を報酬値にリマップするカーブ。デフォルトは線形(そのまま)。")]
        [SerializeField] AnimationCurve episodeEndHpCurve = AnimationCurve.Linear(-1f, -1f, 1f, 1f);
        [Tooltip("エピソード終了時の時間ボーナス計算に使うステップ数スケール。max(0, 1 - ステップ数/この値) で時間ボーナスを算出します。")]
        [SerializeField, Min(1f)] float episodeEndStepScale = 5000f;

        MLAiDecisionAgent decisionAgent;
        BehaviorParameters behaviorParameters;
        DemonstrationRecorder demonstrationRecorder;
        bool isLearningMode = true;
        bool isDemonstrationRecordingEnabled;
        bool hasRecordedDemonstrationAction;
        AiAction recordedDemonstrationAction = AiAction.None;
        string demonstrationNameBase = "Player1Demo";
        string demonstrationRoundDirectory;
        string demonstrationTempName;
        bool demonstrationRoundActive;
        string demonstrationRoundPlayer1StrikerName = "Unknown";
        string demonstrationRoundPlayer2StrikerName = "Unknown";
        string demonstrationRoundAiBrainPrefabName = "Unknown";
        string demonstrationSessionTimestamp;
        float? previousSelfHp;
        float? previousOpponentHp;
        bool hasPreviousPositions;
        Vector3 previousSelfPosition;
        Vector3 previousOpponentPosition;
        float? previousDistance;
        float? initialSelfY;
        readonly Vector2[] selfMoveDirectionLocalHistory = new Vector2[BEAT_STACK_COUNT];
        readonly Vector2[] opponentMoveDirectionLocalHistory = new Vector2[BEAT_STACK_COUNT];
        readonly float[] selfMoveMagnitudeHistory = new float[BEAT_STACK_COUNT];
        readonly float[] selfMoveWorldYHistory = new float[BEAT_STACK_COUNT];
        readonly float[] opponentMoveMagnitudeHistory = new float[BEAT_STACK_COUNT];
        readonly StateTransitionFlags[] selfStateTransitionHistory = new StateTransitionFlags[BEAT_STACK_COUNT];
        readonly StateTransitionFlags[] opponentStateTransitionHistory = new StateTransitionFlags[BEAT_STACK_COUNT];
        readonly bool[] selfDamagedHistory = new bool[BEAT_STACK_COUNT];
        readonly bool[] opponentDamagedHistory = new bool[BEAT_STACK_COUNT];
        readonly Queue<float> movementAverageWindow = new();
        readonly Queue<bool> damageHitAverageWindow = new();
        int aiChargeCount;
        int beatsSinceLastCharge;
        IObservableStriker observedSelfStriker;
        IObservableStriker observedOpponentStriker;
        IDisposable selfStateCategorySubscription;
        IDisposable opponentStateCategorySubscription;
        StrikerStateCategory? previousSelfObservedStateCategory;
        StrikerStateCategory? previousOpponentObservedStateCategory;
        bool selfEnteredDashSinceLastBeat;
        bool selfEnteredAttackSinceLastBeat;
        bool selfEnteredChargeSinceLastBeat;
        bool selfEnteredGuardSinceLastBeat;
        bool selfEnteredElseSinceLastBeat;
        bool opponentEnteredDashSinceLastBeat;
        bool opponentEnteredAttackSinceLastBeat;
        bool opponentEnteredChargeSinceLastBeat;
        bool opponentEnteredGuardSinceLastBeat;
        bool opponentEnteredElseSinceLastBeat;

        void Awake() {
            behaviorParameters = EnsureRuntimeBehaviorParameters();

            if (autoConfigureOnAwake) {
                ConfigureMlAgentComponents();
            }

            decisionAgent = EnsureRuntimeDecisionAgent();
            decisionAgent.Bind(this);
        }

        protected override void OnAiEnabled() {
            ResetRuntimeState();
        }

        protected override void OnAiDisabled() {
            ResetRuntimeState();
            hasRecordedDemonstrationAction = false;
            recordedDemonstrationAction = AiAction.None;
            DisposeStateCategorySubscriptions();
        }

        protected override void OnLearningModeChanged(bool isLearning) {
            isLearningMode = isLearning;
            if (behaviorParameters != null) {
                ConfigureMlAgentComponents();
            }
        }

        protected override void OnDestroy() {
            if (demonstrationRecorder != null) {
                demonstrationRecorder.Record = false;
                demonstrationRecorder.Close();
            }
            DisposeStateCategorySubscriptions();
            base.OnDestroy();
        }

        public override void ConfigureDemonstrationRecording(bool isRecording, string demonstrationNameBase, int playerId) {
            this.demonstrationNameBase = string.IsNullOrWhiteSpace(demonstrationNameBase) ? "Player1Demo" : demonstrationNameBase.Trim();
            isDemonstrationRecordingEnabled = isRecording && playerId == 0;
            if (!isDemonstrationRecordingEnabled) {
                demonstrationSessionTimestamp = null;
            }

            ConfigureDemonstrationRecorder();
            if (behaviorParameters != null) {
                ConfigureMlAgentComponents();
            }
        }

        public override void RecordDemonstrationAction(AiAction action) {
            if (!isDemonstrationRecordingEnabled) {
                return;
            }

            recordedDemonstrationAction = action;
            hasRecordedDemonstrationAction = true;
        }

        public override void BeginRoundEpisode(int roundNumber, string player1StrikerName, string player2StrikerName, string aiBrainPrefabName) {
            if (!isDemonstrationRecordingEnabled) {
                return;
            }

            if (string.IsNullOrEmpty(demonstrationSessionTimestamp)) {
                demonstrationSessionTimestamp = DateTime.Now.ToString("yyyy-MM-dd-hh-mm-ss");
            }

            demonstrationRoundDirectory = GetDemonstrationRoundDirectoryPath();
            demonstrationTempName = $"Recording-{roundNumber}";
            demonstrationRoundPlayer1StrikerName = string.IsNullOrWhiteSpace(player1StrikerName) ? "Unknown" : player1StrikerName;
            demonstrationRoundPlayer2StrikerName = string.IsNullOrWhiteSpace(player2StrikerName) ? "Unknown" : player2StrikerName;
            demonstrationRoundAiBrainPrefabName = string.IsNullOrWhiteSpace(aiBrainPrefabName) ? "Unknown" : aiBrainPrefabName;
            demonstrationRoundActive = true;

            ConfigureDemonstrationRecorder(demonstrationRoundDirectory, demonstrationTempName, true);
        }

        public override void CompleteRoundEpisode(int roundNumber, bool player1Win) {
            if (!isDemonstrationRecordingEnabled || !demonstrationRoundActive) {
                return;
            }

            var sourceDirectory = demonstrationRoundDirectory;
            var sourceName = demonstrationTempName;
            ConfigureDemonstrationRecorder(sourceDirectory, sourceName, false);

            if (string.IsNullOrEmpty(sourceDirectory) || string.IsNullOrEmpty(sourceName)) {
                demonstrationRoundActive = false;
                return;
            }

            var sourcePath = Path.Combine(sourceDirectory, $"{sourceName}.demo");
            if (!File.Exists(sourcePath)) {
                demonstrationRoundActive = false;
                return;
            }

            var outcomeText = player1Win ? "Win" : "Lose";
            var finalizedName = BuildFinalDemonstrationName(roundNumber, demonstrationRoundPlayer1StrikerName, demonstrationRoundPlayer2StrikerName, demonstrationRoundAiBrainPrefabName, outcomeText);
            var destinationPath = Path.Combine(sourceDirectory, $"{finalizedName}.demo");
            if (File.Exists(destinationPath)) {
                File.Delete(destinationPath);
            }

            File.Move(sourcePath, destinationPath);
            demonstrationRoundActive = false;
        }

        void ConfigureTeamId(int playerId) {
            behaviorParameters.TeamId = playerId & 1;
        }

        void ConfigureMlAgentComponents() {
            if (behaviorParameters == null) {
                return;
            }

            var brainParameters = behaviorParameters.BrainParameters;

            brainParameters.VectorObservationSize = STACKED_OBSERVATION_COUNT;
            brainParameters.NumStackedVectorObservations = 1;
            brainParameters.ActionSpec = new ActionSpec(0, new[] { BUTTON_ACTION_BRANCH_SIZE, MOVE_DIRECTION_BRANCH_SIZE });

            behaviorParameters.BehaviorName = behaviorName;

            if (isDemonstrationRecordingEnabled) {
                behaviorParameters.BehaviorType = BehaviorType.HeuristicOnly;
                behaviorParameters.Model = null;
            }
            else if (isLearningMode) {
                behaviorParameters.BehaviorType = BehaviorType.Default;
                behaviorParameters.Model = null;
            }
            else {
                if (inferenceOnnx != null) {
                    behaviorParameters.Model = inferenceOnnx;
                    behaviorParameters.BehaviorType = BehaviorType.InferenceOnly;
                }
                else {
                    behaviorParameters.BehaviorType = BehaviorType.HeuristicOnly;
                    behaviorParameters.Model = null;
                }
            }
        }

        void ConfigureDemonstrationRecorder() {
            if (isDemonstrationRecordingEnabled) {
                demonstrationRecorder = GetComponent<DemonstrationRecorder>();
                if (demonstrationRecorder == null) {
                    demonstrationRecorder = gameObject.AddComponent<DemonstrationRecorder>();
                    demonstrationRecorder.hideFlags = HideFlags.HideInInspector;
                }

                ConfigureDemonstrationRecorder(GetDemonstrationRoundDirectoryPath(), $"{demonstrationNameBase}_{DateTime.Now:yyyyMMdd_HHmmss}", false);
                return;
            }

            if (demonstrationRecorder == null) {
                return;
            }

            demonstrationRecorder.Record = false;
            demonstrationRecorder.Close();
        }

        void ConfigureDemonstrationRecorder(string directoryPath, string demoName, bool record) {
            demonstrationRecorder = GetComponent<DemonstrationRecorder>();
            if (demonstrationRecorder == null) {
                demonstrationRecorder = gameObject.AddComponent<DemonstrationRecorder>();
                demonstrationRecorder.hideFlags = HideFlags.HideInInspector;
            }

            demonstrationRecorder.Record = false;
            demonstrationRecorder.Close();
            demonstrationRecorder.DemonstrationDirectory = directoryPath;
            demonstrationRecorder.DemonstrationName = demoName;
            demonstrationRecorder.Record = record;
        }

        static string BuildFinalDemonstrationName(int roundNumber, string player1StrikerName, string player2StrikerName, string aiBrainPrefabName, string outcomeText) {
            return SanitizeFileName($"Demo-{roundNumber}-{player1StrikerName}-vs-{player2StrikerName}-{aiBrainPrefabName}-{outcomeText}");
        }

        static string SanitizeFileName(string value) {
            var invalidChars = Path.GetInvalidFileNameChars();
            var sanitized = value;
            for (var i = 0; i < invalidChars.Length; i++) {
                sanitized = sanitized.Replace(invalidChars[i], '-');
            }

            return sanitized;
        }

        string GetDemonstrationRoundDirectoryPath() {
            var timestamp = string.IsNullOrEmpty(demonstrationSessionTimestamp)
                ? DateTime.Now.ToString("yyyy-MM-dd-hh-mm-ss")
                : demonstrationSessionTimestamp;
            var projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            if (string.IsNullOrEmpty(projectRoot)) {
                return Path.Combine("Dist", "Demonstration", timestamp);
            }

            return Path.Combine(projectRoot, "Dist", "Demonstration", timestamp);
        }

        BehaviorParameters EnsureRuntimeBehaviorParameters() {
            var runtimeBehaviorParameters = GetComponent<BehaviorParameters>();
            if (runtimeBehaviorParameters != null) {
                return runtimeBehaviorParameters;
            }

            runtimeBehaviorParameters = gameObject.AddComponent<BehaviorParameters>();
            runtimeBehaviorParameters.hideFlags = HideFlags.HideInInspector;
            return runtimeBehaviorParameters;
        }

        MLAiDecisionAgent EnsureRuntimeDecisionAgent() {
            var runtimeDecisionAgent = GetComponent<MLAiDecisionAgent>();
            if (runtimeDecisionAgent != null) {
                return runtimeDecisionAgent;
            }

            runtimeDecisionAgent = gameObject.AddComponent<MLAiDecisionAgent>();
            runtimeDecisionAgent.hideFlags = HideFlags.HideInInspector;
            return runtimeDecisionAgent;
        }
    }
}