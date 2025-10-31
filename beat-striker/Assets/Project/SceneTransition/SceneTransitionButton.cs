using UnityEngine;

public class SceneTransitionButton : MonoBehaviour
{
    [Header("Scene Settings")]
    public string targetSceneName; // 遷移先のシーン名
    
    // Botanボタンから呼び出す
    public void OnClick()
    {
        if (DiagonalStripeTransition.Instance != null && !string.IsNullOrEmpty(targetSceneName))
        {
            Debug.Log($"Scene transition to: {targetSceneName}");
            DiagonalStripeTransition.Instance.TransitionTo(targetSceneName);
        }
        else
        {
            Debug.LogWarning("DiagonalStripeTransition.Instance is null or targetSceneName is empty!");
        }
    }
}
