
using System;
using System.Collections;
using Core.App.Presenters.Scene.Types;
using Core.Utils;
using UnityEngine;

public class SelectScene : MonoBehaviour {
    void Start() {
        this.GetBus().Subscribe<AppMessages.OnTransitionAnimationStarted>(HandleTrackSelection);
    }

    void Update() {

    }

    void HandleTrackSelection(AppMessages.OnTransitionAnimationStarted msg) {
        //遷移アニメーションを記述する
        StartCoroutine(Animation());
    }

    private IEnumerator Animation() {
        //ここにアニメーションを記述する
        yield return new WaitForSeconds(1.0f); //仮で1秒待つ

        this.GetBus().Publish(new AppMessages.RequireLoadScene());
    }

    void OnDestroy() {
        this.GetBus().Unsubscribe<AppMessages.OnTransitionAnimationStarted>(HandleTrackSelection);
    }
}