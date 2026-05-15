using R3;
using UnityEngine;
using VContainer;

namespace Alice {
    public interface ICursorMoveSetting {
        ReadOnlyReactiveProperty<float> CursorSpeed { get; }
        void SetCursorSpeed(float speed);
    }

    public class CursorMoveSetting : ICursorMoveSetting {
        readonly ReactiveProperty<float> cursorSpeed = new();

        public ReadOnlyReactiveProperty<float> CursorSpeed => cursorSpeed;

        [Inject]
        public CursorMoveSetting() {
            cursorSpeed.OnNext(1f);
        }

        public void SetCursorSpeed(float speed) {
            cursorSpeed.OnNext(Mathf.Max(0.01f, speed));
        }
    }
}
