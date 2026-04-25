using R3;
using UnityEngine;

namespace Alice {
    public class BootScene : MonoBehaviour {
        readonly Subject<bool> touchControllerSelectionRequested = new();

        [SerializeField] ActionEmitter gamePadEmitter;
        [SerializeField] ActionEmitter touchPanelEmitter;
        [SerializeField] ActionEmitter keyboardEmitter;

        public Observable<bool> TouchControllerSelectionRequested => touchControllerSelectionRequested;

        void Awake() {
            gamePadEmitter.OnClickEvent
                .Subscribe(_ => touchControllerSelectionRequested.OnNext(false))
                .AddTo(this);
            touchPanelEmitter.OnClickEvent
                .Subscribe(_ => touchControllerSelectionRequested.OnNext(true))
                .AddTo(this);
            keyboardEmitter.OnClickEvent
                .Subscribe(_ => touchControllerSelectionRequested.OnNext(false))
                .AddTo(this);
        }
    }
}
