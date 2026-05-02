
using R3;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using VContainer;
using CorePlayerId = App.PlayerId;

namespace Alice {
    public class BattleFlow {
        const string LOG_PREFIX = "[BattleFlow]";
        readonly IBattleSetting battleSetting;
        readonly IBattleDeployer battleDeployer;
        readonly IStrikerRegistry strikerRegistry;
        readonly IAppStrikerRegistry appStrikerRegistry;
        readonly IBattleJudge battleJudge;
        readonly IBeatjudge beatJudge;
        readonly IMusicPlayer musicPlayer;
        readonly IMusicRegistry musicRegistry;
        readonly IAISetting aiSetting;
        readonly IAppNetworkSetting appNetworkSetting;
        readonly IBattleSelectSetting battleSelectSetting;
        readonly ITutorialSetting tutorialSetting;
        readonly ISceneTransitionService sceneTransitionService;
        readonly ILoadingOverlayService loadingOverlayService;
        readonly IBattlePresenter battlePresenter;
        readonly IBattleTutorialSignalEmitter tutorialSignalEmitter;
        readonly IBattleOnlineSync battleOnlineSync;
        readonly ResultScene resultScene;
        readonly IBattlePlayerPresenter[] battlePlayerPresenters;
        readonly BattleFlowStateMachine stateMachine = new();
        readonly BattleFlowPauseHandler pauseHandler;
        readonly BattleFlowMusicEndHandler musicEndHandler;
        readonly BattleFlowOnlineHandler onlineHandler;
        readonly BattleFlowRoundHandler roundHandler;
        bool battlePrepared;
        bool outroHandled;
        bool battleMusicStarted;
        readonly List<IDisposable> flowEventDisposables = new();
        BattleAddressablePreload battleAddressablePreload;

        readonly Subject<int> roundStartedSubject = new();
        readonly Subject<Unit> roundPlayableStartedSubject = new();
        readonly Subject<Unit> roundFinishedSubject = new();
        readonly Subject<Unit> battleFinishedSubject = new();
        readonly Subject<CorePlayerId> outroStartedSubject = new();
        public Observable<Unit> OnRoundPlayableStarted => roundPlayableStartedSubject;

