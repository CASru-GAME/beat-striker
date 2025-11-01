using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections;
using Core.Utils;
using Core.App.Presenters.Scene.Types;
using Core.App.Types;

public class Backbutton : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    public AppScene previousScene = AppScene.StageSelect;
    public AudioClip clickSound;
    
    [Header("Debounce Settings")]
    [Tooltip("連続クリック防止の間隔（秒）")]
    [Range(0f, 1f)]
    public float debounceTime = 0.1f;
    
    AudioSource audioSource;
    private float lastClickTime = -999f;
    private bool isProcessing = false;
    
    void Start()
    {
        audioSource = GetComponent<AudioSource>();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        // 連続クリック防止（デバウンス）& 処理中チェック
        float currentTime = Time.unscaledTime;
        if (isProcessing || currentTime - lastClickTime < debounceTime)
        {
            return;
        }
        lastClickTime = currentTime;
        isProcessing = true;
        
        if (clickSound != null && audioSource != null)
         {
            audioSource.PlayOneShot(clickSound);
             StartCoroutine(GoToSceneAfterSound());
        }
        else
        {
            this.GetBus().Publish(new AppMessages.RequireTransition(previousScene));
        }
    }
    IEnumerator GoToSceneAfterSound()
    {
        yield return new WaitForSeconds(0.2f);
        this.GetBus().Publish(new AppMessages.RequireTransition(previousScene));
    }

    public void OnPointerEnter(PointerEventData eventData) {

    }

    public void OnPointerExit(PointerEventData eventData)
    {

    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
