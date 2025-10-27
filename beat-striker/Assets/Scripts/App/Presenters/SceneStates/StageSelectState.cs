

using Core.App.Presenters.Scene.Types;
using Core.App.Types;

namespace Core.App.Presenters.Scene.States {

    public class StageSelectState : ISceneState {
        private readonly SceneStateContext context;

        public StageSelectState(SceneStateContext context) {
            this.context = context;
        }

        private void OnAppFlowMessage(AppMessages.RequireTransition message) {
            if (message.command == TransitionRequire.StartExitAnimation) {
                context.controller.ChangeState(new TransitionState(
                    context,
                    AppScene.Title
                ));
            }
            else if (message.command == TransitionRequire.Next) {
                context.controller.ChangeState(new TransitionState(
                    context,
                    AppScene.CharacterSelect
                ));
            }
        }

        private void OnStageSelected(AppMessages.SelectStage message) {
            context.setting.Stage = message.stage;
        }

        private void OnTrackSelected(AppMessages.SelectTrack message) {
            context.setting.Track = message.track;
        }

        public void Enter() {
            context.bus.Subscribe<AppMessages.RequireTransition>(OnAppFlowMessage);
            context.bus.Subscribe<AppMessages.SelectStage>(OnStageSelected);
            context.bus.Subscribe<AppMessages.SelectTrack>(OnTrackSelected);
        }

        public void Exit() {
            context.bus.Unsubscribe<AppMessages.RequireTransition>(OnAppFlowMessage);
            context.bus.Unsubscribe<AppMessages.SelectStage>(OnStageSelected);
            context.bus.Unsubscribe<AppMessages.SelectTrack>(OnTrackSelected);
        }
    }
}