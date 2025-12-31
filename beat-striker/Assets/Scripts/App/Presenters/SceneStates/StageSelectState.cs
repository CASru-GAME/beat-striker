using System;
using Core.App.Presenters.Scene.Types;
using Core.App.Types;
using Core.Utils;
using UnityEngine;

namespace Core.App.Presenters.Scene.States {

    public class StageSelectState : ISceneState {
        private readonly SceneStateContext context;
        private readonly CompositeDisposable subscriptions = new();

        public StageSelectState(SceneStateContext context) {
            this.context = context;
        }

        private void OnRequireTransition(AppScene scene) {
            Debug.Log($"StageSelectState: Received RequireTransition to {scene}");
            if (scene == AppScene.Title) {
                Debug.Log("StageSelectState: Transitioning to Title");
                context.controller.ChangeState(new TransitionState(
                    context,
                    AppScene.Title
                ));
            }
            else if (scene == AppScene.Menu) {
                Debug.Log("StageSelectState: Transitioning to Menu");
                context.controller.ChangeState(new TransitionState(
                    context,
                    AppScene.Menu
                ));
            }
            else if (scene == AppScene.CharacterSelect) {
                Debug.Log("StageSelectState: Transitioning to CharacterSelect");
                context.controller.ChangeState(new TransitionState(
                    context,
                    AppScene.CharacterSelect
                ));
            }
            else {
                Debug.LogWarning($"StageSelectState: Unknown scene transition request: {scene}");
            }
        }

        private void OnStageSelected(StageId stageId) {
            context.setting.Stage = stageId;
        }

        private void OnTrackSelected(TrackId trackId) {
            context.setting.Track = trackId;
        }

        public void Enter() {
            Debug.Log("StageSelectState: Entered StageSelectState");
            context.cursorRegistry.SetCursorsActive(true);
            Debug.Log("StageSelectState: Subscribing to RequireTransition, SelectStage, SelectTrack");
            subscriptions.Add(context.events.SubscribeRequireTransition(OnRequireTransition));
            subscriptions.Add(context.events.SubscribeSelectStage(OnStageSelected));
            subscriptions.Add(context.events.SubscribeSelectTrack(OnTrackSelected));
            context.events.FirePlayBGM(BGMType.MainBGM);
            Debug.Log("StageSelectState: Subscriptions complete");
        }

        public void Exit() {
            Debug.Log("StageSelectState: Exiting StageSelectState");
            subscriptions.Dispose();
        }
    }
}