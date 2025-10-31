
using Core.App.Presenters.Scene.Types;
using Core.App.Types;
using UnityEngine;

namespace Core.App.Presenters.Scene.States {

    public class CharacterSelectState : ISceneState {
        private readonly SceneStateContext context;

        public CharacterSelectState(SceneStateContext context) {
            this.context = context;
        }

        private void OnAppFlowMessage(AppMessages.RequireTransition message) {
            Debug.Log("CharacterSelectState received RequireTransition to " + message.scene);
            if (message.scene == AppScene.StageSelect) {
                context.controller.ChangeState(new TransitionState(
                    context,
                    AppScene.StageSelect
                ));
            }
            else if (message.scene == AppScene.Battle) {
                context.controller.ChangeState(new TransitionState(
                    context,
                    context.setting.Stage.value == "Street" ? AppScene.Battle_Street : AppScene.Battle_Stage
                ));
            }
        }

        private void OnStrikerSelected(AppMessages.SelectStriker message) {
            context.setting.SetStriker(message.playerId, message.striker);
        }

        public void Enter() {
            context.cursorRegistry.SetCursorsActive(true);
            context.bus.Subscribe<AppMessages.RequireTransition>(OnAppFlowMessage);
            context.bus.Subscribe<AppMessages.SelectStriker>(OnStrikerSelected);
        }

        public void Exit() {
            context.bus.Unsubscribe<AppMessages.RequireTransition>(OnAppFlowMessage);
            context.bus.Unsubscribe<AppMessages.SelectStriker>(OnStrikerSelected);
        }
    }
}