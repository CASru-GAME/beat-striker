
using R3;
using System;
using System.Collections.Generic;
using App;
using UnityEngine;

namespace Alice {
    public enum Striker {
        Hero,
        Wizard,
        Fighter,
        Warrior,
    }

    public interface IBattleDeployer {
        Awaitable DeployAsync();
        Awaitable RedeployForNextRoundAsync();
        void Undeploy();
        void BeginRoundEpisode(int roundNumber);
        void ConnectRoundInputs();
        void DisconnectRoundInputs();
        void PauseRound();
        void ResumeRound();
        void RecordRoundResult(int roundNumber, int deadPlayerId);
    }

    public class BattleDeployer : IBattleDeployer, IDisposable {
        class DeployedStriker {
            public int PlayerId;
            public Striker Striker;
            public Transform PlayerTransform;
            public Transform OriginalParent;
            public Vector3 OriginalPosition;
            public Quaternion OriginalRotation;
            public IStrikerHub Hub;
            public AiBrain RuntimeAiBrain;
            public string RuntimeAiBrainPrefabName;
            public IDisposable InpactSubscription;
            public IDisposable AttentionSubscription;
            public IDisposable SpecialRequestFailedSubscription;
            public LoadedAsset<GameObject> PrefabAsset;
        }

        class PendingDeployment {
            public int PlayerId;
            public Striker SelectedStriker;
            public Transform PlayerTransform;
            public StrikerInfo SelectedStrikerInfo;
            public LoadedAsset<GameObject> PrefabAsset;
        }

        readonly IBattleSetting battleSetting;
        readonly IBattleSelectSetting battleSelectSetting;
        readonly IBattleRuleSetting battleRuleSetting;
        readonly IPlayerSelectSetting playerSelectSetting;
        readonly IAppStrikerRegistry appStrikerRegistry;
        readonly IStrikerRegistry strikerRegistry;
        readonly IStrikerFactory strikerHubFactory;
        readonly IGamePadRegistry gamePadRegistry;
        readonly IAIRegistry aiRegistry;
        readonly IAISetting aiSetting;
        readonly ITutorialSetting tutorialSetting;
        readonly IMusicPlayer musicPlayer;
        readonly IBeatjudge beatJudge;
        readonly IBattlePresenter battlePresenter;
        readonly List<DeployedStriker> deployedStrikers = new();
        readonly List<IDisposable> roundSubscriptions = new();
        bool isRoundPaused;
        readonly Dictionary<int, float> opponentEmaScales = new();
        int lastSelectedOpponentIndex = -1;
        LearningCharacter lastSelectedOpponent;

        public BattleDeployer(IBattleSetting battleSetting, IBattleSelectSetting battleSelectSetting, IBattleRuleSetting battleRuleSetting, IPlayerSelectSetting playerSelectSetting, IAppStrikerRegistry appStrikerRegistry, IStrikerRegistry strikerRegistry, IStrikerFactory strikerHubFactory, IGamePadRegistry gamePadRegistry, IAIRegistry aiRegistry, IAISetting aiSetting, ITutorialSetting tutorialSetting, IMusicPlayer musicPlayer, IBeatjudge beatJudge, IBattlePresenter battlePresenter) {
            this.battleSetting = battleSetting;
            this.battleSelectSetting = battleSelectSetting;
            this.battleRuleSetting = battleRuleSetting;
            this.playerSelectSetting = playerSelectSetting;
            this.appStrikerRegistry = appStrikerRegistry;
            this.strikerRegistry = strikerRegistry;
            this.strikerHubFactory = strikerHubFactory;
            this.gamePadRegistry = gamePadRegistry;
            this.aiRegistry = aiRegistry;
            this.aiSetting = aiSetting;
            this.tutorialSetting = tutorialSetting;
            this.musicPlayer = musicPlayer;
            this.beatJudge = beatJudge;
            this.battlePresenter = battlePresenter;
        }

        public Awaitable DeployAsync() {
            return DeployCoreAsync(null);
        }

