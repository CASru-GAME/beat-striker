using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Alice;
using R3;
using Core;
using System;
using UnityEngine.UI;
using UnityEngine.Serialization;

[Serializable]
public class StageSeps {
    public Stage stage;
    public Sprite thumbnail;
}

[RequireComponent(typeof(CanvasGroup))]
public class MusicPopup : MonoBehaviour
{
    private CanvasGroup canvasGroup;
    private Vector3 stageSelectPopInitialLocalPosition;
    [SerializeField] MusicCards musicCards;
    public RectTransform musicSelection;
    public float popupDelay = 0.3f;
    [FormerlySerializedAs("fadeSpeed")]
    public float fadeDuration = 0.3f;
    public float musicSlideDistance = 500f;
    [SerializeField] Botan hideButton;
    [SerializeField] StageSeps[] stageInfo;
    [SerializeField] GameObject stageSelectPop;
    [SerializeField] Image thumbnailImage;

    public Observable<MusicInfo> OnMusicSelected => musicCards.OnMusicSelected;
    readonly Subject<Unit> popupHidden = new();
    public Observable<Unit> OnHidden => popupHidden;
    
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

        stageSelectPopInitialLocalPosition = stageSelectPop.transform.localPosition;

        gameObject.SetActive(false);
        
        hideButton.OnClickEvent.Subscribe((data) => {
            Hide();
        });
    }
    
    public void Show()
    {
        if (isShowing) return;
        
        isShowing = true;
        gameObject.SetActive(true);
        StartCoroutine(ShowWithFadeAndMusicSlide());
    }

    public void Initialize(Stage stage, IReadOnlyList<MusicInfo> musics, IReadOnlyDictionary<string, MusicCardAddressableAssets> preloadedAssetsByMusicId) {
        ApplyStageThumbnail(stage);
        musicCards.Initialize(musics, preloadedAssetsByMusicId);
    }

    public void Initialize(IReadOnlyList<MusicInfo> musics, IReadOnlyDictionary<string, MusicCardAddressableAssets> preloadedAssetsByMusicId) {
        musicCards.Initialize(musics, preloadedAssetsByMusicId);
    }
    
    public void Hide()
    {
        if (!isShowing) return;
        
        isShowing = false;
        StartCoroutine(HideWithFadeAndMusicSlide());
    }
    
    IEnumerator ShowWithFadeAndMusicSlide()
    {
        const float musicSlideDuration = 0.4f;
        const float stageSelectSlideDuration = 0.4f;

        canvasGroup.alpha = 0f;
        targetAlpha = 1f;

        Vector3 stageSelectRightOff = stageSelectPopInitialLocalPosition + new Vector3(musicSlideDistance, 0f, 0f);
        stageSelectPop.transform.localPosition = stageSelectRightOff;

        // musicSelectionを右側にオフセット
        if (musicSelection != null)
        {
            Vector3 centerPos = musicSelection.localPosition;
            Vector3 rightOff = centerPos + new Vector3(musicSlideDistance, 0f, 0f);
            musicSelection.localPosition = rightOff;
        }
        
        yield return Ex.Wait(popupDelay);
        
        // フェードイン
        LeanTween.cancel(stageSelectPop);
        LeanTween.moveLocal(stageSelectPop, stageSelectPopInitialLocalPosition, stageSelectSlideDuration)
            .setEase(LeanTweenType.easeOutQuad);

        float fadeInElapsed = 0f;
        while (fadeInElapsed < fadeDuration)
        {
            fadeInElapsed += Time.deltaTime;
            canvasGroup.alpha = Mathf.Lerp(0f, targetAlpha, Mathf.Clamp01(fadeInElapsed / fadeDuration));
            yield return null;
        }
        canvasGroup.alpha = 1f;
        
        // musicSelectionをスライドイン
        if (musicSelection != null)
        {
            Vector3 centerPos = musicSelection.localPosition - new Vector3(musicSlideDistance, 0f, 0f);
            LeanTween.moveLocal(musicSelection.gameObject, centerPos, musicSlideDuration).setEase(LeanTweenType.easeOutQuad);
            yield return Ex.Wait(musicSlideDuration);
        }

        yield return Ex.Wait(stageSelectSlideDuration);
    }

    void ApplyStageThumbnail(Stage stage)
    {
        foreach (var info in stageInfo)
        {
            if (info.stage == stage)
            {
                thumbnailImage.sprite = info.thumbnail;
                break;
            }
        }
    }
    
    IEnumerator HideWithFadeAndMusicSlide()
    {
        targetAlpha = 0f;

        LeanTween.cancel(stageSelectPop);
        Vector3 stageSelectRightOff = stageSelectPop.transform.localPosition + new Vector3(musicSlideDistance, 0f, 0f);
        LeanTween.moveLocal(stageSelectPop, stageSelectRightOff, 0.4f)
            .setEase(LeanTweenType.easeInQuad);
        
        // musicSelectionを右にスライドアウト
        if (musicSelection != null)
        {
            Vector3 currentPos = musicSelection.localPosition;
            Vector3 rightOff = currentPos + new Vector3(musicSlideDistance, 0f, 0f);
            LeanTween.moveLocal(musicSelection.gameObject, rightOff, 0.4f).setEase(LeanTweenType.easeInQuad);
        }
        
        // フェードアウト
        float fadeOutElapsed = 0f;
        float startAlpha = canvasGroup.alpha;
        while (fadeOutElapsed < fadeDuration)
        {
            fadeOutElapsed += Time.deltaTime;
            canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, Mathf.Clamp01(fadeOutElapsed / fadeDuration));
            yield return null;
        }
        
        canvasGroup.alpha = 0f;
        gameObject.SetActive(false);
        popupHidden.OnNext(Unit.Default);
        Destroy(gameObject);
    }
}
