using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Threading.Tasks;
using System.Collections.Generic;
using VContainer;
using VContainer.Unity;
using R3;
using Alice;
using App;
using TMPro;

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
    [SerializeField] TMP_Text player1ScoreText;
    [SerializeField] TMP_Text player2ScoreText;
    [SerializeField] TMP_Text player1ExcellentText;
    [SerializeField] TMP_Text player2ExcellentText;
    [SerializeField] TMP_Text player1GoodText;
    [SerializeField] TMP_Text player2GoodText;
    [SerializeField] TMP_Text player1MissText;
    [SerializeField] TMP_Text player2MissText;

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

    void Awake()
    {
        EnsureDependenciesInjected();
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

    void EnsureDependenciesInjected()
    {
        if (playerSelectSetting != null && appStrikerRegistry != null && gamePadRegistry != null)
        {
            return;
        }

        var battleScope = LifetimeScope.Find<BattleScope>(gameObject.scene);
        if (battleScope != null && battleScope.Container != null)
        {
            battleScope.Container.Inject(this);
            return;
        }

        var appScope = LifetimeScope.Find<AppScope>();
        if (appScope != null && appScope.Container != null)
        {
            appScope.Container.Inject(this);
        }
    }

    public void GotoSelectScene()
    {
        SceneManager.LoadScene("SelectScene");
    }

    public void ShowResult(IReadOnlyDictionary<PlayerId, BeatPlayerBattleResult> battleResults)
    {
        LoadStrikerPortraits();
        ApplyBattleResults(battleResults);
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

    void ApplyBattleResults(IReadOnlyDictionary<PlayerId, BeatPlayerBattleResult> battleResults)
    {
        var player1Result = battleResults[new PlayerId(0)];
        var player2Result = battleResults[new PlayerId(1)];

        player1ScoreText.text = player1Result.Score.ToString();
        player2ScoreText.text = player2Result.Score.ToString();
        player1ExcellentText.text = player1Result.Excellent.ToString();
        player2ExcellentText.text = player2Result.Excellent.ToString();
        player1GoodText.text = player1Result.Good.ToString();
        player2GoodText.text = player2Result.Good.ToString();
        player1MissText.text = player1Result.Miss.ToString();
        player2MissText.text = player2Result.Miss.ToString();
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
