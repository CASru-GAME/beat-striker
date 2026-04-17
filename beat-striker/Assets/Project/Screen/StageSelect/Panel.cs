using Unity.Cinemachine;
using UnityEngine;
using System;

public class Panel : MonoBehaviour
{
    public Vector3 defaultPosition;
    public Vector3 rightPosition = new Vector3(100, 0, 0); // 右に動かす座標
    public Vector3 leftPosition = new Vector3(-100, 0, 0); // 左に動かす座標
    public float moveSpeed = 10f; // 動く速度
    public GameObject blackObject; // blackのImageオブジェクト参照
    public float fadeDuration = 0.5f; // フェード時間
    private Vector3 targetPosition;
    private bool isMovingRight = false;
    private bool hasCompletedRightMove = false;
    private CanvasGroup blackCanvasGroup;
    public event Action OnRightMoveComplete;
    void Start() {
        defaultPosition = transform.localPosition;
        targetPosition = defaultPosition;
        
        // blackオブジェクトにCanvasGroupを追加/取得して初期状態で非表示に
        if (blackObject != null)
        {
            blackCanvasGroup = blackObject.GetComponent<CanvasGroup>();
            if (blackCanvasGroup == null)
            {
                blackCanvasGroup = blackObject.AddComponent<CanvasGroup>();
            }
            blackCanvasGroup.alpha = 0f;
        }
    }
    public void MoveRight()
    {
        targetPosition = rightPosition;
        isMovingRight = true;
        hasCompletedRightMove = false;
    }

    public void MoveLeft()
    {
        targetPosition = leftPosition;
    }
    
    public void MoveToDefault()
    {
        targetPosition = defaultPosition;
        
        // デフォルト位置に戻るときはblackをフェードアウト
        if (blackCanvasGroup != null)
        {
            LeanTween.cancel(blackObject);
            LeanTween.alphaCanvas(blackCanvasGroup, 0f, fadeDuration).setEase(LeanTweenType.easeInQuad);
        }
        
        isMovingRight = false;
        hasCompletedRightMove = false;
    }

    public void ResetPosition()
    {
        targetPosition = defaultPosition;
        isMovingRight = false;
        hasCompletedRightMove = false;
        
        // リセット時はblackオブジェクトを即座に非表示に
        if (blackCanvasGroup != null)
        {
            LeanTween.cancel(blackObject);
            blackCanvasGroup.alpha = 0f;
        }
    }
    void Update()
    {
        Vector3 previousPosition = transform.localPosition;
        transform.localPosition = Vector3.Lerp(transform.localPosition, targetPosition, Time.deltaTime * moveSpeed);
        
        // 右移動が完了したかチェック
        if (isMovingRight && !hasCompletedRightMove)
        {
            float distance = Vector3.Distance(transform.localPosition, rightPosition);
            if (distance < 0.1f) // 十分近づいた
            {
                hasCompletedRightMove = true;
                isMovingRight = false;
                
                // blackオブジェクト（Image+子テキスト）をフェードイン
                if (blackCanvasGroup != null)
                {
                    LeanTween.cancel(blackObject);
                    LeanTween.alphaCanvas(blackCanvasGroup, 1f, fadeDuration).setEase(LeanTweenType.easeOutQuad);
                }
                
                // イベント発火
                OnRightMoveComplete?.Invoke();
            }
        }
    }
}