        public async Awaitable RedeployForNextRoundAsync() {
            var carriedSpecialPoints = new Dictionary<int, float>();
            foreach (var striker in strikerRegistry.GetAllStrikers()) {
                carriedSpecialPoints[striker.PlayerId.CurrentValue] = striker.SpecialPoint.CurrentValue;
            }

            await DeployCoreAsync(carriedSpecialPoints);
        }

        async Awaitable DeployCoreAsync(IReadOnlyDictionary<int, float> initialSpecialPointsByPlayerId) {
            isRoundPaused = false;

            if (aiSetting.UsesAiSettingStrikerSelection) {
                ApplyLearningSelections();
            }

            var pendingDeployments = new List<PendingDeployment>();
            for (int i = 0; i < battleSetting.PlayerTransforms.Count; i++) {
                var playerId = i;
                var playerTransform = battleSetting.PlayerTransforms[i];
                var selectedStrikerInfo = !playerSelectSetting.TryGetStriker(i, out var selectedStriker)
                    ? appStrikerRegistry.Default
                    : appStrikerRegistry.GetByStriker(selectedStriker);
                selectedStriker = selectedStrikerInfo.BattleStriker;
                var prefabAsset = await appStrikerRegistry.LoadBattlePrefabAsync(selectedStriker);
                pendingDeployments.Add(new PendingDeployment {
                    PlayerId = playerId,
                    SelectedStriker = selectedStriker,
                    PlayerTransform = playerTransform,
                    SelectedStrikerInfo = selectedStrikerInfo,
                    PrefabAsset = prefabAsset,
                });
            }

            if (deployedStrikers.Count > 0) {
                Undeploy();
            }

            for (int i = 0; i < pendingDeployments.Count; i++) {
                var deployment = pendingDeployments[i];
                var playerId = deployment.PlayerId;
                var playerTransform = deployment.PlayerTransform;
                var selectedStriker = deployment.SelectedStriker;
                var selectedStrikerInfo = deployment.SelectedStrikerInfo;
                var prefabAsset = deployment.PrefabAsset;
                var prefabObject = prefabAsset.Asset;
                var prefab = prefabObject != null ? prefabObject.GetComponent<StrikerHub>() : null;
                Debug.Log($"[BattleDeployer] Deploy loop: Player{playerId} TryGetStriker={selectedStriker}, BattleStriker={selectedStrikerInfo.BattleStriker}, Prefab={prefab?.name ?? "NULL"}".ToOrange());
                if (prefab == null) {
                    prefabAsset.Dispose();
                    Debug.LogError($"Striker prefab not found or missing StrikerHub for {selectedStriker}");
                    continue;
                }
                var originalParent = playerTransform.parent;
                var originalPosition = playerTransform.position;
                var originalRotation = playerTransform.rotation;
                var instance = strikerHubFactory.Create(prefab, playerTransform, playerId);
                var runtimeAiBrain = CreateRuntimeAiBrain(playerId, selectedStriker);
                var inpactSubscription = instance.OnInpactGenerated.Subscribe(command => battlePresenter.PlayInpact(command));
                var attentionSubscription = instance.OnAtentionRequested.Subscribe(request => battlePresenter.RequestAttention(playerId, request));
                var specialRequestFailedSubscription = instance.OnSpecialRequestFailed.Subscribe(_ => {
                    battleSetting.SpecialUnavailableSound.PlayAtApp(
                        instance.Position.CurrentValue,
                        battleSetting.SpecialUnavailableSoundVolume);
                });

                if (initialSpecialPointsByPlayerId != null
                    && initialSpecialPointsByPlayerId.TryGetValue(playerId, out var initialSp)
                    && initialSp > 0f) {
                    instance.AddSpecialPoint(initialSp);
                }

                strikerRegistry.RequestRegister(i, instance);

                deployedStrikers.Add(new DeployedStriker {
                    PlayerId = playerId,
                    Striker = selectedStriker,
                    PlayerTransform = playerTransform,
                    OriginalParent = originalParent,
                    OriginalPosition = originalPosition,
                    OriginalRotation = originalRotation,
                    Hub = instance,
                    RuntimeAiBrain = runtimeAiBrain,
                    RuntimeAiBrainPrefabName = runtimeAiBrain != null ? runtimeAiBrain.name.Replace($"_Player{playerId}", string.Empty) : string.Empty,
                    InpactSubscription = inpactSubscription,
                    AttentionSubscription = attentionSubscription,
                    SpecialRequestFailedSubscription = specialRequestFailedSubscription,
                    PrefabAsset = prefabAsset,
                });

                Debug.Log($"Deployed Striker {selectedStriker} for Player {playerId}".ToCyan());
            }
        }

