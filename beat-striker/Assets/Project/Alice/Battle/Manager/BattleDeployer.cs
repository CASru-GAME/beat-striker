
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
    }

    public class BattleDeployer : IBattleDeployer, IDisposable {
        class DeployedStriker {
            public int PlayerId;
            public Transform PlayerTransform;
            public Transform OriginalParent;
            public Vector3 OriginalPosition;
            public Quaternion OriginalRotation;
            public IStrikerHub Hub;
            public AiBrain AiBrain;
            public IDisposable InpactSubscription;
            public IDisposable AttentionSubscription;
        }

        readonly IBattleSetting battleSetting;
        readonly IBattleRuleSetting battleRuleSetting;
        readonly IPlayerSelectSetting playerSelectSetting;
        readonly IAppStrikerRegistry appStrikerRegistry;
        readonly IStrikerRegistry strikerRegistry;
        readonly IStrikerFactory strikerHubFactory;
        readonly IGamePadRegistry gamePadRegistry;
        readonly IBeatjudge beatJudge;
        readonly IBattlePresenter battlePresenter;
        readonly List<DeployedStriker> deployedStrikers = new();
        readonly List<IDisposable> roundSubscriptions = new();

        public BattleDeployer(IBattleSetting battleSetting, IBattleRuleSetting battleRuleSetting, IPlayerSelectSetting playerSelectSetting, IAppStrikerRegistry appStrikerRegistry, IStrikerRegistry strikerRegistry, IStrikerFactory strikerHubFactory, IGamePadRegistry gamePadRegistry, IBeatjudge beatJudge, IBattlePresenter battlePresenter) {
            this.battleSetting = battleSetting;
            this.battleRuleSetting = battleRuleSetting;
            this.playerSelectSetting = playerSelectSetting;
            this.appStrikerRegistry = appStrikerRegistry;
            this.strikerRegistry = strikerRegistry;
            this.strikerHubFactory = strikerHubFactory;
            this.gamePadRegistry = gamePadRegistry;
            this.beatJudge = beatJudge;
            this.battlePresenter = battlePresenter;
        }

        public void Deploy() {
            if (deployedStrikers.Count > 0) {
                Undeploy();
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
                var inpactSubscription = instance.OnInpactGenerated.Subscribe(command => battlePresenter.PlayInpact(command));
                var attentionSubscription = instance.OnAtentionRequested.Subscribe(request => battlePresenter.RequestAttention(playerId, request));

                strikerRegistry.RequestRegister(i, instance);

                deployedStrikers.Add(new DeployedStriker {
                    PlayerId = playerId,
                    PlayerTransform = playerTransform,
                    OriginalParent = originalParent,
                    OriginalPosition = originalPosition,
                    OriginalRotation = originalRotation,
                    Hub = instance,
                    AiBrain = instance.AiBrain,
                    InpactSubscription = inpactSubscription,
                    AttentionSubscription = attentionSubscription,
                });

                Debug.Log($"Deployed Striker {selectedStriker} for Player {i}".ToCyan());
            }
        }

        public void Undeploy() {
            DisconnectRoundInputs();

            foreach (var deployed in deployedStrikers) {
                strikerRegistry.RequestUnregister(deployed.PlayerId);

                if (deployed.PlayerTransform != null) {
                    deployed.PlayerTransform.SetParent(deployed.OriginalParent);
                    deployed.PlayerTransform.SetPositionAndRotation(deployed.OriginalPosition, deployed.OriginalRotation);
                }

                deployed.Hub?.DestroyGameObject();
                deployed.InpactSubscription?.Dispose();
                deployed.AttentionSubscription?.Dispose();
            }

            deployedStrikers.Clear();
        }

        public void ConnectRoundInputs() {
            DisconnectRoundInputs();

            foreach (var deployed in deployedStrikers) {
                var playerId = deployed.PlayerId;
                var instance = deployed.Hub;
                var aiBrain = deployed.AiBrain;
                if (instance == null) {
                    continue;
                }

                var gamePad = gamePadRegistry.Get(playerId);
                if (aiBrain == null) {
                    Debug.LogWarning($"AiBrain not found for Player {playerId}");
                }

                roundSubscriptions.Add(gamePad.HasGamePad.Subscribe(hasGamePad => {
                    if (aiBrain == null) {
                        return;
                    }

                    if (!hasGamePad) {
                        aiBrain.EnableAiMode(instance);
                        gamePadRegistry.RequestRegisterLowPriority(playerId, aiBrain);
                    } else {
                        aiBrain.DisableAiMode();
                    }
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
                        case GamePadButton.Start:
                            instance.Special();
                            break;
                        case GamePadButton.Select:
                            instance.Die();
                            break;
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

        public void Dispose() {
            Undeploy();
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