using Alice;
using R3;
using System.Collections.Generic;
using UnityEngine;
using VContainer;

public class SelectScene : MonoBehaviour
{
    enum SceneInputState {
        Selecting,
        ReadyToStart,
        TransitioningToScreen,
        TransitioningToStageSelect,
    }

    [SerializeField] Characterselectbutton[] characterSelectButtons;
    [SerializeField] StartButtonAnimation startButtonAnimation;
    [SerializeField] CharacterSelectStatusView statusView;
    [SerializeField] Backbutton backbutton;
    public AudioClip clickSound; // クリック時の効果音

    ISceneTransitionService transitionService;
    IGamePadRegistry gamePadRegistry;
    IBattleSelectSetting battleSelectSetting;
    IPlayerSelectSetting playerSelectSetting;
    IAppStrikerRegistry appStrikerRegistry;
    readonly CompositeDisposable subscriptions = new();
    readonly CharacterSelectSelectionPolicy selectionPolicy = new();
    readonly List<CharacterSelectSlotState> slotStates = new();
    readonly Dictionary<Striker, GameObject> strikerModelMap = new();
    bool initialized;
    SceneInputState inputState = SceneInputState.Selecting;

    [Inject]
    public void Construct(
        ISceneTransitionService transitionService,
        IGamePadRegistry gamePadRegistry,
        IBattleSelectSetting battleSelectSetting,
        IPlayerSelectSetting playerSelectSetting,
        IAppStrikerRegistry appStrikerRegistry) {
        this.transitionService = transitionService;
        this.gamePadRegistry = gamePadRegistry;
        this.battleSelectSetting = battleSelectSetting;
        this.playerSelectSetting = playerSelectSetting;
        this.appStrikerRegistry = appStrikerRegistry;
    }

    void Start() {
        if (initialized) return;

        selectionPolicy.Reset(playerSelectSetting);
        BuildStrikerModelMap();

        for (var i = 0; i < characterSelectButtons.Length; i++) {
            characterSelectButtons[i].OnStrikerClicked
                .Subscribe(OnStrikerClicked)
                .AddTo(subscriptions);
        }

        backbutton.OnBackPressed
            .Subscribe(_ => RequestStageSelectTransition())
            .AddTo(subscriptions);

        playerSelectSetting.SelectedStrikers
            .Subscribe(_ => RefreshState())
            .AddTo(subscriptions);

        for (var playerId = 0; playerId < CharacterSelectSelectionPolicy.MAXPLAYERS; playerId++) {
            gamePadRegistry.Get(playerId)
                .HasGamePad
                .Subscribe(_ => RefreshState())
                .AddTo(subscriptions);

            gamePadRegistry.Get(playerId)
                .OnButtonDown
                .Subscribe(button => OnButtonDown(button))
                .AddTo(subscriptions);
        }

        RefreshState();
        _ = transitionService.RequestEndTransitionAsync(AppScene.CharacterSelect);
        initialized = true;
    }

    void OnStrikerClicked(StrikerClickRequest request) {
        if (IsTransitioning()) {
            return;
        }

        var targetSlot = selectionPolicy.ResolveSelectionTargetSlot(
            request.PlayerId,
            GetJoinedPlayerCount(),
            playerSelectSetting.TryGetStriker(0, out _));

        playerSelectSetting.SelectStriker(targetSlot, request.Striker);
        selectionPolicy.RecordSelection(targetSlot);

        RefreshState();
    }

    void OnButtonDown(GamePadButton button) {
        if (IsTransitioning()) {
            return;
        }

        if (button == GamePadButton.South) {
            UndoSelection();
            return;
        }

        if (button != GamePadButton.East) return;
        if (inputState != SceneInputState.ReadyToStart) return;
        if (!startButtonAnimation.IsStartInputReady) {
            return;
        }

        AudioSource.PlayClipAtPoint(clickSound, Camera.main.transform.position);
        inputState = SceneInputState.TransitioningToScreen;
        _ = transitionService.RequestStartTransition(ResolvePlayScene());
    }

    AppScene ResolvePlayScene() {
        return battleSelectSetting.SelectedStage.CurrentValue == Stage.Street
            ? AppScene.Street
            : AppScene.Live;
    }

    void RequestStageSelectTransition() {
        if (IsTransitioning()) {
            return;
        }

        inputState = SceneInputState.TransitioningToStageSelect;
        _ = transitionService.RequestStartTransition(AppScene.StageSelect);
    }

    void UndoSelection() {
        if (selectionPolicy.TryPopUndoSlot(playerSelectSetting, out var slot)) {
            playerSelectSetting.DeselectStriker(slot);
        }

        RefreshState();
    }

    void RefreshState() {
        RefreshReadyState();
        RefreshStatusView();
    }

    void RefreshReadyState() {
        var allSelected = CanStartBattle();
        startButtonAnimation.SetAllStrikersSelected(allSelected);

        if (!IsTransitioning()) {
            inputState = allSelected ? SceneInputState.ReadyToStart : SceneInputState.Selecting;
        }
    }

    bool IsTransitioning() {
        return inputState == SceneInputState.TransitioningToScreen
            || inputState == SceneInputState.TransitioningToStageSelect;
    }

    bool CanStartBattle() {
        var requiredSlots = selectionPolicy.GetRequiredSlotCount(GetJoinedPlayerCount());
        if (requiredSlots <= 0) return false;

        for (var playerId = 0; playerId < requiredSlots; playerId++) {
            if (!playerSelectSetting.TryGetStriker(playerId, out _)) {
                return false;
            }
        }

        return true;
    }

    int GetJoinedPlayerCount() {
        var joinedPlayers = 0;
        for (var playerId = 0; playerId < CharacterSelectSelectionPolicy.MAXPLAYERS; playerId++) {
            if (gamePadRegistry.Get(playerId).HasGamePad.CurrentValue) {
                joinedPlayers++;
            }
        }
        return joinedPlayers;
    }

    void BuildStrikerModelMap() {
        strikerModelMap.Clear();
        var strikers = appStrikerRegistry.GetAll();
        for (var i = 0; i < strikers.Count; i++) {
            var info = strikers[i];
            strikerModelMap[info.BattleStriker] = info.PreviewModel;
        }
    }

    void RefreshStatusView() {
        var requiredSlots = selectionPolicy.GetRequiredSlotCount(GetJoinedPlayerCount());
        slotStates.Clear();

        for (var i = 0; i < requiredSlots; i++) {
            var hasGamePad = gamePadRegistry.Get(i).HasGamePad.CurrentValue;
            GameObject selectedModelPrefab = null;

            if (playerSelectSetting.TryGetStriker(i, out var striker) && strikerModelMap.TryGetValue(striker, out var modelPrefab)) {
                selectedModelPrefab = modelPrefab;
            }

            slotStates.Add(new CharacterSelectSlotState(i, hasGamePad, selectedModelPrefab));
        }

        statusView.Render(slotStates);
    }

    void OnDestroy() {
        subscriptions.Dispose();
    }
}
