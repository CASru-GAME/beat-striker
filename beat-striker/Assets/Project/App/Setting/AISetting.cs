using R3;
using UnityEngine;

namespace Alice {
    public interface IAISetting {
        ReadOnlyReactiveProperty<bool> IsLearning { get; }
        void SetLearning(bool isLearning);
    }

    public class AISetting : MonoBehaviour, IAISetting {
        [SerializeField] bool isLearning;

        readonly ReactiveProperty<bool> isLearningProperty = new(false);

        public ReadOnlyReactiveProperty<bool> IsLearning => isLearningProperty;

        void Awake() {
            isLearningProperty.OnNext(isLearning);
        }

        public void SetLearning(bool isLearning) {
            this.isLearning = isLearning;
            isLearningProperty.OnNext(isLearning);
        }

        void OnDestroy() {
            isLearningProperty.Dispose();
        }
    }
}
