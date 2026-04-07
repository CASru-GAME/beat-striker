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

    public void ShowResult(IReadOnlyDictionary<PlayerId, BeatPlayerBattleResult> battleResults, IReadOnlyDictionary<PlayerId, int> roundWins)
    {
        LoadStrikerPortraits();
        ApplyBattleResults(battleResults);
        ApplyRoundWinColors(roundWins);
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
        var player1ScoreText = player1Result.Score.ToString();
        var player2ScoreText = player2Result.Score.ToString();
        var player1ComboText = player1Result.MaxCombo.ToString();
        var player2ComboText = player2Result.MaxCombo.ToString();

        resultSceneView.Player1ScoreText.text = player1ScoreText;
        resultSceneView.Player2ScoreText.text = player2ScoreText;
        resultSceneView.Player1ScoreSubText.text = player1ScoreText;
        resultSceneView.Player2ScoreSubText.text = player2ScoreText;
        resultSceneView.Player1ComboText.text = player1ComboText;
        resultSceneView.Player2ComboText.text = player2ComboText;
        resultSceneView.Player1ComboSubText.text = player1ComboText;
        resultSceneView.Player2ComboSubText.text = player2ComboText;
        resultSceneView.Player1ExcellentText.text = player1Result.Excellent.ToString();
        resultSceneView.Player2ExcellentText.text = player2Result.Excellent.ToString();
        resultSceneView.Player1GoodText.text = player1Result.Good.ToString();
        resultSceneView.Player2GoodText.text = player2Result.Good.ToString();
        resultSceneView.Player1MissText.text = player1Result.Miss.ToString();
        resultSceneView.Player2MissText.text = player2Result.Miss.ToString();
    }

    void ApplyRoundWinColors(IReadOnlyDictionary<PlayerId, int> roundWins)
    {
        var player1RoundWins = roundWins.TryGetValue(new PlayerId(0), out var player1Wins) ? player1Wins : 0;
        var player2RoundWins = roundWins.TryGetValue(new PlayerId(1), out var player2Wins) ? player2Wins : 0;

        ApplyPlayerRoundWinColors(resultSceneView.Player1RoundWinImages, player1RoundWins);
        ApplyPlayerRoundWinColors(resultSceneView.Player2RoundWinImages, player2RoundWins);
    }

    void ApplyPlayerRoundWinColors(IReadOnlyList<UnityEngine.UI.Image> roundWinImages, int roundWins)
    {
        var activeWins = Mathf.Clamp(roundWins, 0, roundWinImages.Count);
        for (var i = 0; i < roundWinImages.Count; i++)
        {
            roundWinImages[i].color = i < activeWins
                ? resultSceneView.RoundWinColor
                : resultSceneView.RoundNeutralColor;
        }
    }

    void LoadStrikerPortraits()
    {
        Sprite player1Portrait;
        if (playerSelectSetting.TryGetStriker(0, out var player1Striker))
        {
            player1Portrait = appStrikerRegistry.GetByStriker(player1Striker).Portrait;
        }
        else
        {
            player1Portrait = appStrikerRegistry.Default.Portrait;
        }
        resultSceneView.Player1PortraitImage.sprite = player1Portrait;

        Sprite player2Portrait;
        if (playerSelectSetting.TryGetStriker(1, out var player2Striker))
        {
            player2Portrait = appStrikerRegistry.GetByStriker(player2Striker).Portrait;
        }
        else
        {
            player2Portrait = appStrikerRegistry.Default.Portrait;
        }
        resultSceneView.Player2PortraitImage.sprite = player2Portrait;
    }
}
