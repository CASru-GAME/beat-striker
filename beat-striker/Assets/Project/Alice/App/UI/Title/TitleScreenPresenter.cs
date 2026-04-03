using System;
using System.Collections.Generic;
using R3;
using UnityEngine;
using VContainer;

namespace Alice {
    public class TitleScreenPresenter : MonoBehaviour {

        ISceneTransitionService sceneTransitionService;

        [Inject]
        public void Construct(ISceneTransitionService sceneTransitionService) {
            this.sceneTransitionService = sceneTransitionService;
        }
    }
}