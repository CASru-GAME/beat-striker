
using Core;
using Core.App.Presenters.Scene.Types;
using Core.App.Types;
using Core.Utils;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.TextCore.Text;
using Core.App.Presenters.Scene.States;

[RequireComponent(typeof(Botan))]
[RequireComponent(typeof(AudioSource))]
public class Characterselectbutton : MonoBehaviour
{
    public SelectScene selectScene;
    Botan botan;
    public RawImage image;
    public RawImage image2; // 追加の画像
    public TextMeshProUGUI text; // 追加のテキスト
    public AudioClip hoverSound;
    AudioSource audioSource;
    [SerializeField] string strikerId; // プレイヤーごとの選択状態
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start() {
        botan = GetComponent<Botan>();
        audioSource = GetComponent<AudioSource>();

        image.color = Color.gray;
        if (image2 != null) image2.color = Color.gray;
        if (text != null) text.color = Color.gray;
        
        botan.onHover += (e) => {
            image.color = Color.white;
            if (image2 != null) image2.color = Color.white;
            if (text != null) text.color = Color.white;
            Debug.Log("hovered");
            if(hoverSound != null && audioSource != null) {
                audioSource.PlayOneShot(hoverSound);
            }
        };
        botan.onClick += (e) => {
            this.GetBus().Publish(new AppMessages.SelectStriker(new PlayerId(e.EventData.pointerId), new StrikerId(strikerId)));
            Debug.Log($"Published SelectStriker for Player {e.EventData.pointerId} and Striker {strikerId}");
            if(!selectScene.isSelected[e.EventData.pointerId]) {
                selectScene.isSelected[e.EventData.pointerId] = true;
            }
            // 両方のプレイヤーが選択したらバトルシーンへ遷移
            if(selectScene.isSelected[0] && selectScene.isSelected[1]) {
                Debug.Log("Both players have selected their strikers. Transitioning to Battle scene.");
            }
        };
        botan.onHoverExit += (e) => {
            image.color = Color.gray;
            if (image2 != null) image2.color = Color.gray;
            if (text != null) text.color = Color.gray;
            Debug.Log("hover exited");
        };
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
