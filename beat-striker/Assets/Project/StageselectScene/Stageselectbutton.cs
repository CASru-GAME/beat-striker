using UnityEngine;
using UnityEngine.UI;
using System.Collections;

[RequireComponent(typeof(Botan))]
[RequireComponent(typeof(AudioSource))]
public class Stageselectbutton : MonoBehaviour
{
     Botan botan;
    public RawImage image;
    public AudioClip hoverSound;
    AudioSource audioSource;
    public Panel panel; // Panel参照
    public enum MoveType { None, Right, Left }
    public MoveType moveType = MoveType.None;
    public GameObject popupPanel;
    public CanvasGroup popupCanvasGroup;
    public float popupDelay = 0.3f;
    public float fadeSpeed = 6.0f;
    private static bool isPopupShown = false;
    public float targetAlpha = 0f;
    public RectTransform musicSelection;
    public float musicSlideDistance = 500f;
    private bool isPopupFadeInComplete = false;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start() {
        popupPanel.SetActive(false);
        botan = GetComponent<Botan>();
        audioSource = GetComponent<AudioSource>();

        image.color = Color.gray;
        if (popupCanvasGroup != null) popupCanvasGroup.alpha = 0f;

        botan.onHover += (e) => {
            if (isPopupShown) return;
            image.color = Color.white;
            Debug.Log("hovered");
            if (hoverSound != null && audioSource != null) {
                audioSource.PlayOneShot(hoverSound);
            }
             if(panel != null) {
                if (moveType == MoveType.Right) panel.MoveRight();
                else if (moveType == MoveType.Left) panel.MoveLeft();
            }
        };
        botan.onClick += (e) => {
            Debug.Log("clicked");
            if (popupPanel != null && popupCanvasGroup != null) {
                StartCoroutine(ShowPopupWithFadeAndMusicSlide());
                isPopupShown = true;
            }
        };
        botan.onHoverExit += (e) => {
            if (isPopupShown) return;
            image.color = Color.gray;
            Debug.Log("hover exited");
        };
        
    }
    IEnumerator ShowPopupWithFadeAndMusicSlide()
    {
        popupPanel.SetActive(true);
        popupCanvasGroup.alpha = 0f;
        targetAlpha = 1f;

        if (musicSelection != null)
        {
            Vector3 centerPos = musicSelection.localPosition;
            Vector3 rightOff = centerPos + new Vector3(musicSlideDistance, 0f, 0f);
            musicSelection.localPosition = rightOff;
        }
        yield return new WaitForSeconds(popupDelay);
        while (popupCanvasGroup.alpha < 0.99f) 
        {

            popupCanvasGroup.alpha = Mathf.Lerp(popupCanvasGroup.alpha, targetAlpha, Time.deltaTime * fadeSpeed);
            yield return null;
        }
        popupCanvasGroup.alpha = 1f;
        isPopupFadeInComplete = true;
        if (musicSelection != null)
        {
            Vector3 centerPos = musicSelection.localPosition - new Vector3(musicSlideDistance, 0f, 0f);
            LeanTween.moveLocal(musicSelection.gameObject, centerPos, 0.4f).setEase(LeanTweenType.easeOutQuad);
        }
    }
    // Update is called once per frame
    void Update()
    {
        
    }
}
