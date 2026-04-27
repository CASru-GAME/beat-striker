
using UnityEngine;
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
    private IMusicRegistry musicRegistry;
    private bool isPreviewEnabled;
    LoadedAsset<AudioClip> previewClipAsset;
    LoadedAsset<TextAsset> spectrumAsset;
    LoadedAsset<TextAsset> beatDataAsset;
    readonly Subject<MusicInfo> musicSelected = new();
    int loadVersion;
    int loadingVersion = -1;

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
                clickSound.PlayAtApp();
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

    public async void SetMusic(MusicInfo music, IMusicRegistry musicRegistry) {
        CacheComponents();
        ReleaseLoadedAssets();
        audioSource.Stop();
        currentMusic = music;
        this.musicRegistry = musicRegistry;
        title.text = music.DisplayName;
        description.text = $"Composer: {music.Composer}\nBPM: Loading...\nLength: 0:00\n{music.Description}";
        var version = loadVersion;
        await LoadMusicAssetsAndRefreshDescriptionAsync(version);
        if (version != loadVersion || currentMusic != music) {
            return;
        }
        if (audioSource.clip == null) {
            return;
        }
        audioSource.time = 0f;
        audioSource.volume = 0f;
        audioSource.Play();
    }

    static string FormatLength(float seconds) {
        if (seconds <= 0f) {
            return "0:00";
        }

        var totalSeconds = Mathf.FloorToInt(seconds);
        var minutes = totalSeconds / 60;
        var remainSeconds = totalSeconds % 60;
        return $"{minutes}:{remainSeconds:00}";
    }

    public void SetPreviewEnabled(bool enabled) {
        isPreviewEnabled = enabled;
        if (isPreviewEnabled && currentMusic != null && musicRegistry != null && audioSource.clip == null && loadingVersion != loadVersion) {
            _ = LoadMusicAssetsAndRefreshDescriptionAsync(loadVersion);
        }
        if (!isPreviewEnabled) {
            audioSource.Stop();
            audioSource.volume = 0f;
            ReleaseLoadedAssets();
        }
    }

    public void OnHidden() {
        isPreviewEnabled = false;
        audioSource.Stop();
        audioSource.volume = 0f;
        ReleaseLoadedAssets();
    }

    void OnDestroy() {
        ReleaseLoadedAssets();
    }

    async Awaitable<int> LoadMusicAssetsAndRefreshDescriptionAsync(int version) {
        var music = currentMusic;
        var bpm = await LoadMusicAssetsAsync(version);
        if (version != loadVersion || currentMusic != music || music == null) {
            return 0;
        }

        description.text = $"Composer: {music.Composer}\nBPM: {bpm}\nLength: {FormatLength(audioSource.clip != null ? audioSource.clip.length : 0f)}\n{music.Description}";
        return bpm;
    }

    async Awaitable<int> LoadMusicAssetsAsync(int version) {
        if (currentMusic == null || musicRegistry == null) {
            return 0;
        }

        if (loadingVersion == version) {
            return 0;
        }

        loadingVersion = version;
        var music = currentMusic;
        try {
            var loadedPreviewClipAsset = await musicRegistry.LoadAudioClipAsync(music.Id);
            if (version != loadVersion || currentMusic != music) {
                loadedPreviewClipAsset.Dispose();
                return 0;
            }

            var loadedSpectrumAsset = audioSpectrum != null
                ? await musicRegistry.LoadSpectrumDataAsync(music.Id)
                : null;
            if (version != loadVersion || currentMusic != music) {
                loadedPreviewClipAsset.Dispose();
                loadedSpectrumAsset?.Dispose();
                return 0;
            }

            var loadedBeatDataAsset = await musicRegistry.LoadBeatDataAsync(music.Id);
            if (version != loadVersion || currentMusic != music) {
                loadedPreviewClipAsset.Dispose();
                loadedSpectrumAsset?.Dispose();
                loadedBeatDataAsset.Dispose();
                return 0;
            }

            previewClipAsset = loadedPreviewClipAsset;
            audioSource.clip = previewClipAsset.Asset;
            if (audioSpectrum != null) {
                spectrumAsset = loadedSpectrumAsset;
                audioSpectrum.SetBakedSpectrumText(spectrumAsset != null ? spectrumAsset.Asset : null);
            }
            beatDataAsset = loadedBeatDataAsset;
            return BeatDataParser.CalculateBpm(beatDataAsset.Asset);
        }
        finally {
            if (loadingVersion == version) {
                loadingVersion = -1;
            }
        }
    }

    void ReleaseLoadedAssets() {
        loadVersion++;
        if (audioSource != null) {
            audioSource.clip = null;
        }

        previewClipAsset?.Dispose();
        previewClipAsset = null;
        spectrumAsset?.Dispose();
        spectrumAsset = null;
        beatDataAsset?.Dispose();
        beatDataAsset = null;
    }
}