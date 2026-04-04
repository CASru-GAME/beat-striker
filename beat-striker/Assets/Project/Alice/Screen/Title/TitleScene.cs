using Alice;
using UnityEngine;
using VContainer;

public class TitleScene : MonoBehaviour {
    ISceneTransitionService sceneTransitionService;

    [Inject]
    public void Construct(ISceneTransitionService sceneTransitionService) {
        this.sceneTransitionService = sceneTransitionService;

        _ = sceneTransitionService.RequestEndTransitionAsync(AppScene.Title);
    }

    public void GotoSelectScene() {
        sceneTransitionService.RequestStartTransition(AppScene.StageSelect);
    }
}
