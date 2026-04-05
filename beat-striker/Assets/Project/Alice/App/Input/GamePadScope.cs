using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Alice {
    public class GamePadScope : LifetimeScope {
        [SerializeField] GamePad gamePad;

        protected override void Configure(IContainerBuilder builder) {
            builder.RegisterBuildCallback(container => {
                gamePad.Initialize(container.Resolve<IGamePadRegistry>());
            });
        }
    }
}
