namespace Alice {
    public class ResultMenuPresenter {

        readonly ISceneTransitionService sceneTransitionService;
        bool initialized;

        public ResultMenuPresenter(ISceneTransitionService sceneTransitionService) {
            this.sceneTransitionService = sceneTransitionService;
            Initialize();
        }

        void Initialize() {
            if (initialized) {
                return;
            }

            _ = sceneTransitionService.RequestEndTransitionAsync(AppScene.ResultMenu);
            initialized = true;
        }
    }
}
