
using R3;
using System;
using System.Collections.Generic;
using App;
using Core.Striker;
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
    }

    public class BattleDeployer : IBattleDeployer, IDisposable {
        readonly BattleConfig config;
        readonly IStrikerRegistry strikerRegistry;
        readonly IStrikerFactory strikerHubFactory;
        readonly IGamePadRegistry gamePadRegistry;
        readonly IBeatjudge beatJudge;
        readonly List<IDisposable> subscriptions = new();

        public BattleDeployer(BattleConfig config, IStrikerRegistry strikerRegistry, IStrikerFactory strikerHubFactory, IGamePadRegistry gamePadRegistry, IBeatjudge beatJudge) {
            this.config = config;
            this.strikerRegistry = strikerRegistry;
            this.strikerHubFactory = strikerHubFactory;
            this.gamePadRegistry = gamePadRegistry;
            this.beatJudge = beatJudge;
        }

        public void Deploy() {
            DisposeSubscriptions();

            for (int i = 0; i < config.Strikers.Count; i++) {
                if (i >= config.PlayerTransforms.Count) break;
                var playerId = i;
                var transform = config.PlayerTransforms[i];
                var strikerEntry = config.StrikerEntries.Find(entry => entry.striker == config.Strikers[i]);
                if (strikerEntry.prefab == null) {
                    Debug.LogError($"Striker prefab not found for {config.Strikers[i]}");
                    continue;
                }
                var instance = strikerHubFactory.Create(strikerEntry.prefab, transform, playerId);

                strikerRegistry.RequestRegister(i, instance);

                var gamePad = gamePadRegistry.Get(playerId);
                subscriptions.Add(gamePad.HasGamePad.Subscribe(hasGamePad => {
                    instance.AiBrain.SetAiMode(!hasGamePad);
                    if (!hasGamePad) {
                        gamePadRegistry.RequestRegisterLowPriority(playerId, instance.AiBrain);
                    }
                }));

                subscriptions.Add(gamePad.OnDirection.Subscribe(direction => {
                    instance.ChangeDirection(direction);
                }));

                subscriptions.Add(gamePad.OnDirectionCanceled.Subscribe(_ => {
                    instance.CancelDirection();
                }));

                var beatPlayer = beatJudge.GetBeatPlayer(playerId);
                subscriptions.Add(beatPlayer.OnBeatExecuted.Subscribe(beatResult => {
                    switch (beatResult.Button) {
                        case GamePadButton.North:
                            instance.Special();
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

                subscriptions.Add(Disposable.Create(() => {
                    if (instance != null) {
                        instance.CancelDirection();
                    }
                }));


                Debug.Log($"Deployed Striker {config.Strikers[i]} for Player {i}".ToCyan());
            }
        }

        public void Dispose() {
            DisposeSubscriptions();
        }

        void DisposeSubscriptions() {
            foreach (var subscription in subscriptions) {
                subscription.Dispose();
            }
            subscriptions.Clear();
        }
    }


}