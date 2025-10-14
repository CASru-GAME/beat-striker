using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class CardCarouselWithRaycast : MonoBehaviour
{
    [Header("Card Prefab")]
    public GameObject cardPrefab; // Inspectorで設定するPrefab
    [Header("Card Count")]
    public int cardCount = 3; // 生成するカード枚数（必要に応じて変更）
    private List<GameObject> cards = new List<GameObject>(); // 生成したカードを保持

    [Header("Buttons")]
    public Button rightButton;
    public Button leftButton;

    [Header("Motion")]
    public float slideDistance = 600f; // 画面外へ送る距離（必要に応じて調整）
    public float leftSlideDuration = 0.3f; // 左スライドの時間
    public float upMoveDuration = 0.2f; // 上移動の時間
    public float rightSlideDuration = 0.3f; // 右スライドの時間
    public float upMoveDistance = 100f; // 上への移動距離
    public float depthMove = 60f; // 奥への擬似Z動作（ローカルZ）
    public float depthAnimDuration = 0.3f; // Z軸アニメーション時間

    bool isAnimating = false;
    int currentIndex = 0; // cards[currentIndex] が前面
    private Dictionary<GameObject, int> lastKnownOrder = new Dictionary<GameObject, int>(); // カードの前回の順番を記憶

    void Start()
    {
        // Prefabと枚数のチェック
        if (cardPrefab == null) { Debug.LogError("Card prefab not set"); return; }
        if (cardCount <= 0) { Debug.LogError("Card count must be > 0"); return; }

        cards.Clear();
        for (int i = 0; i < cardCount; i++)
        {
            var card = Instantiate(cardPrefab, transform);
            card.name = "Card_" + i;
            cards.Add(card);

        }

        // 初期表示：currentIndex のカードだけ操作可能に
        UpdateCardRaycastStates();
        UpdateCardView();

        // ボタン登録
        rightButton.onClick.AddListener(OnRightPressed);
        leftButton.onClick.AddListener(OnLeftPressed);
    }

    void UpdateCardRaycastStates()
    {
        // Raycast状態と描画順のみを更新
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
                cg.alpha = 1f;
            }
        }

        // 親階層の描画順を整える（currentIndexが最上位）
        cards[currentIndex].transform.SetAsLastSibling();
    }

    void UpdateCardView()
    {
        // カードの順番変化を検知してアニメーション
        for (int i = 0; i < cards.Count; i++)
        {
            GameObject card = cards[i];
            int currentOrder = card.transform.GetSiblingIndex();
            
            // 前回の順番と比較
            if (lastKnownOrder.ContainsKey(card))
            {
                int previousOrder = lastKnownOrder[card];
                
                // 順番が変わった場合のみアニメーション
                if (currentOrder != previousOrder)
                {
                    // 下から上に移動（前面に来た）場合
                    if (currentOrder > previousOrder)
                    {
                        // 現在位置 → 左 → 手前 → 中央
                        Vector3 currentPos = card.transform.localPosition;
                        Vector3 originalPos = new Vector3(0f, currentPos.y, 0f);
                        
                        LeanTween.cancel(card);
                        
                        // 1. X軸：現在位置から左へ (0.3s)
                        LeanTween.moveLocalX(card, -slideDistance, leftSlideDuration).setEase(LeanTweenType.easeInOutQuad).setOnComplete(() =>
                        {
                            // 2. Z軸：現在のZ位置から手前へ (0.2s)
                            LeanTween.moveLocalZ(card, 0f, upMoveDuration).setEase(LeanTweenType.easeOutQuad).setOnComplete(() =>
                            {
                                // 3. X軸：左から中央へ (0.3s)
                                LeanTween.moveLocalX(card, 0f, rightSlideDuration).setEase(LeanTweenType.easeOutQuad).setOnComplete(() =>
                                {
                                    card.transform.localPosition = originalPos;
                                    isAnimating = false;
                                });
                            });
                        });
                    }
                    // 上から下に移動（背面に回った）場合
                    else if (currentOrder < previousOrder)
                    {
                        // 現在位置 → 左 → 奥 → 中央
                        Vector3 currentPos = card.transform.localPosition;
                        float targetZ = depthMove * (cards.Count - currentOrder - 1) / (float)cards.Count;
                        
                        LeanTween.cancel(card);
                        
                        // 1. X軸：現在位置から左へ (0.3s)
                        LeanTween.moveLocalX(card, -slideDistance, leftSlideDuration).setEase(LeanTweenType.easeInOutQuad).setOnComplete(() =>
                        {
                            // 2. Z軸：現在のZ位置から奥へ (0.2s)
                            LeanTween.moveLocalZ(card, targetZ, upMoveDuration).setEase(LeanTweenType.easeInQuad).setOnComplete(() =>
                            {
                                // 3. X軸：左から中央へ (0.3s)
                                LeanTween.moveLocalX(card, 0f, rightSlideDuration).setEase(LeanTweenType.easeOutQuad).setOnComplete(() =>
                                {
                                    card.transform.localPosition = new Vector3(0f, currentPos.y, targetZ);
                                    isAnimating = false;
                                });
                            });
                        });
                    }
                }
            }
            else
            {
                // 初回は即座に位置を設定
                if (i == currentIndex)
                {
                    card.transform.localPosition = new Vector3(
                        0f,
                        card.transform.localPosition.y,
                        0f
                    );
                }
                else
                {
                    float targetZ = depthMove * (cards.Count - currentOrder - 1) / (float)cards.Count;
                    card.transform.localPosition = new Vector3(
                        0f,
                        card.transform.localPosition.y,
                        targetZ
                    );
                }
            }
            
            // 現在の順番を記録
            lastKnownOrder[card] = currentOrder;
        }
    }

    public void OnRightPressed()
    {
        if (isAnimating) return;
        isAnimating = true;

        GameObject currentCard = cards[currentIndex];
        int nextIndex = (currentIndex + 1) % cards.Count;

        UpdateAllCanvasGroupsInteractable(false);

        // 論理的な順番変更のみ
        currentCard.transform.SetAsFirstSibling(); // 背面に回す
        currentIndex = nextIndex;

        // 状態更新
        UpdateCardRaycastStates();
        
        // ビュー更新（アニメーション）
        UpdateCardView();
    }

    public void OnLeftPressed()
    {
        if (isAnimating) return;
        isAnimating = true;

        int prevIndex = (currentIndex - 1 + cards.Count) % cards.Count;
        GameObject prevCard = cards[prevIndex];

        UpdateAllCanvasGroupsInteractable(false);

        // 論理的な順番変更のみ
        prevCard.transform.SetAsLastSibling();
        currentIndex = prevIndex;

        // 状態更新
        UpdateCardRaycastStates();
        
        // ビュー更新（アニメーション）
        UpdateCardView();
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

