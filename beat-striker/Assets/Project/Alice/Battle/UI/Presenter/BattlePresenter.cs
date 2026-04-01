using System.Threading.Tasks;
using UnityEngine;
using CorePlayerId = Core.App.Types.PlayerId;

namespace Alice {
    public interface IBattlePresenter {
        Task PlayBattleOpeningAsync();
        Task PlayRoundStartAsync(int roundNumber);
        void EnterRoundPlayablePhase();
        Task PlayRoundEndTransitionAsync();
        Task PlayRoundResumeTransitionAsync();
        Task PlayBattleEndingAsync(CorePlayerId winner);
    }

    public class BattlePresenter : MonoBehaviour, IBattlePresenter {
        [SerializeField] StageCamera stageCamera;
        [SerializeField] BattleRoundStartView roundStartPresenter;
        [SerializeField] BattleResultTextView resultTextPresenter;
        [SerializeField] BattleFadeView fadePresenter;

        public async Task PlayBattleOpeningAsync() {
            await Task.WhenAll(
                stageCamera.PresentIntroAsync(),
                fadePresenter.PresentFadeOutAsync());
        }

        public async Task PlayRoundStartAsync(int roundNumber) {
            await roundStartPresenter.PresentRoundStartAsync(roundNumber);
        }

        public void EnterRoundPlayablePhase() {
            stageCamera.PresentRoundPlayableStart();
        }

        public async Task PlayRoundEndTransitionAsync() {
            stageCamera.PresentRoundFinish();
            await fadePresenter.PresentFadeInAsync();
        }

        public async Task PlayRoundResumeTransitionAsync() {
            stageCamera.ResetRoundCamera();
            await fadePresenter.PresentFadeOutAsync();
        }

        public async Task PlayBattleEndingAsync(CorePlayerId winner) {
            stageCamera.PresentBattleFinish();
            await resultTextPresenter.PresentBattleFinishAsync();

            await Task.WhenAll(
                stageCamera.PresentOutroAsync(winner),
                resultTextPresenter.PresentOutroAsync());

            await fadePresenter.PresentFadeInAsync();
        }
    }
}