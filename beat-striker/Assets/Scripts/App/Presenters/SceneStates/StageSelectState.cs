

using Core.App.Presenters.Scene.Types;
using Core.App.Types;
using UnityEngine;

namespace Core.App.Presenters.Scene.States {

    public class StageSelectState : ISceneState {
        private readonly SceneStateContext context;

        public StageSelectState(SceneStateContext context) {
            this.context = context;
        }

        private void OnAppFlowMessage(AppMessages.RequireTransition message) {
            Debug.Log($"StageSelectState: Received RequireTransition to {message.scene}");
            if (message.scene == AppScene.Title) {
                Debug.Log("StageSelectState: Transitioning to Title");
                context.controller.ChangeState(new TransitionState(
                    context,
                    AppScene.Title
                ));
            }
            else if (message.scene == AppScene.Menu) {
                Debug.Log("StageSelectState: Transitioning to Menu");
                context.controller.ChangeState(new TransitionState(
                    context,
                    AppScene.Menu
                ));
            }
            else if (message.scene == AppScene.CharacterSelect) {
                Debug.Log("StageSelectState: Transitioning to CharacterSelect");
                context.controller.ChangeState(new TransitionState(
                    context,
                    AppScene.CharacterSelect
                ));
            }
            else {
                Debug.LogWarning($"StageSelectState: Unknown scene transition request: {message.scene}");
            }
        }

        private void OnStageSelected(AppMessages.SelectStage message) {
            context.setting.Stage = message.stage;
        }

        private void OnTrackSelected(AppMessages.SelectTrack message) {
            context.setting.Track = message.track;
        }

        public void Enter() {
            Debug.Log("StageSelectState: Entered StageSelectState");
            context.cursorRegistry.SetCursorsActive(true);
            Debug.Log("StageSelectState: Subscribing to RequireTransition, SelectStage, SelectTrack");
            context.bus.Subscribe<AppMessages.RequireTransition>(OnAppFlowMessage);
            context.bus.Subscribe<AppMessages.SelectStage>(OnStageSelected);
            context.bus.Subscribe<AppMessages.SelectTrack>(OnTrackSelected);
            context.bus.Publish(new AppMessages.PlayBGM(BGMType.MainBGM));
            Debug.Log("StageSelectState: Subscriptions complete");
        }

        public void Exit() {
            Debug.Log("StageSelectState: Exiting StageSelectState");
            context.bus.Unsubscribe<AppMessages.RequireTransition>(OnAppFlowMessage);
            context.bus.Unsubscribe<AppMessages.SelectStage>(OnStageSelected);
            context.bus.Unsubscribe<AppMessages.SelectTrack>(OnTrackSelected);
        }
    }
}