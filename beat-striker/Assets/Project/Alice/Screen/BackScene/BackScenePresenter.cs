using R3;

namespace Alice {
    public class BackScenePresenter : System.IDisposable {
        readonly BackSelectSceneTextHover[] views;
        readonly ISceneTransitionService transitionService;
        readonly CompositeDisposable subscriptions = new();
        bool initialized;

        public BackScenePresenter(
            BackSelectSceneTextHover[] views,
            ISceneTransitionService transitionService) {
            this.views = views;
            this.transitionService = transitionService;

            Initialize();
        }

        void Initialize() {
            if (initialized) {
                return;
            }

            for (var i = 0; i < views.Length; i++) {
                views[i].OnClicked
                    .Subscribe(scene => transitionService.RequestStartTransition(scene))
                    .AddTo(subscriptions);
            }

            _ = transitionService.RequestEndTransitionAsync(AppScene.ResultMenu);
            initialized = true;
        }

        public void Dispose() {
            subscriptions.Dispose();
        }
    }
}