        void ApplyLearningSelections() {
            var p1Striker = aiSetting.LearningPlayer1Striker;
            playerSelectSetting.SelectStriker(0, p1Striker);
            
            if (aiSetting.UsesSelfPlayOpponentSelection) {
                playerSelectSetting.SelectStriker(1, p1Striker);
                lastSelectedOpponentIndex = -1;
                lastSelectedOpponent = null;
                Debug.Log($"[BattleDeployer] ApplyLearningSelections: SelfPlay ON, Player2 also set to {p1Striker}".ToOrange());
            } else if (aiSetting.UsesLearningOpponentPool) {
                var result = aiSetting.GetWeightedRandomOpponent(GetOpponentEmaScale);
                if (result.HasValue) {
                    lastSelectedOpponentIndex = result.Value.Index;
                    lastSelectedOpponent = result.Value.Character;
                    playerSelectSetting.SelectStriker(1, result.Value.Character.Striker);
                    var emaScale = GetOpponentEmaScale(result.Value.Index);
                    Debug.Log($"[BattleDeployer] ApplyLearningSelections: SelfPlay OFF, Player2 set to {result.Value.Character.Striker} (index={result.Value.Index}, weight={result.Value.Character.Weight:F2}, emaScale={emaScale:F3})".ToOrange());
                } else {
                    lastSelectedOpponentIndex = -1;
                    lastSelectedOpponent = null;
                    playerSelectSetting.SelectStriker(1, p1Striker);
                    Debug.Log($"[BattleDeployer] ApplyLearningSelections: No valid opponent, fallback to {p1Striker}".ToOrange());
                }
            } else {
                lastSelectedOpponentIndex = -1;
                lastSelectedOpponent = null;
                playerSelectSetting.SelectStriker(1, p1Striker);
                Debug.Log($"[BattleDeployer] ApplyLearningSelections: Opponent pool is disabled in {aiSetting.Mode.CurrentValue}, Player2 fallback to {p1Striker}".ToOrange());
            }

            // Verify selections were applied correctly
            playerSelectSetting.TryGetStriker(0, out var verifyP1);
            playerSelectSetting.TryGetStriker(1, out var verifyP2);
            Debug.Log($"[BattleDeployer] ApplyLearningSelections VERIFY: Player0={verifyP1}, Player1={verifyP2}".ToOrange());
        }

        float GetOpponentEmaScale(int opponentIndex) {
            if (!opponentEmaScales.TryGetValue(opponentIndex, out var scale)) {
                return 1f; // 初回は最大スケール
            }
            return scale;
        }

        public void BeginRoundEpisode(int roundNumber) {
            if (deployedStrikers.Count == 0) {
                return;
            }

            var player1 = deployedStrikers.Find(x => x.PlayerId == 0);
            var player2 = deployedStrikers.Find(x => x.PlayerId == 1);
            var player1StrikerName = player1?.Striker.ToString() ?? "Unknown";
            var player2StrikerName = player2?.Striker.ToString() ?? "Unknown";

            foreach (var deployed in deployedStrikers) {
                if (deployed.RuntimeAiBrain == null) {
                    continue;
                }

                deployed.RuntimeAiBrain.BeginRoundEpisode(roundNumber, player1StrikerName, player2StrikerName, deployed.RuntimeAiBrainPrefabName);
            }
        }

