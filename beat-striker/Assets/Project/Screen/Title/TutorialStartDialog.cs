using R3;
using UnityEngine;

namespace Alice {
    public class TutorialStartDialog : MonoBehaviour {
        readonly Subject<Unit> yesRequested = new();
        readonly Subject<Unit> noRequested = new();
        readonly Subject<Unit> onlineRequested = new();

        [SerializeField] ActionEmitter yesEmitter;
        [SerializeField] ActionEmitter noEmitter;
        [SerializeField] ActionEmitter onlineEmitter;

        public Observable<Unit> YesRequested => yesRequested;
        public Observable<Unit> NoRequested => noRequested;
        public Observable<Unit> OnlineRequested => onlineRequested;

        void Awake() {
            yesEmitter.OnClickEvent.Subscribe(_ => yesRequested.OnNext(Unit.Default)).AddTo(this);
            noEmitter.OnClickEvent.Subscribe(_ => noRequested.OnNext(Unit.Default)).AddTo(this);
            onlineEmitter.OnClickEvent.Subscribe(_ => onlineRequested.OnNext(Unit.Default)).AddTo(this);
        }

        public void SetVisible(bool visible) {
            gameObject.SetActive(visible);
        }
    }
}
