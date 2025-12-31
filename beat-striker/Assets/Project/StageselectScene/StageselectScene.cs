using System;
using System.Collections;
using Core.App;
using Core.App.Installers;
using Core.App.Interfaces;
using Core.App.Presenters.Scene.Types;
using Core.App.Types;
using UnityEngine;
using UnityEngine.SceneManagement;

public class StageselectScene : MonoBehaviour {
    private IAppModel appModel;
    private IDisposable transitionSubscription;

    void Start() {
        appModel = AppFlowScope.GetInstance().GetAppModel();
        transitionSubscription = appModel.SubscribeTransitionAnimationStarted(OnTransitionStarted);
    }

    void Update() {
    }

    void OnTransitionStarted(AppScene scene) {
        Debug.Log("StageselectScene: OnTransitionStarted received");
        //遷移アニメーションを記述する
        StartCoroutine(Animation());
    }

    IEnumerator Animation() {
        //ここにアニメーションを記述する
        yield return new WaitForSeconds(1.0f); //仮で1秒待つ

        appModel.FireRequireLoadScene();
    }

    void OnDestroy() {
        transitionSubscription?.Dispose();
    }
}
