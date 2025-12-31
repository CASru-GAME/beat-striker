using UnityEngine;
using System.Collections.Generic;
using Core.Utils;
using Core.App.Presenters.Scene.Types;
using Core.App.Types;
using Core.App.Views;

namespace Core.App.Models {
    public interface IBGMManager {
        void PlayBGM(BGMType bgmType);
        void StopBGM();
    }

    public class BGMManager : IBGMManager {
        private readonly IBGMView view;
        private readonly IBus bus;

        public BGMManager(IBGMView view, IBus bus, ILife life) {
            this.view = view;
            this.bus = bus;
            
            life.Link(OnEnable, OnDisable);
        }

        private void OnEnable() {
            bus.Subscribe<AppMessages.PlayBGM>(OnPlayBGM);
            bus.Subscribe<AppMessages.StopBGM>(OnStopBGM);
        }

        private void OnDisable() {
            bus.Unsubscribe<AppMessages.PlayBGM>(OnPlayBGM);
            bus.Unsubscribe<AppMessages.StopBGM>(OnStopBGM);
        }

        private void OnPlayBGM(AppMessages.PlayBGM message) {
            PlayBGM(message.bgmType);
        }

        private void OnStopBGM(AppMessages.StopBGM message) {
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
