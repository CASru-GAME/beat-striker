using System;
using Core.App;
using Core.App.Installers;
using Core.App.Interfaces;
using Core.App.Presenters.Scene.Types;
using Core.App.Types;
using Core.Utils;
using TMPro;
using UnityEngine;

public class Selecter : MonoBehaviour {
    public float offsetY = 0f;
    public PlayerId playerId;
    private bool isSelected = false;
    public TextMeshProUGUI text;

    public float scaleUpFactor = 1.1f;
    public float animationDuration = 0.1f;

    private IAppModel appModel;
    private IDisposable cursorPositionSub;
    private IDisposable strikerSelectSub;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Awake() {
        appModel = AppFlowScope.GetInstance().GetAppModel();
        cursorPositionSub = appModel.SubscribeCursorPositionUpdated(OnCursorPositionUpdated);
        strikerSelectSub = appModel.SubscribeSelectStriker(OnSelectStriker);
        if (text != null) {
            text.text = $"{playerId.value + 1}P";
        }
    }

    void OnCursorPositionUpdated(CursorPositionUpdate update) {
        if (isSelected || !update.playerId.Equals(playerId)) return;
        transform.position = update.position + new Vector2(0, offsetY);
    }

    void OnSelectStriker(StrikerSelection selection) {
        if (!selection.playerId.Equals(playerId)) return;
        if (selection.strikerId.HasValue) {
            PlaySelectAnimation();
        }
        isSelected = selection.strikerId.HasValue;
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
        cursorPositionSub?.Dispose();
        strikerSelectSub?.Dispose();
    }
}
