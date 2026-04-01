
using R3;
using System;
using System.Collections.Generic;
using App;
using Alice;
using UnityEngine;

namespace Alice {

    [System.Serializable]
    public struct StrikerPrefab {
        public Striker striker;
        public StrikerHub prefab;
    }

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

        readonly BattleConfig config;
        readonly IStrikerRegistry strikerRegistry;
        readonly IStrikerFactory strikerHubFactory;
        readonly IGamePadRegistry gamePadRegistry;
        readonly IBeatjudge beatJudge;
        readonly IBattlePresenter battlePresenter;
        readonly List<DeployedStriker> deployedStrikers = new();
        readonly List<IDisposable> roundSubscriptions = new();

        public BattleDeployer(BattleConfig config, IStrikerRegistry strikerRegistry, IStrikerFactory strikerHubFactory, IGamePadRegistry gamePadRegistry, IBeatjudge beatJudge, IBattlePresenter battlePresenter) {
            this.config = config;
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

            for (int i = 0; i < config.Strikers.Count; i++) {
                if (i >= config.PlayerTransforms.Count) break;
                var playerId = i;
                var playerTransform = config.PlayerTransforms[i];
                var strikerEntry = config.StrikerEntries.Find(entry => entry.striker == config.Strikers[i]);
                if (strikerEntry.prefab == null) {
                    Debug.LogError($"Striker prefab not found for {config.Strikers[i]}");
                    continue;
                }
                var originalParent = playerTransform.parent;
                var originalPosition = playerTransform.position;
                var originalRotation = playerTransform.rotation;
                var instance = strikerHubFactory.Create(strikerEntry.prefab, playerTransform, playerId);
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

                Debug.Log($"Deployed Striker {config.Strikers[i]} for Player {i}".ToCyan());
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

                if (deployed.Hub != null && deployed.Hub.Rigidbody != null) {
                    UnityEngine.Object.Destroy(deployed.Hub.Rigidbody.gameObject);
                }

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
                var requestedDirection = Vector2.zero;
                var hasRequestedDirection = false;
                if (aiBrain == null) {
                    Debug.LogError($"AiBrain not found for Player {playerId}");
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

                roundSubscriptions.Add(gamePad.OnDirection.Subscribe(direction => {
                    requestedDirection = direction;
                    hasRequestedDirection = true;
                }));

                roundSubscriptions.Add(gamePad.OnDirectionCanceled.Subscribe(_ => {
                    hasRequestedDirection = false;
                    requestedDirection = Vector2.zero;
                }));

                var beatPlayer = beatJudge.GetBeatPlayer(playerId);
                roundSubscriptions.Add(beatPlayer.OnBeatCommandRequested.Subscribe(beatResult => {
                    if (!beatResult.IsSuccess) {
                        return;
                    }

                    if (hasRequestedDirection) {
                        instance.ChangeDirection(requestedDirection);
                        return;
                    }

                    instance.CancelDirection();
                }));

                roundSubscriptions.Add(beatPlayer.OnBeatPassed.Subscribe(_ => {
                    if (hasRequestedDirection) {
                        instance.ChangeDirection(requestedDirection);
                        return;
                    }

                    requestedDirection = Vector2.zero;
                    instance.CancelDirection();
                }));

                roundSubscriptions.Add(beatPlayer.OnBeatCommandExecuted.Subscribe(beatResult => {
                    if (!beatResult.IsSuccess) {
                        return;
                    }

                    var specialPointGain = CalculateSpecialPointGain(beatResult.ComboCount);
                    instance.AddSpecialPoint(specialPointGain);

                    switch (beatResult.Button) {
                        case GamePadButton.North:
                            instance.Special();
                            break;
                        case GamePadButton.Left:
                            instance.Die();
                            break;
                        case GamePadButton.East:
                            instance.Attack();
                            break;
                        case GamePadButton.West:
                            instance.Charge();
                            break;
                        case GamePadButton.South:
                            instance.Dash();
                            break;
                        case GamePadButton.Right:
                            instance.Guard();
                            break;
                    }
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
            var combo1Gain = Mathf.Max(0f, config.Combo1SpecialPointGain);
            var convergenceRate = Mathf.Max(0f, config.SpecialPointGainConvergenceRate);
            var convergenceValue = Mathf.Max(combo1Gain, config.SpecialPointGainConvergenceValue);
            var x = combo - 1;
            return convergenceValue - (convergenceValue - combo1Gain) * Mathf.Exp(-convergenceRate * x);
        }
    }


}