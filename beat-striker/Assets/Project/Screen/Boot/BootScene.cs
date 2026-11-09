using R3;
using UnityEngine;

namespace Alice {
    public class BootScene : MonoBehaviour {
        readonly Subject<bool> touchControllerSelectionRequested = new();

        [SerializeField] ActionEmitter gamePadEmitter;
        [SerializeField] ActionEmitter touchPanelEmitter;
        [SerializeField] ActionEmitter keyboardEmitter;
        [SerializeField] BootCreditsView credits;

        public Observable<bool> TouchControllerSelectionRequested => touchControllerSelectionRequested;
        public Awaitable PlayCreditsAsync() => credits.PlayAsync(destroyCancellationToken);

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