        [Inject]
        public BattleFlow(IBattleSetting battleSetting, IBattleDeployer battleDeployer, IStrikerRegistry strikerRegistry, IAppStrikerRegistry appStrikerRegistry, IBattleJudge battleJudge, IBeatjudge beatJudge, IMusicPlayer musicPlayer, IMusicRegistry musicRegistry, IAISetting aiSetting, IAppNetworkSetting appNetworkSetting, IBattleSelectSetting battleSelectSetting, ITutorialSetting tutorialSetting, ISceneTransitionService sceneTransitionService, ILoadingOverlayService loadingOverlayService, IBattlePresenter battlePresenter, IBattleTutorialSignalEmitter tutorialSignalEmitter, IBattleOnlineSync battleOnlineSync, ResultScene resultScene, IBattlePlayerPresenter[] battlePlayerPresenters) {
            this.battleSetting = battleSetting;
            this.battleDeployer = battleDeployer;
            this.strikerRegistry = strikerRegistry;
            this.appStrikerRegistry = appStrikerRegistry;
            this.battleJudge = battleJudge;
            this.beatJudge = beatJudge;
            this.musicPlayer = musicPlayer;
            this.musicRegistry = musicRegistry;
            this.aiSetting = aiSetting;
            this.appNetworkSetting = appNetworkSetting;
            this.battleSelectSetting = battleSelectSetting;
            this.tutorialSetting = tutorialSetting;
            this.sceneTransitionService = sceneTransitionService;
            this.loadingOverlayService = loadingOverlayService;
            this.battlePresenter = battlePresenter;
            this.tutorialSignalEmitter = tutorialSignalEmitter;
            this.battleOnlineSync = battleOnlineSync;
            this.resultScene = resultScene;
            this.battlePlayerPresenters = battlePlayerPresenters;
            battlePrepared = false;
            outroHandled = false;
            battleMusicStarted = false;
            stateMachine.OnStateChanged.Subscribe(OnBattleFlowStateChanged);
            pauseHandler = new BattleFlowPauseHandler(stateMachine, beatJudge, musicPlayer, battleDeployer, battlePresenter, battlePlayerPresenters, CompleteBattleByPendingMusicEndIfNeeded);
            musicEndHandler = new BattleFlowMusicEndHandler(stateMachine, aiSetting, battleJudge, beatJudge, battleOnlineSync, pauseHandler, BeginRoundResolution, () => roundHandler.CurrentRound, CompleteBattleWithWinnerAsync, () => roundHandler.ResolveTopHitPointPlayerId(), PublishRoundOutcome, () => onlineHandler.IsOnlineClient);
            onlineHandler = new BattleFlowOnlineHandler(stateMachine, appNetworkSetting, battleOnlineSync, battleJudge, beatJudge, pauseHandler, BeginRoundResolution, CompleteBattleWithWinnerAsync, () => roundHandler.CurrentRound, ApplySuspendMenuPause, ApplySuspendMenuResume, EndBattleToTitleAsync, deadPlayerId => roundHandler.ResolveRoundAsync(deadPlayerId), deadPlayerId => roundHandler.OnStrikerDead(deadPlayerId));
            var roundSceneSpawnTracker = new RoundSceneSpawnTracker();
            roundHandler = new BattleFlowRoundHandler(stateMachine, strikerRegistry, battleJudge, beatJudge, battleDeployer, battlePresenter, battlePlayerPresenters, aiSetting, tutorialSetting, musicPlayer, pauseHandler, musicEndHandler, onlineHandler, roundSceneSpawnTracker, () => battleAddressablePreload, () => battleMusicStarted, value => battleMusicStarted = value, BeginRoundResolution, EndBattleToTitleAsync, CompleteBattleWithWinnerAsync, roundWins => musicEndHandler.ResolveMusicEndWinner(roundWins), () => musicEndHandler.ShouldCompleteBattleByMusicEnd, roundStartedSubject.OnNext, () => roundPlayableStartedSubject.OnNext(Unit.Default), () => roundFinishedSubject.OnNext(Unit.Default));
            Debug.Log($"{LOG_PREFIX} Constructed. initialRound={roundHandler.CurrentRound}, playerPresenterCount={battlePlayerPresenters.Length}");
        }

        async Task PrepareBattleAsync() {
            if (battlePrepared) {
                Debug.Log($"{LOG_PREFIX} PrepareBattle skipped because already prepared");
                return;
            }

            Debug.Log($"{LOG_PREFIX} PrepareBattle start");
            battlePrepared = true;
            battleAddressablePreload = await LoadBattleAddressablesAsync();
            await battleDeployer.DeployAsync(battleAddressablePreload);
            Debug.Log($"{LOG_PREFIX} PrepareBattle completed and deploy requested");
        }

        async Task<BattleAddressablePreload> LoadBattleAddressablesAsync() {
            Debug.Log($"{LOG_PREFIX} LoadBattleAddressablesAsync start");
            using var scope = loadingOverlayService.Begin();
            var preload = new BattleAddressablePreload();
            try {
                var strikers = appStrikerRegistry.GetAll();
                for (var i = 0; i < strikers.Count; i++) {
                    var striker = strikers[i].BattleStriker;
                    var prefabAsset = await appStrikerRegistry.LoadBattlePrefabAsync(striker);
                    preload.AddBattlePrefab(striker, prefabAsset);
                }

                var selectedMusic = musicRegistry.GetById(battleSelectSetting.SelectedMusicId.CurrentValue);
                var clipAsset = await musicRegistry.LoadAudioClipAsync(selectedMusic.Id);
                var beatDataAsset = await musicRegistry.LoadBeatDataAsync(selectedMusic.Id);
                preload.SetMusic(selectedMusic.Id, clipAsset, beatDataAsset);
                Debug.Log($"{LOG_PREFIX} LoadBattleAddressablesAsync completed. strikerCount={strikers.Count}, musicId={selectedMusic.Id}");
                return preload;
            }
            catch {
                preload.Dispose();
                throw;
            }
        }

