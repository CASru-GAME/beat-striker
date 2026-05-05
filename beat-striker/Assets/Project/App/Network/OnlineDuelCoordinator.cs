using System.Threading.Tasks;
using VContainer;

namespace Alice {
    public interface IOnlineDuelCoordinator {
        Task NotifySceneReadyAsync(AppScene scene);
    }

    public class OnlineDuelCoordinator : IOnlineDuelCoordinator {
        readonly IOnlineDuelFusionClient duelClient;

        [Inject]
        public OnlineDuelCoordinator(IOnlineDuelFusionClient duelClient) {
            this.duelClient = duelClient;
        }

        public Task NotifySceneReadyAsync(AppScene scene) {
            return duelClient.NotifySceneReadyAsync(scene);
        }
    }
}
