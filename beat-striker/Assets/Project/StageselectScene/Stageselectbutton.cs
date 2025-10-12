using UnityEngine;
using UnityEngine.UI;
using System.Collections;
public class Stageselectbutton : MonoBehaviour
{
     Botan botan;
    public RawImage image;
    public AudioClip hoverSound;
    AudioSource audioSource;
    public Panel panel; // Panel参照
    public enum MoveType { None, Right, Left }
    public MoveType moveType = MoveType.None;
    public GameObject PopupPanel;
    public CanvasGroup popupCanvasGroup;
    public float popupDelay = 0.3f;
    public float fadeSpeed = 6.0f;
    private static bool isPopupShown = false;
    public float targetAlpha = 0f;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start() {
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
            if (PopupPanel != null && popupCanvasGroup != null) {
                StartCoroutine(ShowPopupWithFade());
                isPopupShown = true;
            }
        };
        botan.onHoverExit += (e) => {
            if (isPopupShown) return;
            image.color = Color.gray;
            Debug.Log("hover exited");
        };
        
    }
    IEnumerator ShowPopupWithFade()
    {
        PopupPanel.SetActive(true);
        popupCanvasGroup.alpha = 0f;
        targetAlpha = 1f;
        yield return new WaitForSeconds(popupDelay);
        while (popupCanvasGroup.alpha < 0.99f) 
        {

            popupCanvasGroup.alpha = Mathf.Lerp(popupCanvasGroup.alpha, targetAlpha, Time.deltaTime * fadeSpeed);
            yield return null;
        }
        popupCanvasGroup.alpha = 1f;
    }
    // Update is called once per frame
    void Update()
    {
        
    }
}
