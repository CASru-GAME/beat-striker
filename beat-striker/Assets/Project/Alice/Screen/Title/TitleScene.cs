using R3;
using UnityEngine;

namespace Alice {
    public class TitleScene : MonoBehaviour {
        readonly Subject<Unit> gotoSelectRequested = new();

        public Observable<Unit> GotoSelectRequested => gotoSelectRequested;

        public void RequestGotoSelectScene() {
            gotoSelectRequested.OnNext(Unit.Default);
        }
    }
}
