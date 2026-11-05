
using Core;
using Alice;
using R3;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public record StrikerClickRequest(int PlayerId, Striker Striker);

[RequireComponent(typeof(Botan))]
public class Characterselectbutton : MonoBehaviour
{
    Botan botan;
    public RawImage image;
    public RawImage image2; // 追加の画像
    public TextMeshProUGUI text; // 追加のテキスト
    public AudioClip hoverSound;
    [SerializeField] Striker striker;
    [SerializeField] GameObject unavailableOverlay;
    public Striker Striker => striker;

    readonly Subject<StrikerClickRequest> strikerClicked = new();
    public Observable<StrikerClickRequest> OnStrikerClicked => strikerClicked;
    void Awake() {
        botan = GetComponent<Botan>();

        SetHighlighted(false);

        botan.OnHoverEvent.Subscribe((e) => {
            SetHighlighted(true);
            hoverSound.PlayAtApp();
        });
        botan.OnClickEvent.Subscribe((e) => {
            var playerId = e.EventData.pointerId;
            strikerClicked.OnNext(new StrikerClickRequest(playerId, striker));
        });
        botan.OnHoverExitEvent.Subscribe(_ => SetHighlighted(false));
    }

    public void SetInteractable(bool interactable) {
        botan.Interactable = interactable;
        if (unavailableOverlay != null) {
            unavailableOverlay.SetActive(!interactable);
        }
        if (!interactable) {
            SetHighlighted(false);
        }
    }

    void SetHighlighted(bool highlighted) {
        var color = highlighted ? Color.white : Color.gray;
        image.color = color;
        image2.color = color;
        text.color = color;
    }

    public void PlayClickFeedback(Transform clickTarget, float scaleDownAmount = 0.9f, float scaleDuration = 0.1f) {
        if (clickTarget != null) {
            // 元のスケールをキャンセル
            LeanTween.cancel(clickTarget.gameObject);

            // へこんで戻る
            LeanTween.scale(clickTarget.gameObject, Vector3.one * scaleDownAmount, scaleDuration)
                .setEase(LeanTweenType.easeOutQuad)
                .setOnComplete(() => {
                    LeanTween.scale(clickTarget.gameObject, Vector3.one, scaleDuration)
                        .setEase(LeanTweenType.easeOutQuad);
                });
        }
    }

    void OnDestroy() {
        strikerClicked.Dispose();
    }
}
