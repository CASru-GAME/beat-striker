using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.EventSystems;
public class Popupimage : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public AudioClip musicClip;
    AudioSource audioSource;
    public Vector3 normalScale = Vector3.one;
    public Vector3 hoverScale = new Vector3(1.1f, 1.1f, 1f);
    public float scaleSpeed = 8f;
    private Vector3 targetScale;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start() {
        audioSource = GetComponent<AudioSource>();
        transform.localScale = normalScale;
        targetScale = normalScale;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (musicClip != null && audioSource != null)
        {
            audioSource.clip = musicClip;
            audioSource.Play();
        }
        targetScale = hoverScale;
        // ポインターがオブジェクトに入ったときの処理
        Debug.Log("Pointer Entered");
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (audioSource != null)
        {
            audioSource.Stop();
        }
        targetScale = normalScale;
        // ポインターがオブジェクトから出たときの処理
        Debug.Log("Pointer Exited");
    }

    // Update is called once per frame
    void Update()
    {
        transform.localScale = Vector3.Lerp(transform.localScale, targetScale, Time.deltaTime * scaleSpeed);
    }
}
