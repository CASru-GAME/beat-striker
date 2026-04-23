using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Policies;
using Unity.MLAgents.Sensors;
using Unity.InferenceEngine;
using UnityEngine;

namespace Alice {
    [DefaultExecutionOrder(-1000)]
    public partial class MLAiBrain : AiBrain {
        internal const int STRIKER_TYPE_COUNT = 4;
        internal const int MOVE_DIRECTION_BRANCH_SIZE = 9;
        internal const int BUTTON_ACTION_BRANCH_SIZE = 4;
        internal const int BEAT_STACK_COUNT = 4;
        internal const int OBSERVATION_COUNT = 74;
        internal const int OBSERVATION_STACK_COUNT = 1;
        internal const int STACKED_OBSERVATION_COUNT = OBSERVATION_COUNT * OBSERVATION_STACK_COUNT;

        protected override AiAction OnGoodWindow(AiObservation observation) {
            ConfigureTeamId(observation.Self.PlayerId.CurrentValue);
            EnsureStateCategorySubscriptions(observation);
            UpdateBeatFeatureHistory(observation);
            UpdateStateTransitionWindows();
            decisionAgent.SetObservation(observation);
            decisionAgent.RequestDecisionNow();

            var action = decisionAgent.CurrentAction;
            EvaluateAndReward(observation, action);
            return action;
        }
    }
}