        public void RecordRoundResult(int roundNumber, int deadPlayerId) {
            if (!IsInfiniteRoundMode()) {
                return;
            }

            var player1Win = deadPlayerId != 0;
            foreach (var deployed in deployedStrikers) {
                deployed.RuntimeAiBrain?.CompleteRoundEpisode(roundNumber, player1Win);
            }

            if (lastSelectedOpponentIndex < 0) {
                return;
            }

            var idx = lastSelectedOpponentIndex;
            var smoothing = aiSetting.EmaSmoothing;
            var floor = aiSetting.EmaFloorScale;
            float winOrLoss = deadPlayerId != 0 ? 0f : 1f; // 1P勝利=0（スケール低下）, 1P敗北=1（スケール維持）

            if (!opponentEmaScales.TryGetValue(idx, out var currentScale)) {
                currentScale = 1f;
            }

            // scale' = (1 - floor) * (smoothing * scale + (1 - smoothing) * winOrLoss) + floor
            var newScale = (1f - floor) * (smoothing * currentScale + (1f - smoothing) * winOrLoss) + floor;
            opponentEmaScales[idx] = newScale;

            Debug.Log($"[BattleDeployer] RecordRoundResult: opponentIndex={idx}, deadPlayerId={deadPlayerId}, win={winOrLoss:F0}, scale={currentScale:F3}->{newScale:F3} (smoothing={smoothing:F2}, floor={floor:F2})".ToOrange());
        }

        public void Undeploy() {
            DisconnectRoundInputs();
            isRoundPaused = false;

            foreach (var deployed in deployedStrikers) {
                strikerRegistry.RequestUnregister(deployed.PlayerId);

                if (deployed.PlayerTransform != null) {
                    deployed.PlayerTransform.SetParent(deployed.OriginalParent);
                    deployed.PlayerTransform.SetPositionAndRotation(deployed.OriginalPosition, deployed.OriginalRotation);
                }

                deployed.Hub?.ExitState();
                deployed.Hub?.Dispose(); // Hub自体もIDisposableなので呼んでおく
                deployed.Hub?.DestroyGameObject();
                if (deployed.RuntimeAiBrain != null) {
                    deployed.RuntimeAiBrain.EndRoundEpisode();
                    gamePadRegistry.RequestUnregister(deployed.RuntimeAiBrain);
                    deployed.RuntimeAiBrain.DisableAiMode();
                    UnityEngine.Object.Destroy(deployed.RuntimeAiBrain.gameObject);
                }
                deployed.InpactSubscription?.Dispose();
                deployed.AttentionSubscription?.Dispose();
                deployed.SpecialRequestFailedSubscription?.Dispose();
                deployed.PrefabAsset?.Dispose();
                deployed.PrefabAsset = null;
            }

            deployedStrikers.Clear();
        }

