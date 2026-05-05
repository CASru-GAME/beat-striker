using System.Threading.Tasks;
using UnityEngine;
using VContainer;

namespace Alice {
    public interface IOnlineDuelCoordinator {
        Task NotifySceneReadyAsync(AppScene scene);
        Task NotifyPlayerStatusAsync(OnlineDuelPlayerStatus status);
    }

    public class OnlineDuelCoordinator : IOnlineDuelCoordinator {
        const string LOG_PREFIX = "[OnlineDuelCoordinator]";

        readonly IOnlineDuelFusionClient duelClient;

        [Inject]
        public OnlineDuelCoordinator(IOnlineDuelFusionClient duelClient) {
            this.duelClient = duelClient;
        }

        public async Task NotifySceneReadyAsync(AppScene scene) {
            try {
                await duelClient.NotifySceneReadyAsync(scene);
            }
            catch (System.Exception exception) {
                Debug.LogWarning($"{LOG_PREFIX} Scene ready notification failed. scene={scene}, message={exception.Message}");
            }
        }

        public async Task NotifyPlayerStatusAsync(OnlineDuelPlayerStatus status) {
            try {
                await duelClient.NotifyPlayerStatusAsync(status);
            }
            catch (System.Exception exception) {
                Debug.LogWarning($"{LOG_PREFIX} Player status notification failed. status={status}, message={exception.Message}");
            }
        }
    }
}
