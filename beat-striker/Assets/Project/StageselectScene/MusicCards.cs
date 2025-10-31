using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System;
using Core;
using Core.App.Types;

public class MusicCards : MonoBehaviour
{
    [Header("Card Prefab")]
    public MusicCard cardPrefab;
    public List<SelectableMusic> musics = new();
    private List<MusicCard> cards = new();

    [Header("Buttons")]
    public Botan rightButton;
    public Botan leftButton;

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

    void Start()
    {
        cards.Clear();
        for (int i = 0; i < musics.Count; i++) {
            var card = Instantiate(cardPrefab, transform);
            card.name = "Card_" + i;
            card.SetMusic(musics[i]);
            cards.Add(card);
        }
        cards.Reverse();

        rightButton.onClick += e => OnRightPressed();
        leftButton.onClick += e => OnLeftPressed();
    }

    public void OnRightPressed()
    {
        if (isAnimating) return;
        isAnimating = true;

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
    }

    public void OnLeftPressed()
    {
        if (isAnimating) return;
        isAnimating = true;

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
    }
}

[Serializable]
public struct SelectableMusic {
    public string description;
    public AudioClip clip;
    public TrackId trackId;
}