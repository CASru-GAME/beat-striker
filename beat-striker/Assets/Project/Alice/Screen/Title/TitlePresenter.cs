using System.Threading.Tasks;
using R3;

namespace Alice {
    public class TitlePresenter : System.IDisposable {
        readonly TitleScene view;
        readonly ISceneTransitionService sceneTransitionService;
        readonly CompositeDisposable subscriptions = new();

        public TitlePresenter(TitleScene view, ISceneTransitionService sceneTransitionService) {
            this.view = view;
            this.sceneTransitionService = sceneTransitionService;

            this.view.GotoSelectRequested
                .Subscribe(_ => GoToSelectScene())
                .AddTo(subscriptions);
            _ = EnterTitleAsync();
        }

        public Task EnterTitleAsync() {
            return sceneTransitionService.RequestEndTransitionAsync(AppScene.Title);
        }

        public void GoToSelectScene() {
            _ = sceneTransitionService.RequestStartTransition(AppScene.StageSelect);
        }

        public void Dispose() {
            subscriptions.Dispose();
        }
    }
}
