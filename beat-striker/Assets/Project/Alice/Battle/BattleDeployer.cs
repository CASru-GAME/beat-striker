
using R3;
using System;
using System.Collections.Generic;
using App;
using Core.Striker;
using UnityEngine;
using VContainer;

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
        readonly List<IDisposable> subscriptions = new();

        public BattleDeployer(BattleConfig config, IStrikerRegistry strikerRegistry, IStrikerFactory strikerHubFactory, IGamePadRegistry gamePadRegistry) {
            this.config = config;
            this.strikerRegistry = strikerRegistry;
            this.strikerHubFactory = strikerHubFactory;
            this.gamePadRegistry = gamePadRegistry;
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
                var instance = strikerHubFactory.Create(strikerEntry.prefab, transform);

                strikerRegistry.RequestRegister(i, instance);

                var gamePad = gamePadRegistry.Get(playerId);
                var subscription = gamePad.OnButtonDown.Subscribe(button => {
                    Debug.Log($"Player {playerId} pressed {button}");
                });
                subscriptions.Add(subscription);


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