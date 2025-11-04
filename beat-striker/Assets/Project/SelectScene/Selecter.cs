using Core.App.Presenters.Scene.Types;
using Core.App.Types;
using Core.Utils;
using TMPro;
using UnityEngine;

public class Selecter : MonoBehaviour
{
    public float offsetY = 0f;
    public PlayerId playerId;
    private bool isSelected = false;
    public TextMeshProUGUI text;
    
    public float scaleUpFactor = 1.1f;
    public float animationDuration = 0.1f;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Awake() {
        this.GetBus().Subscribe<AppMessages.CursorPositionUpdated>(OnCursorPositionUpdated);
        this.GetBus().Subscribe<AppMessages.SelectStriker>(OnSelectStriker);
        text.text = $"{playerId.value + 1}P";
    }
    
    void OnCursorPositionUpdated(AppMessages.CursorPositionUpdated msg) {
        if (isSelected || !msg.playerId.Equals(playerId)) return;
        transform.position = msg.position + new Vector2(0, offsetY);
    }

    void OnSelectStriker(AppMessages.SelectStriker msg) {
        if (!msg.playerId.Equals(playerId)) return;
        if (msg.striker.HasValue) {
            PlaySelectAnimation();
        }
        isSelected = msg.striker.HasValue;
    }

    void PlaySelectAnimation() {
        Vector3 originalScale = transform.localScale;
        Vector3 scaledUp = originalScale * scaleUpFactor;
        
        LeanTween.scale(gameObject, scaledUp, animationDuration)
            .setOnComplete(() => {
                LeanTween.scale(gameObject, originalScale, animationDuration);
            });
    }

    void OnDestroy() {
        this.GetBus().Unsubscribe<AppMessages.CursorPositionUpdated>(OnCursorPositionUpdated);
        this.GetBus().Unsubscribe<AppMessages.SelectStriker>(OnSelectStriker);
    }
}
