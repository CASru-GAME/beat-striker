using R3;
using UnityEngine;

namespace Alice {
    public class MenuPresenterView : MonoBehaviour {
        readonly Subject<Unit> gotoTitleRequested = new();
        readonly Subject<Unit> gotoTutorialRequested = new();
        readonly Subject<Unit> localBattleRequested = new();
        readonly Subject<Unit> onlineBattleRequested = new();
        readonly Subject<Unit> gotoRankingRequested = new();

        [SerializeField] ActionEmitter gotoTitleEmitter;
        [SerializeField] ActionEmitter gotoTutorialEmitter;
        [SerializeField] ActionEmitter localBattleEmitter;
        [SerializeField] ActionEmitter onlineBattleEmitter;
        [SerializeField] ActionEmitter gotoRankingEmitter;

        public Observable<Unit> GotoTitleRequested => gotoTitleRequested;
        public Observable<Unit> GotoTutorialRequested => gotoTutorialRequested;
        public Observable<Unit> LocalBattleRequested => localBattleRequested;
        public Observable<Unit> OnlineBattleRequested => onlineBattleRequested;
        public Observable<Unit> GotoRankingRequested => gotoRankingRequested;

        void Awake() {
            gotoTitleEmitter.OnClickEvent.Subscribe(_ => gotoTitleRequested.OnNext(Unit.Default)).AddTo(this);
            gotoTutorialEmitter.OnClickEvent.Subscribe(_ => gotoTutorialRequested.OnNext(Unit.Default)).AddTo(this);
            localBattleEmitter.OnClickEvent.Subscribe(_ => localBattleRequested.OnNext(Unit.Default)).AddTo(this);
            onlineBattleEmitter.OnClickEvent.Subscribe(_ => onlineBattleRequested.OnNext(Unit.Default)).AddTo(this);
            gotoRankingEmitter.OnClickEvent.Subscribe(_ => gotoRankingRequested.OnNext(Unit.Default)).AddTo(this);
        }
    }
}
