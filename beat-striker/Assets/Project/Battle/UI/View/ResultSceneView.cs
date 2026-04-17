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
    [SerializeField] TMP_Text player1ScoreSubText;
    [SerializeField] TMP_Text player2ScoreSubText;
    [SerializeField] TMP_Text player1ComboText;
    [SerializeField] TMP_Text player2ComboText;
    [SerializeField] TMP_Text player1ComboSubText;
    [SerializeField] TMP_Text player2ComboSubText;
    [SerializeField] TMP_Text player1ExcellentText;
    [SerializeField] TMP_Text player2ExcellentText;
    [SerializeField] TMP_Text player1GoodText;
    [SerializeField] TMP_Text player2GoodText;
    [SerializeField] TMP_Text player1MissText;
    [SerializeField] TMP_Text player2MissText;
    [SerializeField] Image[] player1RoundWinImages = new Image[3];
    [SerializeField] Image[] player2RoundWinImages = new Image[3];
    [SerializeField] Color roundWinColor = Color.green;
    [SerializeField] Color roundNeutralColor = Color.white;

    public GameObject ResultRoot => resultRoot;
    public ResultPanelButton ResultPanelButton => resultPanelButton;
    public Image Player1PortraitImage => player1PortraitImage;
    public Image Player2PortraitImage => player2PortraitImage;
    public TMP_Text Player1ScoreText => player1ScoreText;
    public TMP_Text Player2ScoreText => player2ScoreText;
    public TMP_Text Player1ScoreSubText => player1ScoreSubText;
    public TMP_Text Player2ScoreSubText => player2ScoreSubText;
    public TMP_Text Player1ComboText => player1ComboText;
    public TMP_Text Player2ComboText => player2ComboText;
    public TMP_Text Player1ComboSubText => player1ComboSubText;
    public TMP_Text Player2ComboSubText => player2ComboSubText;
    public TMP_Text Player1ExcellentText => player1ExcellentText;
    public TMP_Text Player2ExcellentText => player2ExcellentText;
    public TMP_Text Player1GoodText => player1GoodText;
    public TMP_Text Player2GoodText => player2GoodText;
    public TMP_Text Player1MissText => player1MissText;
    public TMP_Text Player2MissText => player2MissText;
    public Image[] Player1RoundWinImages => player1RoundWinImages;
    public Image[] Player2RoundWinImages => player2RoundWinImages;
    public Color RoundWinColor => roundWinColor;
    public Color RoundNeutralColor => roundNeutralColor;

    public void InitializeInactiveState()
    {
        resultRoot.SetActive(false);
    }

    public void ShowRoot()
    {
        resultRoot.SetActive(true);
    }
}
