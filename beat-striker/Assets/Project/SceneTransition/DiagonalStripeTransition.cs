using UnityEngine;
using System.Collections;
using Core.App.Presenters.Scene.Types;
using Core.App.Types;
using Core.Utils;

/// <summary>
/// シーントランジションの制御を行うクラス
/// メッセージバスの購読とアニメーション開始・完了通知を担当
/// </summary>
public class DiagonalStripeTransition : MonoBehaviour {
    [Header("References")]
    public DiagonalStripeVisual visual; // 見た目の制御を行うコンポーネント

    private bool isTransitioning = false;

    void Awake() {
        Debug.Log("DiagonalStripeTransition Awake called");

        // シーン遷移時に破棄されないようにする
        DontDestroyOnLoad(gameObject);

        // Visualコンポーネントの取得
        if (visual == null) {
            visual = GetComponent<DiagonalStripeVisual>();
            if (visual == null) {
                Debug.LogError("DiagonalStripeVisual component not found!");
            }
        }
    }

    void Start() {
        // 遷移開始通知を購読
        this.GetBus().Subscribe<AppMessages.OnTransitionAnimationStarted>(OnTransitionStartedMessage);
    }

    void OnDestroy() {
        // 購読解除
        this.GetBus().Unsubscribe<AppMessages.OnTransitionAnimationStarted>(OnTransitionStartedMessage);
    }

    /// <summary>
    /// App側から遷移開始通知を受け取る
    /// </summary>
    void OnTransitionStartedMessage(AppMessages.OnTransitionAnimationStarted msg) {
        Debug.Log($"OnTransitionAnimationStarted received: {msg.scene}");
        if (!isTransitioning) {
            StartCoroutine(PlayTransitionAnimation());
        }
    }

    /// <summary>
    /// トランジションアニメーション全体を制御
    /// </summary>
    IEnumerator PlayTransitionAnimation() {
        Debug.Log("PlayTransitionAnimation started");
        isTransitioning = true;

        if (visual == null) {
            Debug.LogError("Visual component is null!");
            isTransitioning = false;
            yield break;
        }

        // フェードイン(ストライプを順次表示)
        yield return StartCoroutine(visual.PlayFadeIn());

        Debug.Log("Publishing RequireLoadScene");
        // アニメーション完了後にロード許可を送る
        this.GetBus().Publish(new AppMessages.RequireLoadScene());

        // 少し待機
        yield return new WaitForSeconds(0.1f);

        // フェードアウト(ストライプを順次非表示)
        yield return StartCoroutine(visual.PlayFadeOut());

        isTransitioning = false;
        Debug.Log("PlayTransitionAnimation complete");

        // アニメーション終了後、自身を破棄
        Debug.Log("Destroying DiagonalStripeTransition instance");
        Destroy(gameObject);
    }
}
