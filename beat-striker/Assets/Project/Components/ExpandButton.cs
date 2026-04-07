using UnityEngine;
using Core;
using UnityEngine.UI;
using R3;

[RequireComponent(typeof(Botan))]
public class ExpandButton : MonoBehaviour {
    private Botan button;
    public float hoveredAlpha = 0.9f;
    public float hoveredScale = 1.1f;
    public float scaleAnimationDuration = 0.2f;
    private Vector3 originalScale;

    void Awake() {
        button = GetComponent<Botan>();
        originalScale = transform.localScale;
        button.OnHoverEvent.Subscribe(data => {
            LeanTween.cancel(gameObject);
            LeanTween.scale(gameObject, originalScale * hoveredScale, scaleAnimationDuration).setEaseOutBack();
        });
        button.OnHoverExitEvent.Subscribe(data => {
            LeanTween.cancel(gameObject);
            LeanTween.scale(gameObject, originalScale, scaleAnimationDuration).setEaseOutBack();
        });
    }

    void OnDisable() {
        this.transform.localScale = originalScale;
    }


}