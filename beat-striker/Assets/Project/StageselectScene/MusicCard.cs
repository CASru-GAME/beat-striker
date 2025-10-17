
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using TMPro;

[RequireComponent(typeof(AudioSource))]
public class MusicCard : MonoBehaviour {
    AudioSource audioSource;
    [SerializeField] Botan botan;
    [SerializeField] TextMeshProUGUI description;
    private Vector3 originalScale;

    void Awake() {
        audioSource = GetComponent<AudioSource>();
        originalScale = transform.localScale;
        
        botan.onHover += (e) => {
            audioSource.Play();
            transform.localScale = originalScale * 1.1f;
            Debug.Log("card hovered");

        };
        botan.onHoverExit += (e) => {
            audioSource.Stop();
            transform.localScale = originalScale;
            Debug.Log("card hover exited");
        };
    } 

    public void SetMusic(SelectableMusic music) {
        audioSource.clip = music.clip;
        description.text = music.description;
    }

    public void OnHidden() {
        audioSource.Stop();
    }
}