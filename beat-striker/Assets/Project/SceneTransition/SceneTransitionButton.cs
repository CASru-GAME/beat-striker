using UnityEngine;
using Core.App.Types;
using Core.App.Presenters.Scene.Types;
using Core.Utils;

public class SceneTransitionButton : MonoBehaviour
{
    [Header("Scene Settings")]
    public AppScene targetScene; // 遷移先のシーン
    
    // Buttonボタンから呼び出す
    public void OnClick()
    {
        Debug.Log($"Button clicked - Publishing RequireTransition to: {targetScene}");
        // 次シーンへの遷移要求
        this.GetBus().Publish(new AppMessages.RequireTransition(targetScene));
    }
}
