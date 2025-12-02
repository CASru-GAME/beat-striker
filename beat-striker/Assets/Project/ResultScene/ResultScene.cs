using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Core.App.Installers;
using Core.App.Types;
using Core.Utils;

public class ResultScene : MonoBehaviour
{
    [SerializeField] Image player1PortraitImage; // Player1の顔写真を表示するImage
    [SerializeField] Image player2PortraitImage; // Player2の顔写真を表示するImage

    private IBus bus;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        bus = this.GetBus();
        LoadStrikerPortraits();
    }

    // Update is called once per frame
    void Update()
    {

    }

    public void GotoSelectScene()
    {
        SceneManager.LoadScene("SelectScene");
    }

    void LoadStrikerPortraits()
    {
        // AppFlowScopeからストライカー情報を取得
        var appFlowScope = AppFlowScope.GetInstance();
        if (appFlowScope == null)
        {
            Debug.LogError("AppFlowScope instance not found!");
            return;
        }

        // BattleSettingModelからストライカーIDを取得
        var battleSettingModel = appFlowScope.battleSettingModel;
        if (battleSettingModel == null)
        {
            Debug.LogError("BattleSettingModel not found!");
            return;
        }

        // Player1のストライカーIDを取得して顔写真を設定
        var player1StrikerId = battleSettingModel.GetStriker(new PlayerId(0));
        if (player1PortraitImage != null && player1StrikerId != null)
        {
            var portrait = appFlowScope.GetStrikerPortrait(player1StrikerId.Value);
            if (portrait != null)
            {
                player1PortraitImage.sprite = portrait;
                Debug.Log($"Player1 portrait set for StrikerId: {player1StrikerId}");
            }
        }

        // Player2のストライカーIDを取得して顔写真を設定
        var player2StrikerId = battleSettingModel.GetStriker(new PlayerId(1));
        if (player2PortraitImage != null && player2StrikerId != null)
        {
            var portrait = appFlowScope.GetStrikerPortrait(player2StrikerId.Value);
            if (portrait != null)
            {
                player2PortraitImage.sprite = portrait;
                Debug.Log($"Player2 portrait set for StrikerId: {player2StrikerId}");
            }
        }
    }
}
