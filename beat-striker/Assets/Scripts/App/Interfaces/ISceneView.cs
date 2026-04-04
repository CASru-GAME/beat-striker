

using System;
using Core.App.Types;

namespace Core.App.Presenters.Scene {

    public interface ISceneView {

        /// <summary>
        /// シーンを読み込む
        /// </summary>
        void LoadScene(FAFA scene, Action<FAFA> OnSceneLoadCompleted);
    }
}