using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using VContainer;
using Alice;

public class ResultScene : MonoBehaviour
{
    [SerializeField] Image player1PortraitImage; // Player1の顔写真を表示するImage
    [SerializeField] Image player2PortraitImage; // Player2の顔写真を表示するImage

    IPlayerSelectSetting playerSelectSetting;
    IAppStrikerRegistry appStrikerRegistry;

    [Inject]
    public void Construct(IPlayerSelectSetting playerSelectSetting, IAppStrikerRegistry appStrikerRegistry)
    {
        this.playerSelectSetting = playerSelectSetting;
        this.appStrikerRegistry = appStrikerRegistry;
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
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
        if (playerSelectSetting.TryGetStriker(0, out var player1Striker))
        {
            player1PortraitImage.sprite = appStrikerRegistry.GetByStriker(player1Striker).Portrait;
        }
        else
        {
            player1PortraitImage.sprite = appStrikerRegistry.Default.Portrait;
        }

        if (playerSelectSetting.TryGetStriker(1, out var player2Striker))
        {
            player2PortraitImage.sprite = appStrikerRegistry.GetByStriker(player2Striker).Portrait;
        }
        else
        {
            player2PortraitImage.sprite = appStrikerRegistry.Default.Portrait;
        }
    }
}