        public void StartBattle() {
            Debug.Log($"{LOG_PREFIX} StartBattle called. state={stateMachine.Current}, prepared={battlePrepared}");
            if (!stateMachine.TryStartBattle(nameof(StartBattle))) {
                Debug.Log($"{LOG_PREFIX} StartBattle skipped because state does not allow start. state={stateMachine.Current}");
                return;
            }

            Debug.Log($"{LOG_PREFIX} StartBattle launching async sequence");
            _ = StartBattleSequenceAsync();
        }

        async Task StartBattleSequenceAsync() {
            try {
                var scene = ResolveCurrentBattleScene();
                Debug.Log($"{LOG_PREFIX} StartBattleSequenceAsync begin. targetScene={scene}");
                await PrepareBattleAsync();
                Debug.Log($"{LOG_PREFIX} StartBattle reset battle state");
                beatJudge.ResetBattleState();
                battlePresenter.HandleBattleStarted();
                battleMusicStarted = false;
                musicEndHandler.Reset();
                Debug.Log($"{LOG_PREFIX} StartBattle subscribe striker dead events");
                roundHandler.SubscribeStrikerDeadEvents();
                Debug.Log($"{LOG_PREFIX} StartBattle subscribe flow events");
                SubscribeFlowEvents();
                var endResult = await sceneTransitionService.RequestEndTransitionAsync(ResolveCurrentBattleScene());
                Debug.Log($"{LOG_PREFIX} StartBattleSequenceAsync transition end completed. isSuccess={endResult.IsSuccess}");
                await battlePresenter.PlayBattleOpeningAsync();
                Debug.Log($"{LOG_PREFIX} StartBattleSequenceAsync battle opening completed");
                await System.Threading.Tasks.Task.WhenAll(battlePlayerPresenters.Select(battlePlayerPresenter => battlePlayerPresenter.PlayOpeningHpFillAsync()));
                Debug.Log($"{LOG_PREFIX} StartBattleSequenceAsync battle player HP opening animation completed");

                await roundHandler.StartRoundPlayableAsync();
                Debug.Log($"{LOG_PREFIX} StartBattleSequenceAsync completed first round playable start");
            }
            catch (Exception exception) {
                DisposeBattleAddressablePreload();
                Debug.LogError($"{LOG_PREFIX} StartBattleSequenceAsync failed: {exception.Message}");
                Debug.LogException(exception);
            }
        }

        AppScene ResolveCurrentBattleScene() {
            return battleSelectSetting.SelectedStage.CurrentValue == Stage.Street
                ? AppScene.Street
                : AppScene.Live;
        }

        void OnBattleFlowStateChanged(BattleFlowState state) {
            onlineHandler.PublishPhase(state);
        }

        Task StartRoundPlayableAsync() {
            return roundHandler.StartRoundPlayableAsync();
        }

        void CompleteBattle() {
            if (outroHandled) return;

            outroHandled = true;
            stateMachine.TryMarkFinished(nameof(CompleteBattle));
            battlePresenter.CloseSuspendMenu();
            musicPlayer.Stop();
            battleMusicStarted = false;
            battleDeployer.DisconnectRoundInputs();
            roundHandler.DisposeDeadEventSubscriptions();
            DisposeFlowEventSubscriptions();
            battleDeployer.Undeploy();
            DisposeBattleAddressablePreload();
        }

        void DisposeBattleAddressablePreload() {
            battleAddressablePreload?.Dispose();
            battleAddressablePreload = null;
        }

        void OnStrikerDead(int deadPlayerId) {
            roundHandler.OnStrikerDead(deadPlayerId);
        }

        Task ResolveRoundAsync(int deadPlayerId) {
            return roundHandler.ResolveRoundAsync(deadPlayerId);
        }

