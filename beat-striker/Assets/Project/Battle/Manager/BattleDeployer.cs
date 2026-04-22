
using R3;
using System;
using System.Collections.Generic;
using App;
using Alice;
using UnityEngine;
using UnityEngine.InputSystem.LowLevel;

namespace Alice {
    public enum Striker {
        Hero,
        Wizard,
        Fighter,
        Warrior,
    }

    public interface IBattleDeployer {
        void Deploy();
        void Undeploy();
        void ConnectRoundInputs();
        void DisconnectRoundInputs();
        void PauseRound();
        void ResumeRound();
    }

    public class BattleDeployer : IBattleDeployer, IDisposable {
        class DeployedStriker {
            public int PlayerId;
            public Transform PlayerTransform;
            public Transform OriginalParent;
            public Vector3 OriginalPosition;
            public Quaternion OriginalRotation;
            public IStrikerHub Hub;
            public AiBrain RuntimeAiBrain;
            public IDisposable InpactSubscription;
            public IDisposable AttentionSubscription;
            public IDisposable SpecialRequestFailedSubscription;
        }

        readonly IBattleSetting battleSetting;
        readonly IBattleRuleSetting battleRuleSetting;
        readonly IPlayerSelectSetting playerSelectSetting;
        readonly IAppStrikerRegistry appStrikerRegistry;
        readonly IStrikerRegistry strikerRegistry;
        readonly IStrikerFactory strikerHubFactory;
        readonly IGamePadRegistry gamePadRegistry;
        readonly IAIRegistry aiRegistry;
        readonly IAISetting aiSetting;
        readonly IMusicPlayer musicPlayer;
        readonly IBeatjudge beatJudge;
        readonly IBattlePresenter battlePresenter;
        readonly List<DeployedStriker> deployedStrikers = new();
        readonly List<IDisposable> roundSubscriptions = new();
        bool isRoundPaused;
        int learningRoundIndex;

        public BattleDeployer(IBattleSetting battleSetting, IBattleRuleSetting battleRuleSetting, IPlayerSelectSetting playerSelectSetting, IAppStrikerRegistry appStrikerRegistry, IStrikerRegistry strikerRegistry, IStrikerFactory strikerHubFactory, IGamePadRegistry gamePadRegistry, IAIRegistry aiRegistry, IAISetting aiSetting, IMusicPlayer musicPlayer, IBeatjudge beatJudge, IBattlePresenter battlePresenter) {
            this.battleSetting = battleSetting;
            this.battleRuleSetting = battleRuleSetting;
            this.playerSelectSetting = playerSelectSetting;
            this.appStrikerRegistry = appStrikerRegistry;
            this.strikerRegistry = strikerRegistry;
            this.strikerHubFactory = strikerHubFactory;
            this.gamePadRegistry = gamePadRegistry;
            this.aiRegistry = aiRegistry;
            this.aiSetting = aiSetting;
            this.musicPlayer = musicPlayer;
            this.beatJudge = beatJudge;
            this.battlePresenter = battlePresenter;
        }

        public void Deploy() {
            if (deployedStrikers.Count > 0) {
                Undeploy();
            }

            isRoundPaused = false;

            if (aiSetting.IsLearning.CurrentValue) {
                ApplyLearningSelections();
                learningRoundIndex += 1;
            } else {
                learningRoundIndex = 0;
            }

            for (int i = 0; i < battleSetting.PlayerTransforms.Count; i++) {
                var playerId = i;
                var playerTransform = battleSetting.PlayerTransforms[i];
                var selectedStrikerInfo = !playerSelectSetting.TryGetStriker(i, out var selectedStriker)
                    ? appStrikerRegistry.Default
                    : appStrikerRegistry.GetByStriker(selectedStriker);
                selectedStriker = selectedStrikerInfo.BattleStriker;
                if (selectedStrikerInfo.Prefab == null) {
                    Debug.LogError($"Striker prefab not found for {selectedStriker}");
                    continue;
                }
                var originalParent = playerTransform.parent;
                var originalPosition = playerTransform.position;
                var originalRotation = playerTransform.rotation;
                var instance = strikerHubFactory.Create(selectedStrikerInfo.Prefab, playerTransform, playerId);
                var runtimeAiBrain = CreateRuntimeAiBrain(playerId, selectedStriker);
                var inpactSubscription = instance.OnInpactGenerated.Subscribe(command => battlePresenter.PlayInpact(command));
                var attentionSubscription = instance.OnAtentionRequested.Subscribe(request => battlePresenter.RequestAttention(playerId, request));
                var specialRequestFailedSubscription = instance.OnSpecialRequestFailed.Subscribe(_ => {
                    AudioSource.PlayClipAtPoint(
                        battleSetting.SpecialUnavailableSound,
                        instance.Position.CurrentValue,
                        battleSetting.SpecialUnavailableSoundVolume);
                });

                strikerRegistry.RequestRegister(i, instance);

                deployedStrikers.Add(new DeployedStriker {
                    PlayerId = playerId,
                    PlayerTransform = playerTransform,
                    OriginalParent = originalParent,
                    OriginalPosition = originalPosition,
                    OriginalRotation = originalRotation,
                    Hub = instance,
                    RuntimeAiBrain = runtimeAiBrain,
                    InpactSubscription = inpactSubscription,
                    AttentionSubscription = attentionSubscription,
                    SpecialRequestFailedSubscription = specialRequestFailedSubscription,
                });

                Debug.Log($"Deployed Striker {selectedStriker} for Player {i}".ToCyan());
            }
        }

        void ApplyLearningSelections() {
            playerSelectSetting.SelectStriker(0, aiSetting.LearningPlayer1Striker);
            
            if (aiSetting.UseSelfPlay.CurrentValue) {
                playerSelectSetting.SelectStriker(1, aiSetting.LearningPlayer1Striker);
            } else {
                playerSelectSetting.SelectStriker(1, aiSetting.GetLearningOpponentStriker(learningRoundIndex));
            }
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
                    roundSubscriptions.Add(Observable.CombineLatest(aiSetting.IsLearning, aiSetting.UseSelfPlay, (isLearning, useSelfPlay) => {
                        if (playerId != 0 && !useSelfPlay) return false;
                        return isLearning;
                    }).Subscribe(shouldLearn => aiBrain.ApplyLearningMode(shouldLearn)));
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
                        aiBrain.DisableAiMode();
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
            var opponentStriker = ResolveOpponentStriker(playerId, selfStriker);
            if (!aiRegistry.TryResolve(selfStriker, opponentStriker, out var aiRegistration)) {
                Debug.LogWarning($"Failed to resolve AI brain registration. playerId={playerId}, self={selfStriker}, opponent={opponentStriker}. Configure fallbackAiId in AIRegistry inspector or add matching entry.");
                return null;
            }

            var aiBrain = UnityEngine.Object.Instantiate(aiRegistration.BrainPrefab, battleSetting.PlayerTransforms[playerId]);
            aiBrain.name = $"{aiRegistration.Id}_Player{playerId}";
            
            bool initialShouldLearn = aiSetting.IsLearning.CurrentValue;
            if (playerId != 0 && !aiSetting.UseSelfPlay.CurrentValue) {
                initialShouldLearn = false;
            }
            aiBrain.ApplyLearningMode(initialShouldLearn);
            
            aiBrain.DisableAiMode();
            return aiBrain;
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