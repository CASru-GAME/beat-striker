using R3;
using UnityEngine;

namespace Alice {
    public class TutorialStartDialog : MonoBehaviour {
        readonly Subject<Unit> yesRequested = new();
        readonly Subject<Unit> noRequested = new();

        [SerializeField] ActionEmitter yesEmitter;
        [SerializeField] ActionEmitter noEmitter;

        public Observable<Unit> YesRequested => yesRequested;
        public Observable<Unit> NoRequested => noRequested;

        void Awake() {
            yesEmitter.OnClickEvent.Subscribe(_ => yesRequested.OnNext(Unit.Default)).AddTo(this);
            noEmitter.OnClickEvent.Subscribe(_ => noRequested.OnNext(Unit.Default)).AddTo(this);
        }

        public void SetVisible(bool visible) {
            gameObject.SetActive(visible);
        }
    }
}
