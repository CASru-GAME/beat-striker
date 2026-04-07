
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
    [SerializeField]AudioSpectrum audioSpectrum;
    [SerializeField] Botan botan;
    [SerializeField] TextMeshProUGUI description, title;
    [SerializeField] AudioClip clickSound; // クリック時の効果音
    [SerializeField] float previewVolume = 1f;
    [SerializeField] float fadeInSeconds = 0.3f;
    [SerializeField] float fadeOutSeconds = 0.3f;
    private Vector3 originalScale;
    private MusicInfo currentMusic;
    private bool isPreviewEnabled;
    readonly Subject<MusicInfo> musicSelected = new();

    public Observable<MusicInfo> OnMusicSelected => musicSelected;

    void CacheComponents() {
        audioSource = GetComponent<AudioSource>();
    }

    void Awake() {
        CacheComponents();
        originalScale = transform.localScale;
        audioSource.spatialBlend = 0f;
        audioSource.loop = false;
        audioSource.volume = 0f;
        
        botan.OnHoverEvent.Subscribe((e) => {
            transform.localScale = originalScale * 1.1f;

        });
        botan.OnHoverExitEvent.Subscribe((e) => {
            transform.localScale = originalScale;
        });
        botan.OnClickEvent.Subscribe((e) => {

            // クリック効果音を再生（プレビュー音楽を一旦止めて効果音を再生）
            if (clickSound != null) {
                audioSource.Stop();
                audioSource.PlayOneShot(clickSound);
            }

            musicSelected.OnNext(currentMusic);
        });
    } 

    void Update() {
        if (!isPreviewEnabled) {
            if (audioSource.isPlaying) {
                audioSource.Stop();
            }
            audioSource.volume = 0f;
            return;
        }

        if (audioSource.clip != null && !audioSource.isPlaying) {
            audioSource.time = 0f;
            audioSource.volume = 0f;
            audioSource.Play();
        }

        if (!audioSource.isPlaying || audioSource.clip == null) {
            return;
        }

        float time = audioSource.time;
        float length = audioSource.clip.length;

        float fadeIn = fadeInSeconds <= 0f ? 1f : Mathf.Clamp01(time / fadeInSeconds);
        float fadeOut = 1f;
        if (fadeOutSeconds > 0f) {
            float fadeOutStart = Mathf.Max(0f, length - fadeOutSeconds);
            if (time >= fadeOutStart) {
                fadeOut = Mathf.Clamp01((length - time) / fadeOutSeconds);
            }
        }

        audioSource.volume = previewVolume * Mathf.Min(fadeIn, fadeOut);
    }

    public void SetMusic(MusicInfo music) {
        CacheComponents();
        audioSource.Stop();
        audioSource.clip = music.AudioClip;
        if (audioSource.clip == null) {
            return;
        }
        audioSource.time = 0f;
        audioSource.volume = 0f;
        audioSource.Play();
        if (audioSpectrum != null) {
            audioSpectrum.SetBakedSpectrumText(music.SpectrumData);
        }
        description.text = music.Description;
        title.text = music.DisplayName;
        currentMusic = music;
    }

    public void SetPreviewEnabled(bool enabled) {
        isPreviewEnabled = enabled;
        if (!isPreviewEnabled) {
            audioSource.Stop();
            audioSource.volume = 0f;
        }
    }

    public void OnHidden() {
        isPreviewEnabled = false;
        audioSource.Stop();
        audioSource.volume = 0f;
    }
}