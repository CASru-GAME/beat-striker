using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ResultSceneView : MonoBehaviour
{
    [SerializeField] GameObject resultRoot;
    [SerializeField] ResultPanelButton resultPanelButton;
    [SerializeField] Image player1PortraitImage;
    [SerializeField] Image player2PortraitImage;
    [SerializeField] TMP_Text player1ScoreText;
    [SerializeField] TMP_Text player2ScoreText;
    [SerializeField] TMP_Text player1ExcellentText;
    [SerializeField] TMP_Text player2ExcellentText;
    [SerializeField] TMP_Text player1GoodText;
    [SerializeField] TMP_Text player2GoodText;
    [SerializeField] TMP_Text player1MissText;
    [SerializeField] TMP_Text player2MissText;

    public GameObject ResultRoot => resultRoot;
    public ResultPanelButton ResultPanelButton => resultPanelButton;
    public Image Player1PortraitImage => player1PortraitImage;
    public Image Player2PortraitImage => player2PortraitImage;
    public TMP_Text Player1ScoreText => player1ScoreText;
    public TMP_Text Player2ScoreText => player2ScoreText;
    public TMP_Text Player1ExcellentText => player1ExcellentText;
    public TMP_Text Player2ExcellentText => player2ExcellentText;
    public TMP_Text Player1GoodText => player1GoodText;
    public TMP_Text Player2GoodText => player2GoodText;
    public TMP_Text Player1MissText => player1MissText;
    public TMP_Text Player2MissText => player2MissText;

    public void InitializeInactiveState()
    {
        resultRoot.SetActive(false);
    }

    public void ShowRoot()
    {
        resultRoot.SetActive(true);
    }
}
