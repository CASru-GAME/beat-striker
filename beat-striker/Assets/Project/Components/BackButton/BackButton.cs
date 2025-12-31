using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections;
using Core.Utils;
using Core.App.Presenters.Scene.Types;
using Core.App.Types;
using Core;
using UnityEngine.UI;

[RequireComponent(typeof(Botan))]
public class Backbutton : MonoBehaviour {
    private Botan button;
    public AppScene previousScene;
    public RawImage image;
    public float hoveredAlpha = 0.9f;
    public float hoveredScale = 1.1f;
    public float scaleAnimationDuration = 0.2f;
    private float originalAlpha;
    private Vector3 originalScale;

    void Awake() {
        button = GetComponent<Botan>();
        button.onClick += GoToSceneAfterSound;
        originalAlpha = image.color.a;
        originalScale = transform.localScale;
        button.onHover += data => {
            var col = image.color;
            col.a = hoveredAlpha;
            image.color = col;
            LeanTween.cancel(gameObject);
            LeanTween.scale(gameObject, originalScale * hoveredScale, scaleAnimationDuration).setEaseOutBack();
        };
        button.onHoverExit += data => {
            var col = image.color;
            col.a = originalAlpha;
            image.color = col;
            LeanTween.cancel(gameObject);
            LeanTween.scale(gameObject, originalScale, scaleAnimationDuration).setEaseOutBack();
        };
    }

    void GoToSceneAfterSound(BotanEventData data) {
        this.GetBus().Publish(new AppMessages.RequireTransition(previousScene));

    }

}
