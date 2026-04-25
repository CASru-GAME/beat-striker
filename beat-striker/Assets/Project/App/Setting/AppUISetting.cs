using R3;
using UnityEngine;

namespace Alice {
    public interface IAppUISetting {
        ReadOnlyReactiveProperty<bool> UsesTouchController { get; }
        void SetUsesTouchController(bool enabled);
    }

    public class AppUISetting : MonoBehaviour, IAppUISetting {
        [SerializeField] bool usesTouchController;

        readonly ReactiveProperty<bool> usesTouchControllerProperty = new(false);
        bool initialized;

        public ReadOnlyReactiveProperty<bool> UsesTouchController => usesTouchControllerProperty;

        void Awake() {
            InitializeDefaults();
        }

        public void InitializeDefaults() {
            if (initialized) {
                return;
            }

            usesTouchControllerProperty.OnNext(usesTouchController);
            initialized = true;
        }

        public void SetUsesTouchController(bool enabled) {
            usesTouchController = enabled;
            usesTouchControllerProperty.OnNext(enabled);
        }
    }
}
