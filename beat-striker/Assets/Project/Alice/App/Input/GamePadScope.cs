using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Alice {
    public class GamePadScope : LifetimeScope {
        const string LOG_PREFIX = "[GamePadScope]";

        [SerializeField] GamePad gamePad;

        protected override void Configure(IContainerBuilder builder) {
            Debug.Log($"{LOG_PREFIX} Configure begin. scene={gameObject.scene.name}, gamePad={gamePad.name}");
            builder.RegisterBuildCallback(container => {
                Debug.Log($"{LOG_PREFIX} BuildCallback begin. scene={gameObject.scene.name}, gamePad={gamePad.name}");
                gamePad.Initialize(container.Resolve<IGamePadRegistry>());
                Debug.Log($"{LOG_PREFIX} BuildCallback initialize completed. gamePad={gamePad.name}");
            });
            Debug.Log($"{LOG_PREFIX} Configure completed. scene={gameObject.scene.name}, gamePad={gamePad.name}");
        }

        protected override LifetimeScope FindParent() {
            var parent = AppScope.Instance;
            Debug.Log($"{LOG_PREFIX} FindParent called. resolvedParent={parent != null}, gamePad={gamePad.name}");
            return parent;
        }
    }
}
