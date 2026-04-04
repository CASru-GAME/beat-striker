using Alice;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Alice {
    public interface IStrikerFactory {
        IStrikerHub Create(StrikerHub prefab, Transform playerTransform, int playerId);
    }

    public class StrikerHubFactory : IStrikerFactory {
        readonly IObjectResolver container;

        public StrikerHubFactory(IObjectResolver container) {
            this.container = container;
        }

        public IStrikerHub Create(StrikerHub prefab, Transform playerTransform, int playerId) {
            var instance = Object.Instantiate(prefab);
            instance.transform.SetPositionAndRotation(playerTransform.position, playerTransform.rotation);
            playerTransform.SetParent(instance.transform);
            container.InjectGameObject(instance.gameObject);
            var runtime = instance.EnsureAliceRuntimeHub();
            container.Inject(runtime);
            runtime.SetPlayerId(playerId);
            return runtime;
        }
    }
}
