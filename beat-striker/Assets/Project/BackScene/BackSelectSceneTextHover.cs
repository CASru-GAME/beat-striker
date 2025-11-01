using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class BackSelectSceneTextHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [Header("Image References")]
    public Image[] gradientImages; // グラデーションの四角いImageの配列（左から右の順）
    
    [Header("Animation Settings")]
    public float fadeInDuration = 0.3f; // 各Imageのフェードイン時間
    public float delayBetweenImages = 0.05f; // 各Image間の遅延（オーディオスペクトラム風）
    public float maxAlpha = 1f; // 最大透明度
    
    [Header("Sound Effect")]
    public AudioClip hoverSound; // ホバー時の効果音
    [Range(0f, 1f)]
    public float hoverSoundVolume = 1f; // ホバー音の音量
    public AudioClip clickSound; // クリック時の効果音
    [Range(0f, 1f)]
    public float clickSoundVolume = 1f; // クリック音の音量
    
    private CanvasGroup[] imageCanvasGroups;
    private AudioSource audioSource;
    private bool isHovering = false;
    
    void Start()
    {
        // AudioSourceを取得または追加
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
        
        // 各ImageにCanvasGroupを追加
        if (gradientImages != null && gradientImages.Length > 0)
        {
            imageCanvasGroups = new CanvasGroup[gradientImages.Length];
            
            for (int i = 0; i < gradientImages.Length; i++)
            {
                if (gradientImages[i] != null)
                {
                    imageCanvasGroups[i] = gradientImages[i].GetComponent<CanvasGroup>();
                    if (imageCanvasGroups[i] == null)
                    {
                        imageCanvasGroups[i] = gradientImages[i].gameObject.AddComponent<CanvasGroup>();
                    }
                    
                    // 初期状態：透明
                    imageCanvasGroups[i].alpha = 0f;
                }
            }
        }
    }
    
    // Unity Event System用のホバー検知
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (isHovering) return;
        
        isHovering = true;
        Debug.Log("BackSelectSceneText hovered");
        
        // 効果音を再生
        if (audioSource != null && hoverSound != null)
        {
            audioSource.PlayOneShot(hoverSound, hoverSoundVolume);
        }
        
        // 左から順番にフェードイン
        for (int i = 0; i < imageCanvasGroups.Length; i++)
        {
            if (imageCanvasGroups[i] != null)
            {
                int index = i; // ローカルコピー
                float delay = i * delayBetweenImages;
                
                LeanTween.delayedCall(delay, () =>
                {
                    LeanTween.alphaCanvas(imageCanvasGroups[index], maxAlpha, fadeInDuration)
                        .setEase(LeanTweenType.easeOutQuad);
                });
            }
        }
    }
    
    public void OnPointerExit(PointerEventData eventData)
    {
        isHovering = false;
        Debug.Log("BackSelectSceneText hover exit");
        
        // すべてのImageをフェードアウト
        for (int i = 0; i < imageCanvasGroups.Length; i++)
        {
            if (imageCanvasGroups[i] != null)
            {
                LeanTween.cancel(imageCanvasGroups[i].gameObject);
                LeanTween.alphaCanvas(imageCanvasGroups[i], 0f, fadeInDuration * 0.5f)
                    .setEase(LeanTweenType.easeInQuad);
            }
        }
    }
    
    public void OnPointerClick(PointerEventData eventData)
    {
        Debug.Log("BackSelectSceneText clicked");
        
        // クリック時の効果音を再生
        if (audioSource != null && clickSound != null)
        {
            audioSource.PlayOneShot(clickSound, clickSoundVolume);
        }
    }
}
