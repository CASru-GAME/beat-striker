
using UnityEngine;
using Core;
using UnityEngine.UI;
using R3;

[RequireComponent(typeof(Botan))]
public class Backbutton : MonoBehaviour {
    private Botan button;
    public RawImage image;
    public float hoveredAlpha = 0.9f;
    public float hoveredScale = 1.1f;
    public float scaleAnimationDuration = 0.2f;
    private float originalAlpha;
    private Vector3 originalScale;
    public Observable<Unit> OnBackPressed => onBackPressed;
    private readonly Subject<Unit> onBackPressed = new();

    void Awake() {
        button = GetComponent<Botan>();
        button.onClick += data => {
            onBackPressed.OnNext(Unit.Default);
        };
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


}
