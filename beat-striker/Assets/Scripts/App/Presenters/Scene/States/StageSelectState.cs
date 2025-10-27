

using Core.App.Presenters.Scene.Types;
using Core.App.Types;

namespace Core.App.Presenters.Scene.States {

    public class StageSelectState : ISceneState {
        private readonly SceneStateContext context;

        public StageSelectState(SceneStateContext context) {
            this.context = context;
        }

        private void OnAppFlowMessage(RequireTransitionMessage message) {
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

        private void OnStageSelected(SelectStageMessage message) {
            context.setting.Stage = message.stage;
        }

        private void OnTrackSelected(SelectTrackMessage message) {
            context.setting.Track = message.track;
        }

        public void Enter() {
            context.bus.Subscribe<RequireTransitionMessage>(OnAppFlowMessage);
            context.bus.Subscribe<SelectStageMessage>(OnStageSelected);
            context.bus.Subscribe<SelectTrackMessage>(OnTrackSelected);
        }

        public void Exit() {
            context.bus.Unsubscribe<RequireTransitionMessage>(OnAppFlowMessage);
            context.bus.Unsubscribe<SelectStageMessage>(OnStageSelected);
            context.bus.Unsubscribe<SelectTrackMessage>(OnTrackSelected);
        }
    }
}