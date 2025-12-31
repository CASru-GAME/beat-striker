
using Core;
using Core.App.Presenters.Scene.Types;
using Core.App.Types;
using Core.Utils;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.TextCore.Text;
using Core.App.Presenters.Scene.States;
using System.Collections;
using System.Linq;
using Core.App.Installers;

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
    [SerializeField] string strikerId;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Awake() {
        botan = GetComponent<Botan>();
        audioSource = GetComponent<AudioSource>();

        image.color = Color.gray;
        if (image2 != null) image2.color = Color.gray;
        if (text != null) text.color = Color.gray;

        botan.onHover += (e) => {
            image.color = Color.white;
            if (image2 != null) image2.color = Color.white;
            if (text != null) text.color = Color.white;
            if (hoverSound != null && audioSource != null) {
                audioSource.PlayOneShot(hoverSound);
            }
            int playerId = e.EventData.pointerId;
            this.GetBus().Publish(new AppMessages.SelectStriker(new PlayerId(playerId), null));
        };
        botan.onClick += (e) => {
            int playerId = e.EventData.pointerId;
            this.GetBus().Publish(new AppMessages.SelectStriker(new PlayerId(playerId), new StrikerId(strikerId)));
        };
        botan.onHoverExit += (e) => {
            image.color = Color.gray;
            if (image2 != null) image2.color = Color.gray;
            if (text != null) text.color = Color.gray;

            
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
}
