using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using Core;
using Core.Utils;
using Core.App.Presenters.Scene.Types;
using UnityEditor.SceneManagement;
using Core.App.Types;

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
    
    // black表示用
    public GameObject blackObject; // blackのImageオブジェクト
    private CanvasGroup blackCanvasGroup;
    public float blackFadeDuration = 0.5f;
    private bool isHovering = false;
    private bool hasCompletedMove = false;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start() {
        popupPanel.SetActive(false);
        botan = GetComponent<Botan>();
        audioSource = GetComponent<AudioSource>();

        image.color = Color.gray;
        if (popupCanvasGroup != null) popupCanvasGroup.alpha = 0f;
        
        // blackオブジェクトにCanvasGroupを追加/取得して初期状態で非表示に
        if (blackObject != null)
        {
            blackCanvasGroup = blackObject.GetComponent<CanvasGroup>();
            if (blackCanvasGroup == null)
            {
                blackCanvasGroup = blackObject.AddComponent<CanvasGroup>();
            }
            blackCanvasGroup.alpha = 0f;
        }
        
        // Panelの移動完了イベントを購読
        if (panel != null)
        {
            panel.OnRightMoveComplete += OnPanelMoveComplete;
        }

        botan.onHover += (e) => {
            if (isPopupShown) return;
            image.color = Color.white;
            Debug.Log($"{gameObject.name} hovered - moveType: {moveType}");
            if (hoverSound != null && audioSource != null) {
                audioSource.PlayOneShot(hoverSound);
            }
             if(panel != null) {
                if (moveType == MoveType.Right) {
                    Debug.Log($"{gameObject.name} moving right");
                    panel.MoveRight();
                }
                else if (moveType == MoveType.Left) {
                    Debug.Log($"{gameObject.name} moving left");
                    panel.MoveLeft();
                }
                
                isHovering = true;
                hasCompletedMove = false;
            }
            else {
                Debug.LogWarning($"{gameObject.name}: Panel is null!");
            }
        };
        botan.onClick += (e) => {
            Debug.Log("clicked");
            if (popupPanel != null && popupCanvasGroup != null) {
                StartCoroutine(ShowPopupWithFadeAndMusicSlide());
                isPopupShown = true;
                this.GetBus().Publish(new AppMessages.SelectStage(new StageId("どっちか")));
            }
        };
        botan.onHoverExit += (e) => {
            if (isPopupShown) return;
            image.color = Color.gray;
            Debug.Log("hover exited");
            
            isHovering = false;
            hasCompletedMove = false;
            
            // ホバーが離れたときにPanelをデフォルト位置に戻す（blackもフェードアウト）
            if(panel != null) {
                panel.MoveToDefault();
            }
            
            // blackをフェードアウト
            if (blackCanvasGroup != null)
            {
                LeanTween.cancel(blackObject);
                LeanTween.alphaCanvas(blackCanvasGroup, 0f, blackFadeDuration).setEase(LeanTweenType.easeInQuad);
            }
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
    
    void OnPanelMoveComplete()
    {
        Debug.Log($"{gameObject.name} OnPanelMoveComplete - isHovering: {isHovering}, hasCompletedMove: {hasCompletedMove}");
        
        // ホバー中で、まだフェードインしていない場合のみ実行
        if (isHovering && !hasCompletedMove)
        {
            hasCompletedMove = true;

            // blackオブジェクトをフェードイン
            if (blackCanvasGroup != null)
            {
                Debug.Log($"{gameObject.name} fading in black object");
                LeanTween.cancel(blackObject);
                LeanTween.alphaCanvas(blackCanvasGroup, 1f, blackFadeDuration).setEase(LeanTweenType.easeOutQuad);
            }
            else
            {
                Debug.LogWarning($"{gameObject.name}: blackCanvasGroup is null!");
            }
        }
    }
    
    void OnDestroy()
    {
        // イベント購読解除
        if (panel != null)
        {
            panel.OnRightMoveComplete -= OnPanelMoveComplete;
        }
    }
    // Update is called once per frame
    void Update()
    {
        
    }
}
