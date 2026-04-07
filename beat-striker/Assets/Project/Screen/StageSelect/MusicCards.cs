using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using R3;
using Alice;
using Core;

public class MusicCards : MonoBehaviour
{
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

    bool isAnimating = false;
    int currentIndex = 0;
    bool initialized;
    readonly Subject<MusicInfo> musicSelected = new();
    readonly CompositeDisposable subscriptions = new();

    public Observable<MusicInfo> OnMusicSelected => musicSelected;

    public void Initialize(IReadOnlyList<MusicInfo> musics) {
        if (initialized) return;

        cards.Clear();
        for (int i = 0; i < musics.Count; i++) {
            var card = Instantiate(cardPrefab, transform);
            card.name = "Card_" + i;
            card.OnMusicSelected.Subscribe(HandleMusicSelected).AddTo(subscriptions);
            card.SetMusic(musics[i]);
            cards.Add(card);
        }
        cards.Reverse();
        ApplyPreviewState();

        rightButton.OnClickEvent.Subscribe(e => OnRightPressed()).AddTo(subscriptions);
        leftButton.OnClickEvent.Subscribe(e => OnLeftPressed()).AddTo(subscriptions);
        initialized = true;
    }

    void HandleMusicSelected(MusicInfo musicInfo) {
        musicSelected.OnNext(musicInfo);
    }

    public void OnRightPressed()
    {
        if (isAnimating) return;
        isAnimating = true;
        
        // クリック効果音を再生
        PlayClickSound();

        MusicCard currentCard = cards[currentIndex];
        int nextIndex = (currentIndex + 1) % cards.Count;

        Vector3 currentPos = currentCard.transform.localPosition;
        
        LeanTween.cancel(currentCard.gameObject);

        LeanTween.moveLocalX(currentCard.gameObject, -slideDistance, leftSlideDuration).setEase(LeanTweenType.easeInOutQuad).setOnComplete(() =>
        {
            currentCard.transform.SetAsFirstSibling();

            LeanTween.moveLocalX(currentCard.gameObject, 0f, rightSlideDuration).setEase(LeanTweenType.easeOutQuad).setOnComplete(() =>
            {
                currentCard.transform.localPosition = new Vector3(0f, currentPos.y, 0f);
                isAnimating = false;
            });
        });

        currentIndex = nextIndex;
        ApplyPreviewState();
    }

    public void OnLeftPressed()
    {
        if (isAnimating) return;
        isAnimating = true;
        
        // クリック効果音を再生
        PlayClickSound();

        int prevIndex = (currentIndex - 1 + cards.Count) % cards.Count;
        MusicCard prevCard = cards[prevIndex];

        Vector3 currentPos = prevCard.transform.localPosition;
        
        LeanTween.cancel(prevCard.gameObject);

        LeanTween.moveLocalX(prevCard.gameObject, -slideDistance, leftSlideDuration).setEase(LeanTweenType.easeInOutQuad).setOnComplete(() =>
        {
            prevCard.transform.SetAsLastSibling();

            LeanTween.moveLocalX(prevCard.gameObject, 0f, rightSlideDuration).setEase(LeanTweenType.easeOutQuad).setOnComplete(() =>
            {
                prevCard.transform.localPosition = new Vector3(0f, currentPos.y, 0f);
                isAnimating = false;
            });
        });

        currentIndex = prevIndex;
        ApplyPreviewState();
    }

    void ApplyPreviewState()
    {
        for (int i = 0; i < cards.Count; i++)
        {
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