using UnityEngine;
using UnityEngine.UI;
using Core;

public class StartButtonAnimation : MonoBehaviour
{
    [Header("Line References")]
    public RectTransform whiteLineAbove; // 赤いImage（上）
    public RectTransform whiteLineUnder; // 青いImage（下）
    
    [Header("Text References")]
    public CanvasGroup textCanvasGroup; // STARTテキストのCanvasGroup
    
    [Header("Animation Settings")]
    public float lineDuration = 0.5f; // ラインの移動時間
    public float lineDistance = 500f; // ラインの移動距離
    public float textFadeDuration = 0.5f; // テキストのフェード時間
    public bool loopTextFade = true; // テキストのフェードをループするか
    
    [Header("Click Feedback")]
    public AudioClip clickSound; // クリック時の効果音
    public RectTransform clickTarget; // へこませる対象（黒い画像のRectTransform）
    public float scaleDownAmount = 0.95f; // へこむサイズ（1.0が元のサイズ）
    public float scaleDuration = 0.1f; // へこむアニメーションの時間
    
    [Header("Button Control")]
    public Botan[] blackImageButtons; // 黒い画像のボタン（Botanコンポーネント）2つ
    
    private Vector2 aboveStartPos;
    private Vector2 aboveEndPos;
    private Vector2 underStartPos;
    private Vector2 underEndPos;
    
    private bool animationPlayed = false;
    
    void Start()
    {
        // 初期位置を保存
        if (whiteLineAbove != null)
        {
            aboveEndPos = whiteLineAbove.anchoredPosition;
            aboveStartPos = aboveEndPos + new Vector2(-lineDistance, 0); // 左側
            whiteLineAbove.anchoredPosition = aboveStartPos;
        }
        
        if (whiteLineUnder != null)
        {
            underEndPos = whiteLineUnder.anchoredPosition;
            underStartPos = underEndPos + new Vector2(lineDistance, 0); // 右側
            whiteLineUnder.anchoredPosition = underStartPos;
        }
        
        // Textを透明に
        if (textCanvasGroup != null)
        {
            textCanvasGroup.alpha = 0f;
        }
        
        // 黒い画像のボタンを無効化（Botanコンポーネントのみ）
        foreach (var button in blackImageButtons)
        {
            if (button != null)
            {
                button.enabled = false;
            }
        }
    }
    
    public void OnClick()
    {
        // クリックフィードバック（効果音とへこみ）
        PlayClickFeedback();
        
        // アニメーションがまだ再生されていない場合はアニメーション実行
        if (!animationPlayed)
        {
            animationPlayed = true;
            AnimateLines();
        }
    }
    
    void PlayClickFeedback()
    {
        // 効果音を再生
        if (clickSound != null)
        {
            AudioSource.PlayClipAtPoint(clickSound, Camera.main.transform.position);
        }
        
        // へこむアニメーション
        if (clickTarget != null)
        {
            // 元のスケールをキャンセル
            LeanTween.cancel(clickTarget.gameObject);
            
            // へこんで戻る
            LeanTween.scale(clickTarget, Vector3.one * scaleDownAmount, scaleDuration)
                .setEase(LeanTweenType.easeOutQuad)
                .setOnComplete(() => {
                    LeanTween.scale(clickTarget, Vector3.one, scaleDuration)
                        .setEase(LeanTweenType.easeOutQuad);
                });
        }
    }
    
    void AnimateLines()
    {
        Debug.Log("StartButtonAnimation started");
        
        // 赤いLine（左から右へ）
        if (whiteLineAbove != null)
        {
            LeanTween.move(whiteLineAbove, aboveEndPos, lineDuration)
                .setEase(LeanTweenType.easeOutQuad);
        }
        
        // 青いLine（右から左へ）
        if (whiteLineUnder != null)
        {
            LeanTween.move(whiteLineUnder, underEndPos, lineDuration)
                .setEase(LeanTweenType.easeOutQuad)
                .setOnComplete(ShowText);
        }
    }
    
    void ShowText()
    {
        Debug.Log("Lines animation complete, showing text");
        
        if (textCanvasGroup != null)
        {
            if (loopTextFade)
            {
                // フェードイン・アウトをループ
                LeanTween.alphaCanvas(textCanvasGroup, 1f, textFadeDuration)
                    .setEase(LeanTweenType.easeInOutQuad)
                    .setLoopPingPong();
            }
            else
            {
                // 一度だけフェードイン
                LeanTween.alphaCanvas(textCanvasGroup, 1f, textFadeDuration)
                    .setEase(LeanTweenType.easeInOutQuad);
            }
        }
        
        // アニメーション完了後、黒い画像のボタンを有効化
        foreach (var button in blackImageButtons)
        {
            if (button != null)
            {
                button.enabled = true;
            }
        }
        
        Debug.Log("Animation complete. Black image buttons are now active.");
    }
}
