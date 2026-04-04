using System;
using System.Collections.Generic;
using R3;
using UnityEngine;
using VContainer;

namespace Alice {
    public class ResultMenuPresenter : MonoBehaviour {

        ISceneTransitionService sceneTransitionService;
        bool initialized;

        [Inject]
        public void Construct(ISceneTransitionService sceneTransitionService) {
            this.sceneTransitionService = sceneTransitionService;
        }

        void Start() {
            if (initialized) {
                return;
            }

            _ = sceneTransitionService.RequestEndTransitionAsync(AppScene.ResultMenu);
            initialized = true;
        }
    }
}
