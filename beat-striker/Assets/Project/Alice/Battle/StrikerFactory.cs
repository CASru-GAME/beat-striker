using Core.Striker;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Alice {
    public interface IStrikerFactory {
        StrikerHub Create(StrikerHub prefab, Transform playerTransform);
    }

    public class StrikerHubFactory : IStrikerFactory {
        readonly IObjectResolver container;

        public StrikerHubFactory(IObjectResolver container) {
            this.container = container;
        }

        public StrikerHub Create(StrikerHub prefab, Transform playerTransform) {
            var instance = Object.Instantiate(prefab);
            instance.transform.SetPositionAndRotation(playerTransform.position, playerTransform.rotation);
            playerTransform.SetParent(instance.transform);
            container.InjectGameObject(instance.gameObject);
            return instance;
        }
    }
}
