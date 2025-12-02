using UnityEngine;
using System.Collections;
using Core;

[RequireComponent(typeof(CanvasGroup))]
public class MusicPopup : MonoBehaviour
{
    private CanvasGroup canvasGroup;
    public RectTransform musicSelection;
    public float popupDelay = 0.3f;
    public float fadeSpeed = 6.0f;
    public float musicSlideDistance = 500f;
    [SerializeField] Botan hideButton;
    
    private float targetAlpha = 0f;
    private bool isShowing = false;
    
    void Awake()
    {
        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
        }
        
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
        }

        gameObject.SetActive(false);
        
        hideButton.onClick += (data) => {
            Hide();
        };
    }
    
    public void Show()
    {
        if (isShowing) return;
        
        isShowing = true;
        gameObject.SetActive(true);
        StartCoroutine(ShowWithFadeAndMusicSlide());
    }
    
    public void Hide()
    {
        if (!isShowing) return;
        
        isShowing = false;
        StartCoroutine(HideWithFadeAndMusicSlide());
    }
    
    IEnumerator ShowWithFadeAndMusicSlide()
    {
        canvasGroup.alpha = 0f;
        targetAlpha = 1f;

        // musicSelectionを右側にオフセット
        if (musicSelection != null)
        {
            Vector3 centerPos = musicSelection.localPosition;
            Vector3 rightOff = centerPos + new Vector3(musicSlideDistance, 0f, 0f);
            musicSelection.localPosition = rightOff;
        }
        
        yield return new WaitForSeconds(popupDelay);
        
        // フェードイン
        while (canvasGroup.alpha < 0.99f) 
        {
            canvasGroup.alpha = Mathf.Lerp(canvasGroup.alpha, targetAlpha, Time.deltaTime * fadeSpeed);
            yield return null;
        }
        canvasGroup.alpha = 1f;
        
        // musicSelectionをスライドイン
        if (musicSelection != null)
        {
            Vector3 centerPos = musicSelection.localPosition - new Vector3(musicSlideDistance, 0f, 0f);
            LeanTween.moveLocal(musicSelection.gameObject, centerPos, 0.4f).setEase(LeanTweenType.easeOutQuad);
        }
    }
    
    IEnumerator HideWithFadeAndMusicSlide()
    {
        targetAlpha = 0f;
        
        // musicSelectionを右にスライドアウト
        if (musicSelection != null)
        {
            Vector3 currentPos = musicSelection.localPosition;
            Vector3 rightOff = currentPos + new Vector3(musicSlideDistance, 0f, 0f);
            LeanTween.moveLocal(musicSelection.gameObject, rightOff, 0.4f).setEase(LeanTweenType.easeInQuad);
        }
        
        // フェードアウト
        while (canvasGroup.alpha > 0.01f)
        {
            canvasGroup.alpha = Mathf.Lerp(canvasGroup.alpha, targetAlpha, Time.deltaTime * fadeSpeed);
            yield return null;
        }
        
        canvasGroup.alpha = 0f;
        gameObject.SetActive(false);
        Destroy(gameObject);
    }
}