        void SubscribeFlowEvents() {
            DisposeFlowEventSubscriptions();
            flowEventDisposables.Add(battlePresenter.OnPauseMenuRequested.Subscribe(_ => OnPauseMenuRequested()));
            flowEventDisposables.Add(battlePresenter.OnSuspendRequested.Subscribe(_ => OnSuspendRequested()));
            flowEventDisposables.Add(battlePresenter.OnResumeRequested.Subscribe(_ => OnResumeRequested()));
            flowEventDisposables.Add(battlePresenter.OnAttentionActiveStateChanged.Subscribe(isActive => OnAttentionActiveStateChanged(isActive)));
            flowEventDisposables.Add(tutorialSignalEmitter.OnTutorialPauseRequested.Subscribe(_ => PauseRoundForTutorial()));
            flowEventDisposables.Add(tutorialSignalEmitter.OnTutorialResumeRequested.Subscribe(_ => ResumeRoundFromTutorial()));
            flowEventDisposables.Add(musicPlayer.OnPlaybackCompleted.Subscribe(_ => musicEndHandler.OnMusicPlaybackCompleted()));
            flowEventDisposables.Add(tutorialSignalEmitter.OnTutorialEndBattleToTitleRequested.Subscribe(_event => {
                _ = EndBattleToTitleAsync();
            }));

            if (onlineHandler.IsOnlineBattle) {
                flowEventDisposables.Add(battleOnlineSync.OnPhaseReceived.Subscribe(snapshot => onlineHandler.ApplyOnlinePhaseSnapshot(snapshot)));
                flowEventDisposables.Add(battleOnlineSync.OnOutcomeReceived.Subscribe(outcome => onlineHandler.ApplyOnlineOutcomeSnapshot(outcome)));
                flowEventDisposables.Add(battleOnlineSync.OnPauseRequested.Subscribe(_ => onlineHandler.ApplyOnlinePauseRequest()));
                flowEventDisposables.Add(battleOnlineSync.OnResumeRequested.Subscribe(_ => onlineHandler.ApplyOnlineResumeRequest()));
                flowEventDisposables.Add(battleOnlineSync.OnSuspendFinishRequested.Subscribe(_ => onlineHandler.ApplyOnlineSuspendFinishRequest(musicEndHandler.CompleteBattleBySuspendMenuAsync)));
                flowEventDisposables.Add(battleOnlineSync.OnRoundResolutionRequested.Subscribe(deadPlayerId => onlineHandler.ApplyOnlineRoundResolutionRequest(deadPlayerId)));
                flowEventDisposables.Add(battleOnlineSync.OnDisconnected.Subscribe(unit => {
                    _ = EndBattleToTitleAsync();
                }));
            }
        }

        void DisposeFlowEventSubscriptions() {
            foreach (var subscription in flowEventDisposables) {
                subscription.Dispose();
            }
            flowEventDisposables.Clear();
        }

        void OnPauseMenuRequested() {
            if (onlineHandler.IsOnlineClient) {
                onlineHandler.RequestPause();
                return;
            }

            if (onlineHandler.IsOnlineHost) {
                ApplySuspendMenuPause();
                return;
            }

            ApplySuspendMenuPause();
        }

        void OnSuspendRequested() {
            if (onlineHandler.IsOnlineClient) {
                onlineHandler.RequestSuspendFinish();
                return;
            }

            if (onlineHandler.IsOnlineHost) {
                onlineHandler.ApplyOnlineSuspendFinishRequest(musicEndHandler.CompleteBattleBySuspendMenuAsync);
                return;
            }

            if (!stateMachine.CanSuspendBattle) {
                return;
            }

            if (tutorialSetting.IsTutorialBattleRequested) {
                tutorialSetting.ClearTutorialBattleRequest();
                _ = EndBattleToTitleAsync();
                return;
            }

            _ = musicEndHandler.CompleteBattleBySuspendMenuAsync();
        }

        bool ShouldCompleteBattleByMusicEnd() {
            return musicEndHandler.ShouldCompleteBattleByMusicEnd;
        }

        void CompleteBattleByPendingMusicEndIfNeeded() {
            musicEndHandler.CompleteBattleByPendingMusicEndIfNeeded();
        }

        CorePlayerId ResolveMusicEndWinner(IReadOnlyDictionary<CorePlayerId, int> roundWins) {
            return musicEndHandler.ResolveMusicEndWinner(roundWins);
        }

