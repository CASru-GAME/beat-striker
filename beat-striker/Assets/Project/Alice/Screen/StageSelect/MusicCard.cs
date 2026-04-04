
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using R3;
using TMPro;
using Alice;
using Core;

[RequireComponent(typeof(AudioSource))]
public class MusicCard : MonoBehaviour {
    AudioSource audioSource;
    [SerializeField] Botan botan;
    [SerializeField] TextMeshProUGUI description, title;
    [SerializeField] AudioClip clickSound; // クリック時の効果音
    private Vector3 originalScale;
    private MusicInfo currentMusic;
    readonly Subject<MusicInfo> musicSelected = new();

    public Observable<MusicInfo> OnMusicSelected => musicSelected;

    void CacheComponents() {
        audioSource = GetComponent<AudioSource>();
    }

    void Awake() {
        CacheComponents();
        originalScale = transform.localScale;
        
        botan.onHover += (e) => {
            audioSource.Play();
            transform.localScale = originalScale * 1.1f;

        };
        botan.onHoverExit += (e) => {
            audioSource.Stop();
            transform.localScale = originalScale;
        };
        botan.onClick += (e) => {

            // クリック効果音を再生（プレビュー音楽を一旦止めて効果音を再生）
            if (clickSound != null) {
                audioSource.Stop();
                audioSource.PlayOneShot(clickSound);
            }

            musicSelected.OnNext(currentMusic);
        };
    } 

    public void SetMusic(MusicInfo music) {
        CacheComponents();
        audioSource.clip = music.AudioClip;
        description.text = music.Description;
        title.text = music.DisplayName;
        currentMusic = music;
    }

    public void OnHidden() {
        audioSource.Stop();
    }
}