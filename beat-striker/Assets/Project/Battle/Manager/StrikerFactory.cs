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
        readonly IMusicPlayer musicPlayer;

        public StrikerHubFactory(IStrikerRegistry strikerRegistry, IMusicPlayer musicPlayer) {
            this.strikerRegistry = strikerRegistry;
            this.musicPlayer = musicPlayer;
        }

        public IStrikerHub Create(StrikerHub prefab, Transform playerTransform, int playerId) {
            var instance = Object.Instantiate(prefab);
            instance.transform.SetPositionAndRotation(playerTransform.position, playerTransform.rotation);
            playerTransform.SetParent(instance.transform);
            var runtime = instance.EnsureAliceRuntimeHub();
            if (runtime is AliceStrikerHub aliceRuntime) {
                aliceRuntime.InitializeRuntimeDependencies(strikerRegistry, musicPlayer);
            }
            runtime.SetPlayerId(playerId);
            return runtime;
        }
    }
}