        public void ConnectRoundInputs() {
            DisconnectRoundInputs();
            isRoundPaused = false;

            foreach (var deployed in deployedStrikers) {
                var playerId = deployed.PlayerId;
                var instance = deployed.Hub;
                var aiBrain = deployed.RuntimeAiBrain;
                if (instance == null) {
                    continue;
                }

                var gamePad = gamePadRegistry.Get(playerId);
                if (aiBrain != null) {
                    roundSubscriptions.Add(aiSetting.Mode.Subscribe(_ => {
                        if (playerId != 0 && !aiSetting.UsesSelfPlayOpponentSelection) {
                            aiBrain.ApplyLearningMode(false);
                            return;
                        }

                        aiBrain.ApplyLearningMode(aiSetting.EnablesAgentLearning);
                    }));
                }

                roundSubscriptions.Add(gamePad.HasGamePad.Subscribe(hasGamePad => {
                    if (aiBrain == null) {
                        return;
                    }

                    if (!hasGamePad) {
                        aiBrain.EnableAiMode();
                        gamePadRegistry.RequestRegisterLowPriority(playerId, aiBrain);
                    } else {
                        gamePadRegistry.RequestUnregister(aiBrain);
                        if (ShouldKeepAiEnabledForDemonstration(playerId)) {
                            // Demonstration記録時はAgentがdecisionを回し続ける必要があるため、
                            // ゲームパッドが接続されていてもAiBrain自体は有効のまま維持する。
                            aiBrain.EnableAiMode();
                        } else {
                            aiBrain.DisableAiMode();
                        }
                    }
                }));

                if (aiBrain != null) {
                    roundSubscriptions.Add(musicPlayer.OnExcellentZoneEntered.Subscribe(signal => {
                        var opponent = ResolveNearestOpponent(instance);
                        aiBrain.RequestActionOnExcellentWindow(instance, opponent, signal, musicPlayer.CurrentPlaybackTime);
                    }));
                }

                roundSubscriptions.Add(gamePad.OnButtonDown.Subscribe(button => {
                    if (button != GamePadButton.Start) {
                        return;
                    }

                    RequestSpecial(instance);
                }));

                var beatPlayer = beatJudge.GetBeatPlayer(playerId);

                roundSubscriptions.Add(beatPlayer.OnBeatCommandExecuted.Subscribe(beatResult => {
                    if (!beatResult.IsSuccess) {
                        return;
                    }

                    if (playerId == 0 && aiBrain != null) {
                        var recordedDirection = beatResult.Direction.sqrMagnitude > 0.0001f
                            ? beatResult.Direction.normalized
                            : Vector2.zero;
                        aiBrain.RecordDemonstrationAction(new AiAction(recordedDirection, beatResult.Button));
                    }

                    if (beatResult.Direction.sqrMagnitude > 0.0001f) {
                        instance.ChangeDirection(beatResult.Direction);
                    } else {
                        instance.CancelDirection();
                    }

                    var specialPointGain = CalculateSpecialPointGain(beatResult.ComboCount);
                    instance.AddSpecialPoint(specialPointGain);

                    switch (beatResult.Button) {
                        case GamePadButton.East:
                            instance.Attack();
                            break;
                        case GamePadButton.South:
                            instance.Charge();
                            break;
                        case GamePadButton.West:
                            instance.Dash();
                            break;
                        case GamePadButton.Left:
                        case GamePadButton.Right:
                        case GamePadButton.North:
                            instance.Guard();
                            break;
                    }
                }));

                roundSubscriptions.Add(beatPlayer.OnBeatPassed.Subscribe(beatResult => {
                    if (beatResult.Direction.sqrMagnitude > 0.0001f) {
                        instance.ChangeDirection(beatResult.Direction);
                        return;
                    }

                    instance.CancelDirection();
                }));

                roundSubscriptions.Add(Disposable.Create(() => {
                    if (aiBrain != null) {
                        gamePadRegistry.RequestUnregister(aiBrain);
                        aiBrain.DisableAiMode();
                    }
                    if (instance != null) {
                        instance.CancelDirection();
                    }
                }));
            }
        }

        public void DisconnectRoundInputs() {
            foreach (var subscription in roundSubscriptions) {
                subscription.Dispose();
            }
            roundSubscriptions.Clear();
        }

        public void PauseRound() {
            if (isRoundPaused) {
                return;
            }

            DisconnectRoundInputs();
            isRoundPaused = true;
        }

        public void ResumeRound() {
            if (!isRoundPaused) {
                return;
            }

            ConnectRoundInputs();
        }

        public void Dispose() {
            Undeploy();
        }

        AiBrain CreateRuntimeAiBrain(int playerId, Striker selfStriker) {
            if (playerId != 0 && aiSetting.UsesFixedTestOpponentSequence) {
                return CreateTestModeOpponentBrain(playerId);
            }

            if (IsInfiniteRoundMode() && playerId != 0 && !aiSetting.UsesSelfPlayOpponentSelection && lastSelectedOpponent?.BrainPrefab != null) {
                var specifiedBrain = lastSelectedOpponent.BrainPrefab;
                var specifiedBrainName = $"{specifiedBrain.name}_Player{playerId}";
                var specifiedAiBrain = UnityEngine.Object.Instantiate(specifiedBrain, battleSetting.PlayerTransforms[playerId]);
                specifiedAiBrain.name = specifiedBrainName;
                specifiedAiBrain.ApplyLearningMode(false);
                specifiedAiBrain.DisableAiMode();
                return specifiedAiBrain;
            }

            var opponentStriker = ResolveOpponentStriker(playerId, selfStriker);
            var maxAiStrength = ResolveMaxAiStrength();
            if (!aiRegistry.TryResolve(selfStriker, opponentStriker, maxAiStrength, out var aiRegistration)) {
                Debug.LogWarning($"Failed to resolve AI brain registration. playerId={playerId}, self={selfStriker}, opponent={opponentStriker}, maxStrength={maxAiStrength}. Configure fallbackAiId in AIRegistry inspector or add matching entry.");
                return null;
            }
            var brainPrefab = aiRegistration.BrainPrefab;
            var brainName = $"{aiRegistration.Id}_Player{playerId}";

            var aiBrain = UnityEngine.Object.Instantiate(brainPrefab, battleSetting.PlayerTransforms[playerId]);
            aiBrain.name = brainName;
            
            bool initialShouldLearn = aiSetting.EnablesAgentLearning;
            if (playerId != 0 && !aiSetting.UsesSelfPlayOpponentSelection) {
                initialShouldLearn = false;
            }
            aiBrain.ApplyLearningMode(initialShouldLearn);
            aiBrain.ConfigureDemonstrationRecording(aiSetting.IsDemonstrationRecordingMode, aiSetting.DemonstrationName, playerId);
            
            aiBrain.DisableAiMode();
            return aiBrain;
        }

