using UnityEngine;
using UnityEngine.SceneManagement;
using System.Threading.Tasks;
using System.Collections.Generic;
using R3;
using Alice;
using App;

public class ResultScene : System.IDisposable
{
    const int MAX_RESULT_INPUT_PLAYER_SLOTS = 8;

    enum ResultPhase
    {
        Hidden,
        Summary,
        Detail,
    }

    readonly IPlayerSelectSetting playerSelectSetting;
    readonly IAppStrikerRegistry appStrikerRegistry;
    readonly IGamePadRegistry gamePadRegistry;
    readonly ResultSceneView resultSceneView;
    readonly CompositeDisposable resultInputSubscriptions = new();
    TaskCompletionSource<bool> resultEndCompletionSource;
    ResultPhase resultPhase;
    int lastPhaseAdvanceFrame;
    bool canAdvancePhase;

    public ResultScene(ResultSceneView resultSceneView, IPlayerSelectSetting playerSelectSetting, IAppStrikerRegistry appStrikerRegistry, IGamePadRegistry gamePadRegistry)
    {
        this.resultSceneView = resultSceneView;
        this.playerSelectSetting = playerSelectSetting;
        this.appStrikerRegistry = appStrikerRegistry;
        this.gamePadRegistry = gamePadRegistry;
        LoadStrikerPortraits();
        resultSceneView.InitializeInactiveState();
        resultPhase = ResultPhase.Hidden;
        lastPhaseAdvanceFrame = -1;
        canAdvancePhase = false;
    }

    public void GotoSelectScene()
    {
        SceneManager.LoadScene("SelectScene");
    }

    public void ShowResult(IReadOnlyDictionary<PlayerId, BeatPlayerBattleResult> battleResults)
    {
        LoadStrikerPortraits();
        ApplyBattleResults(battleResults);
        resultSceneView.ShowRoot();
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

    public void Dispose()
    {
        resultInputSubscriptions.Dispose();
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
        resultSceneView.ResultPanelButton.StartPhase1FromFlow();
        await resultSceneView.ResultPanelButton.WaitForPhase1CompletedAsync();
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
            LeanTween.delayedCall(0f, () =>
            {
                resultSceneView.ResultPanelButton.ContinueToPhase2FromFlow();
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

        resultSceneView.Player1ScoreText.text = player1Result.Score.ToString();
        resultSceneView.Player2ScoreText.text = player2Result.Score.ToString();
        resultSceneView.Player1ExcellentText.text = player1Result.Excellent.ToString();
        resultSceneView.Player2ExcellentText.text = player2Result.Excellent.ToString();
        resultSceneView.Player1GoodText.text = player1Result.Good.ToString();
        resultSceneView.Player2GoodText.text = player2Result.Good.ToString();
        resultSceneView.Player1MissText.text = player1Result.Miss.ToString();
        resultSceneView.Player2MissText.text = player2Result.Miss.ToString();
    }

    void LoadStrikerPortraits()
    {
        if (playerSelectSetting.TryGetStriker(0, out var player1Striker))
        {
            resultSceneView.Player1PortraitImage.sprite = appStrikerRegistry.GetByStriker(player1Striker).Portrait;
        }
        else
        {
            resultSceneView.Player1PortraitImage.sprite = appStrikerRegistry.Default.Portrait;
        }

        if (playerSelectSetting.TryGetStriker(1, out var player2Striker))
        {
            resultSceneView.Player2PortraitImage.sprite = appStrikerRegistry.GetByStriker(player2Striker).Portrait;
        }
        else
        {
            resultSceneView.Player2PortraitImage.sprite = appStrikerRegistry.Default.Portrait;
        }
    }
}
