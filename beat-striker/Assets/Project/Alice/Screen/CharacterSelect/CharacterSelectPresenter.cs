using System;
using System.Collections.Generic;
using R3;
using UnityEngine;
using VContainer;

namespace Alice {
    public class CharacterSelectPresenter : MonoBehaviour {

        ISceneTransitionService sceneTransitionService;

        [Inject]
        public void Construct(ISceneTransitionService sceneTransitionService) {
            this.sceneTransitionService = sceneTransitionService;
        }
    }
}