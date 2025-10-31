

using System;
using Core.App.Types;

namespace Core.App.Presenters.Scene {

    public interface ISceneView {

        /// <summary>
        /// シーンを読み込む
        /// </summary>
        void LoadScene(AppScene scene, Action<AppScene> OnSceneLoadCompleted);
    }
}