        void PublishRoundOutcome(int finishedRound, int deadPlayerId, int roundWinnerPlayerId, bool continueBattle, int finalWinnerPlayerId, bool stopMusic, IReadOnlyDictionary<CorePlayerId, int> roundWins) {
            onlineHandler.PublishRoundOutcome(finishedRound, deadPlayerId, roundWinnerPlayerId, continueBattle, finalWinnerPlayerId, stopMusic, roundWins);
        }

        async Task CompleteBattleWithWinnerAsync(CorePlayerId winner, IReadOnlyDictionary<CorePlayerId, int> roundWins, bool stopMusic = false) {
            Debug.Log($"{LOG_PREFIX} CompleteBattleWithWinnerAsync begin. winner={winner}");
            if (!stateMachine.TryBeginEndingBattle(nameof(CompleteBattleWithWinnerAsync))) {
                return;
            }

            battleFinishedSubject.OnNext(Unit.Default);
            outroStartedSubject.OnNext(winner);
            await battlePresenter.PlayBattleEndingAsync(winner);
            var battleResults = beatJudge.GetBattleResults();
            resultScene.ShowResult(battleResults, roundWins);
            await resultScene.WaitForBattleEndInputAsync();
            await battlePresenter.PlayBattleFinishFadeInAsync();
            CompleteBattle();
            Debug.Log($"{LOG_PREFIX} CompleteBattleWithWinnerAsync completed. requesting start transition to ResultMenu");
            var startResult = sceneTransitionService.RequestStartTransition(AppScene.ResultMenu);
            Debug.Log($"{LOG_PREFIX} CompleteBattleWithWinnerAsync start transition result. nextScene={AppScene.ResultMenu}, isSuccess={startResult.IsSuccess}");
        }

        bool BeginRoundResolution(bool stopMusic) {
            if (!stateMachine.TryBeginResolvingRound(nameof(BeginRoundResolution))) {
                return false;
            }

            roundHandler.IncrementActiveRoundToken();
            pauseHandler.PauseRoundRuntimeSystems(controlsMusic: false);
            battlePresenter.CloseSuspendMenu();
            if (stopMusic) {
                musicPlayer.Stop();
                battleMusicStarted = false;
            }
            battleDeployer.DisconnectRoundInputs();
            return true;
        }

        int ResolveTopHitPointPlayerId() {
            return roundHandler.ResolveTopHitPointPlayerId();
        }

        void OnResumeRequested() {
            if (onlineHandler.IsOnlineClient) {
                onlineHandler.RequestResume();
                return;
            }

            if (onlineHandler.IsOnlineHost) {
                ApplySuspendMenuResume();
                return;
            }

            ApplySuspendMenuResume();
        }

        void ApplySuspendMenuPause() {
            pauseHandler.ApplySuspendMenuPause();
        }

        void ApplySuspendMenuResume() {
            pauseHandler.ApplySuspendMenuResume();
        }

        void OnAttentionActiveStateChanged(bool isActive) {
            pauseHandler.HandleAttentionActiveStateChanged(isActive);
        }

        public void PauseRoundForTutorial() {
            pauseHandler.PauseRoundForTutorial();
        }

        public void ResumeRoundFromTutorial() {
            pauseHandler.ResumeRoundFromTutorial();
        }

        public async Task EndBattleToTitleAsync() {
            if (!stateMachine.CanBeginEndingToTitle) {
                return;
            }

            if (!stateMachine.TryBeginEndingToTitle(nameof(EndBattleToTitleAsync))) {
                return;
            }

            roundHandler.IncrementActiveRoundToken();
            pauseHandler.PauseRoundRuntimeSystems(controlsMusic: false);
            battlePresenter.CloseSuspendMenu();
            musicPlayer.Stop();
            battleMusicStarted = false;
            battleDeployer.DisconnectRoundInputs();
            beatJudge.ResetRoundState();
            pauseHandler.PresentRoundPlayableFinishToPlayers();
            battleFinishedSubject.OnNext(Unit.Default);

            await battlePresenter.PlayBattleFinishFadeInAsync();
            CompleteBattle();

            var startResult = sceneTransitionService.RequestStartTransition(AppScene.Title);
            Debug.Log($"{LOG_PREFIX} EndBattleToTitleAsync start transition result. nextScene={AppScene.Title}, isSuccess={startResult.IsSuccess}");
        }

    }
}