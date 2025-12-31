using System.Collections;
using Core.App;
using Core.App.Installers;
using Core.App.Interfaces;
using Core.App.Presenters.Scene.Types;
using Core.App.Types;
using Core.Utils;
using UnityEngine;
using UnityEngine.SceneManagement;

public class TitleScene : MonoBehaviour {
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start() {
    }


    // Update is called once per frame
    void Update() {

    }


    void OnDestroy() {
    }

    public void GotoSelectScene() {
        AppFlowScope.GetInstance().GetAppModel().FireRequireTransition(AppScene.StageSelect);
    }
}
