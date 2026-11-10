using Alice;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Alice {
    public interface IStrikerFactory {
        IStrikerHub Create(StrikerHub prefab, Transform playerTransform, int playerId);
    }

    public class StrikerHubFactory : IStrikerFactory {
        readonly IStrikerRegistry strikerRegistry;
        readonly IBattleOnlineSync battleOnlineSync;
        readonly ITutorialSetting tutorialSetting;
        readonly IBattleSetting battleSetting;

        [Inject]
        public StrikerHubFactory(IStrikerRegistry strikerRegistry, IBattleOnlineSync battleOnlineSync, ITutorialSetting tutorialSetting, IBattleSetting battleSetting) {
            this.strikerRegistry = strikerRegistry;
            this.battleOnlineSync = battleOnlineSync;
            this.tutorialSetting = tutorialSetting;
            this.battleSetting = battleSetting;
        }

        public IStrikerHub Create(StrikerHub prefab, Transform playerTransform, int playerId) {
            var instance = Object.Instantiate(prefab);
            instance.transform.SetPositionAndRotation(playerTransform.position, playerTransform.rotation);
            instance.Rigidbody.position = playerTransform.position;
            instance.Rigidbody.rotation = playerTransform.rotation;
            instance.ConfigureStageBoundary(battleSetting.MovementBounds);
            playerTransform.SetParent(instance.transform);
            var runtime = instance.EnsureAliceRuntimeHub();
            if (runtime is AliceStrikerHub aliceRuntime) {
                var isTutorial = tutorialSetting.IsTutorialBattleRequested;
                aliceRuntime.InitializeRuntimeDependencies(strikerRegistry, battleOnlineSync, isTutorial && playerId == 0);
                var isTutorialOpponent = isTutorial && playerId == 1;
                aliceRuntime.ConfigureSurvival(
                    isTutorial,
                    isTutorialOpponent ? tutorialSetting.OpponentHitPointRecoveryRatioPerSecond : 0f);
            }
            runtime.SetPlayerId(playerId);
            return runtime;
        }
    }
}
