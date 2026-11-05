using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using R3;
using Alice;
using Core;

public class MusicCards : MonoBehaviour
{
    readonly IReadOnlyDictionary<string, MusicCardAddressableAssets> emptyAssetsByMusicId = new Dictionary<string, MusicCardAddressableAssets>();
    [Header("Card Prefab")]
    public MusicCard cardPrefab;
    private List<MusicCard> cards = new();

    [Header("Buttons")]
    public Botan rightButton;
    public Botan leftButton;
    
    [Header("Sound")]
    public AudioClip buttonClickSound; // ボタンクリック時の効果音
    [Range(0f, 1f)]
    public float soundVolume = 1f; // 効果音の音量
    
    [Header("Motion")]
    public float slideDistance = 600f;
    public float leftSlideDuration = 0.3f;
    public float upMoveDuration = 0.2f;
    public float rightSlideDuration = 0.3f;
    public float upMoveDistance = 100f;
    public float depthMove = 60f;
    public float depthAnimDuration = 0.3f;
    [SerializeField] RectTransform cardContainer;

    bool isAnimating = false;
    int currentIndex = 0;
    bool initialized;
    readonly Subject<MusicInfo> musicSelected = new();
    readonly CompositeDisposable subscriptions = new();

    public Observable<MusicInfo> OnMusicSelected => musicSelected;

    public void Initialize(IReadOnlyList<MusicInfo> musics, IReadOnlyDictionary<string, MusicCardAddressableAssets> preloadedAssetsByMusicId) {
        if (initialized) return;
        initialized = true;
        BuildCards(musics ?? System.Array.Empty<MusicInfo>(), preloadedAssetsByMusicId ?? emptyAssetsByMusicId);
    }

    void BuildCards(IReadOnlyList<MusicInfo> musics, IReadOnlyDictionary<string, MusicCardAddressableAssets> preloadedAssetsByMusicId) {
        if (cardContainer == null) {
            Debug.LogError("[MusicCards] cardContainer is not assigned.");
            return;
        }

        if (cardPrefab == null) {
            Debug.LogError("[MusicCards] cardPrefab is not assigned.");
            return;
        }

        cards.Clear();
        cardContainer.gameObject.SetActive(false);
        for (int i = musics.Count - 1; i >= 0; i--) {
            var card = Instantiate(cardPrefab, cardContainer);
            card.name = "Card_" + i;
            card.OnMusicSelected.Subscribe(HandleMusicSelected).AddTo(subscriptions);
            var music = musics[i];
            preloadedAssetsByMusicId.TryGetValue(music.Id, out var addressableAsset);
            card.SetMusic(music, addressableAsset);
            cards.Add(card);
        }
        cardContainer.gameObject.SetActive(true);
        ApplyPreviewState();

        if (rightButton != null) {
            rightButton.OnClickEvent.Subscribe(e => OnRightPressed()).AddTo(subscriptions);
        }

        if (leftButton != null) {
            leftButton.OnClickEvent.Subscribe(e => OnLeftPressed()).AddTo(subscriptions);
        }
    }

    void HandleMusicSelected(MusicInfo musicInfo) {
        if (isAnimating) return;
        musicSelected.OnNext(musicInfo);
    }

    public void OnRightPressed()
    {
        SlideToAdjacentCard(moveToBack: true);
    }

    public void OnLeftPressed()
    {
        SlideToAdjacentCard(moveToBack: false);
    }

    void SlideToAdjacentCard(bool moveToBack)
    {
        if (cards.Count < 2) return;
        if (isAnimating) return;
        isAnimating = true;
        PlayClickSound();

        int previousIndex = currentIndex;
        int direction = moveToBack ? 1 : -1;
        currentIndex = (previousIndex + direction + cards.Count) % cards.Count;
        MusicCard movingCard = cards[moveToBack ? previousIndex : currentIndex];
        float restingX = movingCard.transform.localPosition.x;
        int frontSiblingIndex = cardContainer.childCount - 1;
        LeanTween.cancel(movingCard.gameObject);
        movingCard.transform.SetSiblingIndex(moveToBack ? frontSiblingIndex : 0);
        ApplyPreviewState(previousIndex);

        LeanTween.moveLocalX(movingCard.gameObject, restingX + direction * slideDistance, leftSlideDuration).setEase(LeanTweenType.easeInOutQuad).setOnComplete(() =>
        {
            movingCard.transform.SetSiblingIndex(moveToBack ? 0 : frontSiblingIndex);

            LeanTween.moveLocalX(movingCard.gameObject, restingX, rightSlideDuration).setEase(LeanTweenType.easeOutQuad).setOnComplete(() =>
            {
                ApplyPreviewState();
                isAnimating = false;
            });
        });
    }

    void ApplyPreviewState(int transitionIndex = -1)
    {
        for (int i = 0; i < cards.Count; i++)
        {
            bool isCurrent = i == currentIndex;
            bool isTransitionCard = i == transitionIndex;
            cards[i].gameObject.SetActive(isCurrent || isTransitionCard);
            cards[i].SetPreviewEnabled(i == currentIndex);
        }
    }
    
    void PlayClickSound()
    {
        if (buttonClickSound != null)
        {
            GameObject soundObject = new GameObject("ButtonClickSound");
            AudioSource audioSource = soundObject.AddComponent<AudioSource>();
            audioSource.clip = buttonClickSound;
            audioSource.volume = soundVolume;
            audioSource.Play();
            Destroy(soundObject, buttonClickSound.length);
        }
    }

    void OnDestroy() {
        subscriptions.Dispose();
    }
}
