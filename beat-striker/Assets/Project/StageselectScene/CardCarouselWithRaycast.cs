using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class CardCarouselWithRaycast : MonoBehaviour
{
    [Header("Cards (front first)")]
    public List<GameObject> cards; // 先頭が現在前面（例: A,B,C）

    [Header("Buttons")]
    public Button rightButton;
    public Button leftButton;

    [Header("Motion")]
    public float slideDistance = 600f; // 画面外へ送る距離（必要に応じて調整）
    public float duration = 0.35f;
    public float depthMove = 60f; // 奥への擬似Z動作（ローカルZ）

    bool isAnimating = false;
    int currentIndex = 0; // cards[currentIndex] が前面

    void Start()
    {
        // safety checks
        if(cards == null || cards.Count == 0) { Debug.LogError("Cards not set"); return; }
        // 全カードに CanvasGroup があることを保証
        foreach (var c in cards)
        {
            if (c.GetComponent<CanvasGroup>() == null)
                c.AddComponent<CanvasGroup>();
        }

        // 初期表示：currentIndex のカードだけ操作可能に
        UpdateCardRaycastStates();

        // ボタン登録
        rightButton.onClick.AddListener(OnRightPressed);
        leftButton.onClick.AddListener(OnLeftPressed);
    }

    void UpdateCardRaycastStates()
    {
        for (int i = 0; i < cards.Count; i++)
        {
            var cg = cards[i].GetComponent<CanvasGroup>();
            if (i == currentIndex)
            {
                cg.interactable = true;
                cg.blocksRaycasts = true;
                cg.alpha = 1f;
            }
            else
            {
                cg.interactable = false;
                cg.blocksRaycasts = false;
                cg.alpha = 1f; // 見た目はそのまま重なっていて良ければ1。不可視にしたければ0にする
            }
        }

        // 親階層の描画順を念のため整える（currentIndexが最上位）
        cards[currentIndex].transform.SetAsLastSibling();
    }

    public void OnRightPressed()
    {
        if (isAnimating) return;
        isAnimating = true;

        GameObject currentCard = cards[currentIndex];
        // 次のカードは元から下に隠れている（何も移動させない）
        int nextIndex = (currentIndex + 1) % cards.Count;
        GameObject nextCard = cards[nextIndex];

        // アニメーション：現在のカードを右へ移動して奥に沈める
        Vector3 startPos = currentCard.transform.localPosition;
        Vector3 rightPos = startPos + new Vector3(slideDistance, 0f, 0f);

        UpdateAllCanvasGroupsInteractable(false);
        // 横にスライド
        LeanTween.moveLocal(currentCard, rightPos, duration).setEase(LeanTweenType.easeInOutQuad);

        // 少し遅れて奥に下がる（擬似Z）
        LeanTween.moveLocalZ(currentCard, depthMove, duration * 0.6f).setEase(LeanTweenType.easeInQuad).setOnComplete(() =>
        {
            // 終了時に元位置に戻しつつ背面に回す（Sibling）
            currentCard.transform.localPosition = startPos;
            currentCard.transform.SetAsFirstSibling(); // 一番下にすることで背面に回る

            // front を更新（次のカードが前面扱いになる）
            currentIndex = nextIndex;

            // 描画順と Raycast 状態更新
            UpdateCardRaycastStates();

            // Depth戻し（即座に戻す）
            LeanTween.moveLocalZ(currentCard, 0f, 0.01f);

            isAnimating = false;
        });
    }

    public void OnLeftPressed()
    {
        if (isAnimating) return;
        isAnimating = true;

        // 前の（背面にいる）カードを取り出す
        int prevIndex = (currentIndex - 1 + cards.Count) % cards.Count;
        GameObject prevCard = cards[prevIndex];
        GameObject currentCard = cards[currentIndex];

        // prevCard を一旦手前（最上位）にして、左外に配置してからスライドインする
        prevCard.transform.SetAsLastSibling(); // 手前に出す（重なり順）
        Vector3 centerPos = prevCard.transform.localPosition;
        Vector3 leftOff = centerPos + new Vector3(-slideDistance, 0f, 0f);

        // 左外へ瞬時に移動してからスライドイン
        prevCard.transform.localPosition = leftOff;

        // 見えてるカードのRaycastを一旦無効（アニメ中に誤タップを防ぐ）
        UpdateAllCanvasGroupsInteractable(false);

        // 左から中央へ移動
        LeanTween.moveLocal(prevCard, centerPos, duration).setEase(LeanTweenType.easeOutQuad).setOnComplete(() =>
        {
            // prevCard が前面になったので index を更新
            currentIndex = prevIndex;

            // prevCard を最上位にして Raycast 状態を更新
            UpdateCardRaycastStates();

            isAnimating = false;
        });
    }

    void UpdateAllCanvasGroupsInteractable(bool val)
    {
        foreach (var c in cards)
        {
            var cg = c.GetComponent<CanvasGroup>();
            cg.interactable = val;
            cg.blocksRaycasts = val;
        }
    }
}

