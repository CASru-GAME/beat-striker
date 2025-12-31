using System.Collections;
using Core.App.Presenters.Scene.Types;
using Core.App.Types;
using Core.Utils;
using UnityEngine;
using UnityEngine.SceneManagement;
public class StageselectScene : MonoBehaviour
 {
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start() {
        this.GetBus().Subscribe<AppMessages.OnTransitionAnimationStarted>(OnTransitionStartedMessage);
    }



    // Update is called once per frame
    void Update() {

    }

    void OnTransitionStartedMessage(AppMessages.OnTransitionAnimationStarted msg) {
        Debug.Log("StageselectScene: OnTransitionStartedMessage received");
        //遷移アニメーションを記述する
        StartCoroutine(Animation());
    }


    IEnumerator Animation() {
        //ここにアニメーションを記述する
        yield return new WaitForSeconds(1.0f); //仮で1秒待つ

        this.GetBus().Publish(new AppMessages.RequireLoadScene());
    }

    void OnDestroy() {
        this.GetBus().Unsubscribe<AppMessages.OnTransitionAnimationStarted>(OnTransitionStartedMessage);
    }
}
