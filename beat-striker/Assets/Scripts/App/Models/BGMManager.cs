using UnityEngine;
using System;
using System.Collections.Generic;
using Core.Utils;
using Core.App.Presenters.Scene.Types;
using Core.App.Types;
using Core.App.Views;
using Core.App.Interfaces;

namespace Core.App.Models {
    public interface IBGMManager {
        void PlayBGM(BGMType bgmType);
        void StopBGM();
    }

    public class BGMManager : IBGMManager {
        private readonly IBGMView view;
        private readonly IAppModel appModel;
        private readonly CompositeDisposable subscriptions = new();

        public BGMManager(IBGMView view, IAppModel appModel, ILife life) {
            this.view = view;
            this.appModel = appModel;
            
            life.Link(OnEnable, OnDisable);
        }

        private void OnEnable() {
            subscriptions.Add(appModel.SubscribePlayBGM(OnPlayBGM));
            subscriptions.Add(appModel.SubscribeStopBGM(OnStopBGM));
        }

        private void OnDisable() {
            subscriptions.Dispose();
        }

        private void OnPlayBGM(BGMType bgmType) {
            PlayBGM(bgmType);
        }

        private void OnStopBGM() {
            StopBGM();
        }

        public void PlayBGM(BGMType bgmType) {
            view.PlayBGM(bgmType);
        }

        public void StopBGM() {
            view.StopBGM();
        }
    }
}
