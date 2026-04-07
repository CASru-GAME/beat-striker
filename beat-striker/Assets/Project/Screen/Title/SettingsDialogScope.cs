using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Alice {
    public class SettingsDialogScope : LifetimeScope {
        [SerializeField] SettingsDialog settingsDialog;

        protected override void Configure(IContainerBuilder builder) {
            builder.RegisterInstance(settingsDialog);
            builder.Register<SettingsDialogPresenter>(Lifetime.Singleton);
            builder.Register<TimeingAdjustPresenter>(Lifetime.Singleton);
            builder.RegisterBuildCallback(container => {
                _ = container.Resolve<SettingsDialogPresenter>();
                _ = container.Resolve<TimeingAdjustPresenter>();
            });
        }

        protected override LifetimeScope FindParent() {
            return AppScope.Instance;
        }
    }
}
