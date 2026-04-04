
using Core;
using Alice;
using R3;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public record StrikerClickRequest(int PlayerId, Striker Striker);

[RequireComponent(typeof(Botan))]
[RequireComponent(typeof(AudioSource))]
public class Characterselectbutton : MonoBehaviour
{
    Botan botan;
    public RawImage image;
    public RawImage image2; // 追加の画像
    public TextMeshProUGUI text; // 追加のテキスト
    public AudioClip hoverSound;
    AudioSource audioSource;
    [SerializeField] Striker striker;
    public Striker Striker => striker;

    readonly Subject<StrikerClickRequest> strikerClicked = new();
    public Observable<StrikerClickRequest> OnStrikerClicked => strikerClicked;
    void Awake() {
        botan = GetComponent<Botan>();
        audioSource = GetComponent<AudioSource>();

        image.color = Color.gray;
        image2.color = Color.gray;
        text.color = Color.gray;

        botan.onHover += (e) => {
            image.color = Color.white;
            image2.color = Color.white;
            text.color = Color.white;
            audioSource.PlayOneShot(hoverSound);
        };
        botan.onClick += (e) => {
            var playerId = e.EventData.pointerId;
            strikerClicked.OnNext(new StrikerClickRequest(playerId, striker));
        };
        botan.onHoverExit += (e) => {
            image.color = Color.gray;
            image2.color = Color.gray;
            text.color = Color.gray;
        };
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