        int ResolveMaxAiStrength() {
            return tutorialSetting.IsTutorialBattleRequested
                ? tutorialSetting.AiStrength
                : battleSelectSetting.AiStrength;
        }

        AiBrain CreateTestModeOpponentBrain(int playerId) {
            var holder = new GameObject($"TestBeatAi_Player{playerId}");
            holder.transform.SetParent(battleSetting.PlayerTransforms[playerId], false);
            var beatAiBrain = holder.AddComponent<BeatAiBrain>();
            beatAiBrain.name = holder.name;
            beatAiBrain.SetActionSequence(aiSetting.TestOpponentSequence);
            beatAiBrain.ApplyLearningMode(false);
            beatAiBrain.ConfigureDemonstrationRecording(false, aiSetting.DemonstrationName, playerId);
            beatAiBrain.DisableAiMode();
            return beatAiBrain;
        }

        bool IsInfiniteRoundMode() {
            return aiSetting.IsInfiniteRoundMode;
        }

        bool ShouldKeepAiEnabledForDemonstration(int playerId) {
            return playerId == 0 && aiSetting.IsDemonstrationRecordingMode;
        }

        IObservableStriker ResolveNearestOpponent(IObservableStriker self) {
            IObservableStriker nearestOpponent = null;
            var nearestSqrDistance = float.MaxValue;

            foreach (var striker in strikerRegistry.GetAllStrikers()) {
                if (striker.PlayerId.CurrentValue == self.PlayerId.CurrentValue || striker.HitPoint.CurrentValue <= 0f) {
                    continue;
                }

                var sqrDistance = (striker.Position.CurrentValue - self.Position.CurrentValue).sqrMagnitude;
                if (sqrDistance >= nearestSqrDistance) {
                    continue;
                }

                nearestOpponent = striker;
                nearestSqrDistance = sqrDistance;
            }

            return nearestOpponent;
        }

        Striker ResolveOpponentStriker(int playerId, Striker fallback) {
            for (var i = 0; i < battleSetting.PlayerTransforms.Count; i++) {
                if (i == playerId) {
                    continue;
                }

                if (!playerSelectSetting.TryGetStriker(i, out var opponent)) {
                    opponent = appStrikerRegistry.Default.BattleStriker;
                }

                return opponent;
            }

            return fallback;
        }

        void RequestSpecial(IStrikerHub instance) {
            if (battleSetting.IsTestMode) {
                instance.AddSpecialPoint(float.MaxValue);
            }

            instance.Special();
        }

        float CalculateSpecialPointGain(int comboCount) {
            var combo = Mathf.Max(1, comboCount);
            var combo1Gain = Mathf.Max(0f, battleRuleSetting.Combo1SpecialPointGain.CurrentValue);
            var convergenceRate = Mathf.Max(0f, battleRuleSetting.SpecialPointGainConvergenceRate.CurrentValue);
            var convergenceValue = Mathf.Max(combo1Gain, battleRuleSetting.SpecialPointGainConvergenceValue.CurrentValue);
            var x = combo - 1;
            return convergenceValue - (convergenceValue - combo1Gain) * Mathf.Exp(-convergenceRate * x);
        }

    }


}