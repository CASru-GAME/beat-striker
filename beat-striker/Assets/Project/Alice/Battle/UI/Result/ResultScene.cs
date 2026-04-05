using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Threading.Tasks;
using VContainer;
using R3;
using Alice;

public class ResultScene : MonoBehaviour
{
    const int MAX_RESULT_INPUT_PLAYER_SLOTS = 8;

    enum ResultPhase
    {
        Hidden,
        Summary,
        Detail,
    }

    [SerializeField] GameObject resultRoot; // バトル画面上で表示/非表示を切り替えるリザルトUIルート
    [SerializeField] ResultPanelButton resultPanelButton; // リザルトUI再生トリガー
    [SerializeField] Image player1PortraitImage; // Player1の顔写真を表示するImage
    [SerializeField] Image player2PortraitImage; // Player2の顔写真を表示するImage

    IPlayerSelectSetting playerSelectSetting;
    IAppStrikerRegistry appStrikerRegistry;
    IGamePadRegistry gamePadRegistry;
    readonly CompositeDisposable resultInputSubscriptions = new();
    TaskCompletionSource<bool> resultEndCompletionSource;
    ResultPhase resultPhase;
    int lastPhaseAdvanceFrame;
    bool canAdvancePhase;

    [Inject]
    public void Construct(IPlayerSelectSetting playerSelectSetting, IAppStrikerRegistry appStrikerRegistry, IGamePadRegistry gamePadRegistry)
    {
        this.playerSelectSetting = playerSelectSetting;
        this.appStrikerRegistry = appStrikerRegistry;
        this.gamePadRegistry = gamePadRegistry;
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        LoadStrikerPortraits();
        InitializeInactiveState();
        resultPhase = ResultPhase.Hidden;
        lastPhaseAdvanceFrame = -1;
        canAdvancePhase = false;
    }

    // Update is called once per frame
    void Update()
    {

    }

    public void GotoSelectScene()
    {
        SceneManager.LoadScene("SelectScene");
    }

    public void ShowResult()
    {
        LoadStrikerPortraits();
        GetResultRoot().SetActive(true);
        resultPhase = ResultPhase.Summary;
        lastPhaseAdvanceFrame = -1;
        canAdvancePhase = false;
        resultEndCompletionSource?.TrySetCanceled();
        resultEndCompletionSource = new TaskCompletionSource<bool>();
        SubscribeResultStartInput();
        StartSummaryPhase();
    }

    public Task WaitForBattleEndInputAsync()
    {
        return resultEndCompletionSource.Task;
    }

    void OnDestroy()
    {
        resultInputSubscriptions.Dispose();
    }

    void InitializeInactiveState()
    {
        GetResultRoot().SetActive(false);
    }

    GameObject GetResultRoot()
    {
        return resultRoot != null ? resultRoot : gameObject;
    }

    void SubscribeResultStartInput()
    {
        resultInputSubscriptions.Clear();

        for (var playerId = 0; playerId < MAX_RESULT_INPUT_PLAYER_SLOTS; playerId++)
        {
            var playerGamePad = gamePadRegistry.Get(playerId);
            playerGamePad.OnButtonDown
                .Where(button => button == GamePadButton.East)
                .Subscribe(_ => AdvanceResultPhase())
                .AddTo(resultInputSubscriptions);
        }
    }

    async void StartSummaryPhase()
    {
        resultPanelButton.StartPhase1FromFlow();
        await resultPanelButton.WaitForPhase1CompletedAsync();
        canAdvancePhase = true;
    }

    void AdvanceResultPhase()
    {
        if (!canAdvancePhase)
        {
            return;
        }

        if (lastPhaseAdvanceFrame == Time.frameCount)
        {
            return;
        }

        lastPhaseAdvanceFrame = Time.frameCount;

        if (resultPhase == ResultPhase.Hidden)
        {
            return;
        }

        if (resultPhase == ResultPhase.Summary)
        {
            resultPhase = ResultPhase.Detail;
            canAdvancePhase = false;
            LeanTween.delayedCall(gameObject, 0f, () =>
            {
                resultPanelButton.ContinueToPhase2FromFlow();
                canAdvancePhase = true;
            });
            return;
        }

        resultPhase = ResultPhase.Hidden;
        canAdvancePhase = false;
        resultInputSubscriptions.Clear();
        resultEndCompletionSource.TrySetResult(true);
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
