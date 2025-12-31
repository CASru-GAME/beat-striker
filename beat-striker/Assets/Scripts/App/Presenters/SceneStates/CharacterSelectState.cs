using System;
using Core.App.Presenters.Scene.Types;
using Core.App.Interfaces;
using Core.App.Types;
using Core.Utils;
using UnityEngine;

namespace Core.App.Presenters.Scene.States {

    public class CharacterSelectState : ISceneState {
        private readonly SceneStateContext context;
        private readonly CompositeDisposable subscriptions = new();

        public CharacterSelectState(SceneStateContext context) {
            this.context = context;
        }

        private void OnRequireTransition(AppScene scene) {
            Debug.Log("CharacterSelectState received RequireTransition to " + scene);
            if (scene == AppScene.StageSelect) {
                context.controller.ChangeState(new TransitionState(
                    context,
                    AppScene.StageSelect
                ));
            }
            else if (scene == AppScene.Menu) {
                context.controller.ChangeState(new TransitionState(
                    context,
                    AppScene.Menu
                ));
            }
            else if (scene == AppScene.Battle) {
                context.controller.ChangeState(new TransitionState(
                    context,
                    context.setting.Stage.value == "Street" ? AppScene.Battle_Street : AppScene.Battle_Stage
                ));
            }
        }

        private void OnStrikerSelected(StrikerSelection selection) {
            context.setting.SetStriker(selection.playerId, selection.strikerId);
            foreach (var playerId in context.playerRegistry.GetAllPlayerIds()) {
                var striker = context.setting.GetStriker(playerId);
                if (striker == null) {
                    context.events.FireAllStrikersSelectedChanged(false);
                    return;
                }
            }
            context.events.FireAllStrikersSelectedChanged(true);
        }

        public void Enter() {
            context.cursorRegistry.SetCursorsActive(true);
            subscriptions.Add(context.events.SubscribeRequireTransition(OnRequireTransition));
            subscriptions.Add(context.events.SubscribeSelectStriker(OnStrikerSelected));
            context.events.FirePlayBGM(BGMType.MainBGM);
        }

        public void Exit() {
            subscriptions.Dispose();
        }
    }
}