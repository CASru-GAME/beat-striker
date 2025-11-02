
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
    public SelectScene selectScene;
    Botan botan;
    public RawImage image;
    public RawImage image2; // 追加の画像
    public TextMeshProUGUI text; // 追加のテキスト
    public AudioClip hoverSound;
    AudioSource audioSource;
    [SerializeField] string strikerId; // プレイヤーごとの選択状態
    private int clickCount = 0; // クリック回数をカウント
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
            // プレイヤーレジストリから現在のプレイヤー数を取得
            var appFlowScope = FindFirstObjectByType<AppFlowScope>();
            int playerCount = appFlowScope.playerRegistry.GetAllPlayerIds().Count();
            
            int playerId = e.EventData.pointerId;
            
            // プレイヤーが1人の場合、2回目のクリックを2P扱いにする
            if (playerCount == 1) {
                clickCount++;
                if (clickCount == 2) {
                    playerId = 1; // 2回目のクリックは2P扱い
                } else {
                    playerId = 0; // 1回目のクリックは1P扱い
                }
            }
            
            this.GetBus().Publish(new AppMessages.SelectStriker(new PlayerId(playerId), new StrikerId(strikerId)));
            Debug.Log($"Published SelectStriker for Player {playerId} and Striker {strikerId}");
            if(!selectScene.isSelected[playerId]) {
                selectScene.isSelected[playerId] = true;
            }
                        // 両方のプレイヤーが選択したらバトルシーンへ遷移
            if(selectScene.isSelected[0] && selectScene.isSelected[1]) {
                Debug.Log("Both players have selected their strikers. Transitioning to Battle scene in 10 seconds.");
                StartCoroutine(TransitionAfterDelay(10f));
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

    IEnumerator TransitionAfterDelay(float delay) {
        yield return new WaitForSeconds(delay);
        this.GetBus().Publish(new AppMessages.RequireTransition(AppScene.Battle));
        Debug.Log("Scene transition requested via bus.");
    }
}
