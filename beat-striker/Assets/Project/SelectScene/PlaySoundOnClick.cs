using UnityEngine;
using Core;

public class PlaySoundOnClick : MonoBehaviour
{
    private AudioSource audioSource;
    private Botan botan;
    
    void Start()
    {
        audioSource = GetComponent<AudioSource>();
        botan = GetComponent<Botan>();
        
        if (botan != null)
        {
            botan.onClick += OnClick;
        }
    }
    
    void OnClick(BotanEventData data)
    {
        if (audioSource != null && audioSource.clip != null)
        {
            audioSource.Play();
        }
    }
    
    void OnDestroy()
    {
        if (botan != null)
        {
            botan.onClick -= OnClick;
        }
    }
}
