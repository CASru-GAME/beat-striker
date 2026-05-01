using Alice;
using Fusion;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Alice {
    public interface IStrikerFactory {
        IStrikerHub Create(StrikerHub prefab, Transform playerTransform, int playerId);
    }

    public class StrikerHubFactory : IStrikerFactory {
        readonly IStrikerRegistry strikerRegistry;
        readonly IAppNetworkSetting appNetworkSetting;
        readonly INetworkRunnerProvider runnerProvider;
        readonly IGamePadRegistry gamePadRegistry;

        public StrikerHubFactory(IStrikerRegistry strikerRegistry, IAppNetworkSetting appNetworkSetting, INetworkRunnerProvider runnerProvider, IGamePadRegistry gamePadRegistry) {
            this.strikerRegistry = strikerRegistry;
            this.appNetworkSetting = appNetworkSetting;
            this.runnerProvider = runnerProvider;
            this.gamePadRegistry = gamePadRegistry;
        }

        public IStrikerHub Create(StrikerHub prefab, Transform playerTransform, int playerId) {
            if (appNetworkSetting.IsOnline.CurrentValue
                && runnerProvider.TryGetRunner(out var runner)
                && runner.IsRunning
                && runner.IsServer) {
                var networkPrefab = prefab.GetComponent<NetworkObject>();
                var inputAuthority = ResolveInputAuthority(runner, playerId);
                var networkObject = runner.Spawn(networkPrefab, playerTransform.position, playerTransform.rotation, inputAuthority, (_, spawned) => {
                    var hub = spawned.GetComponent<StrikerHub>();
                    var runtimeHub = hub.EnsureAliceRuntimeHub();
                    if (runtimeHub is AliceStrikerHub aliceRuntime) {
                        aliceRuntime.InitializeRuntimeDependencies(strikerRegistry);
                    }
                    runtimeHub.SetPlayerId(playerId);
                    var networkStriker = spawned.GetComponent<NetworkStriker>();
                    networkStriker.InitializeNetworkState(playerId, hub.InspectorStriker);
                });
                networkObject.transform.SetPositionAndRotation(playerTransform.position, playerTransform.rotation);
                playerTransform.SetParent(networkObject.transform);
                return networkObject.GetComponent<StrikerHub>().EnsureAliceRuntimeHub();
            }

            var instance = Object.Instantiate(prefab);
            instance.transform.SetPositionAndRotation(playerTransform.position, playerTransform.rotation);
            playerTransform.SetParent(instance.transform);
            var runtime = instance.EnsureAliceRuntimeHub();
            if (runtime is AliceStrikerHub localRuntime) {
                localRuntime.InitializeRuntimeDependencies(strikerRegistry);
            }
            runtime.SetPlayerId(playerId);
            return runtime;
        }

        PlayerRef ResolveInputAuthority(NetworkRunner runner, int playerId) {
            var gamePad = gamePadRegistry.Get(playerId);
            if (gamePad.HasGamePad.CurrentValue) {
                return runner.LocalPlayer;
            }

            foreach (var player in runner.ActivePlayers) {
                if (player != runner.LocalPlayer) {
                    return player;
                }
            }

            return PlayerRef.None;
        }
    }
}
        