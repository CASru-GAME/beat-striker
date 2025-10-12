using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.EventSystems;
public class Popupimage : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public AudioClip musicClip;
    AudioSource audioSource;
    public AudioSpectrum audioSpectrum; // AudioSpectrum参照
    public CanvasGroup spectrumCanvasGroup; // AudioSpectrumのCanvasGroup参照
    public Vector3 normalScale = Vector3.one;
    public Vector3 hoverScale = new Vector3(1.1f, 1.1f, 1f);
    public float scaleSpeed = 8f;
    public float fadeSpeed = 8f;
    private Vector3 targetScale;
    private float targetAlpha = 0f;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start() {
        audioSource = GetComponent<AudioSource>();
        if (audioSpectrum != null)
            audioSpectrum.enabled = false; // 最初は無効化
        transform.localScale = normalScale;
        targetScale = normalScale;
        if (spectrumCanvasGroup != null)
            spectrumCanvasGroup.alpha = 0f; // 最初は透明
        transform.localScale = normalScale;
        targetScale = normalScale;
        targetAlpha = 0f;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (musicClip != null && audioSource != null) {
            audioSource.clip = musicClip;
            audioSource.Play();
        }
        if (audioSpectrum != null)
            audioSpectrum.enabled = true; // ポインターが乗ったら有効化
        targetScale = hoverScale;
        targetAlpha = 1f;
        // ポインターがオブジェクトに入ったときの処理
        Debug.Log("Pointer Entered");
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (audioSource != null) {
            audioSource.Stop();
        }
        if (audioSpectrum != null)
            audioSpectrum.enabled = false; // ポインターが外れたら無効化
        targetScale = normalScale;
        targetAlpha = 0f;
        // ポインターがオブジェクトから出たときの処理
        Debug.Log("Pointer Exited");
    }

    // Update is called once per frame
    void Update()
    {
        transform.localScale = Vector3.Lerp(transform.localScale, targetScale, Time.deltaTime * scaleSpeed);
        if (spectrumCanvasGroup != null)
         {
            spectrumCanvasGroup.alpha = Mathf.Lerp(spectrumCanvasGroup.alpha, targetAlpha, Time.deltaTime * fadeSpeed);
            spectrumCanvasGroup.blocksRaycasts = spectrumCanvasGroup.alpha > 0.01f;
            spectrumCanvasGroup.interactable = spectrumCanvasGroup.alpha > 0.01f;
        }
    }
}
