using R3;
using UnityEngine;
using CorePlayerId = Core.App.Types.PlayerId;

namespace Alice {
    public interface IBattlePresenter {
        Observable<Unit> IntroFinished { get; }
        Observable<Unit> RoundStartAnimationFinished { get; }
        Observable<Unit> RoundFinishAnimationFinished { get; }
        Observable<Unit> OutroFinished { get; }

        void PresentIntro();
        void PresentRoundStart(int roundNumber);
        void PresentRoundPlayableStart();
        void PresentRoundFinish();
        void PresentBattleFinish();
        void PresentOutro(CorePlayerId winner);
    }

    public class BattlePresenter : MonoBehaviour, IBattlePresenter {
        [SerializeField] StageCamera stageCamera;
        [SerializeField] BattleRoundStartPresenter roundStartPresenter;
        [SerializeField] BattleResultTextPresenter resultTextPresenter;
        [SerializeField] BattleFadePresenter fadePresenter;

        CompositeDisposable disposables = new();

        readonly Subject<Unit> introFinishedSubject = new();
        readonly Subject<Unit> roundStartAnimationFinishedSubject = new();
        readonly Subject<Unit> roundFinishAnimationFinishedSubject = new();
        readonly Subject<Unit> outroFinishedSubject = new();

        public Observable<Unit> IntroFinished => introFinishedSubject;
        public Observable<Unit> RoundStartAnimationFinished => roundStartAnimationFinishedSubject;
        public Observable<Unit> RoundFinishAnimationFinished => roundFinishAnimationFinishedSubject;
        public Observable<Unit> OutroFinished => outroFinishedSubject;

        void Start() {
            stageCamera.IntroFinished
                .Subscribe(_ => introFinishedSubject.OnNext(Unit.Default))
                .AddTo(disposables);

            stageCamera.OutroFinished
                .Subscribe(_ => outroFinishedSubject.OnNext(Unit.Default))
                .AddTo(disposables);

            roundStartPresenter.AnimationFinished
                .Subscribe(_ => roundStartAnimationFinishedSubject.OnNext(Unit.Default))
                .AddTo(disposables);

            fadePresenter.FadeInCompleted
                .Subscribe(_ => roundFinishAnimationFinishedSubject.OnNext(Unit.Default))
                .AddTo(disposables);

            resultTextPresenter.FinishHidden
                .Subscribe(_ => fadePresenter.PresentFadeTransition())
                .AddTo(disposables);

            resultTextPresenter.OutroFinished
                .Subscribe(_ => outroFinishedSubject.OnNext(Unit.Default))
                .AddTo(disposables);
        }

        void OnDestroy() {
            disposables.Dispose();
        }

        public void PresentIntro() {
            stageCamera.PresentIntro();
        }

        public void PresentRoundStart(int roundNumber) {
            roundStartPresenter.PresentRoundStart(roundNumber);
        }

        public void PresentRoundPlayableStart() {
            stageCamera.PresentRoundPlayableStart();
        }

        public void PresentRoundFinish() {
            stageCamera.PresentRoundFinish();
            fadePresenter.PresentFadeTransition();
        }

        public void PresentBattleFinish() {
            stageCamera.PresentBattleFinish();
            resultTextPresenter.PresentBattleFinish();
        }

        public void PresentOutro(CorePlayerId winner) {
            stageCamera.PresentOutro(winner);
            resultTextPresenter.PresentOutro();
        }
    }
}