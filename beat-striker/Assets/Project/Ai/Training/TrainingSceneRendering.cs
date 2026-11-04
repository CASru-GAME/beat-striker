using UnityEngine;
using UnityEngine.Rendering;

namespace Alice {
    // The training scene owns its rendering budget without changing project quality assets.
    [DefaultExecutionOrder(-10000)]
    public sealed class TrainingSceneRendering : MonoBehaviour {
        [SerializeField] RenderPipelineAsset renderPipeline;

        RenderPipelineAsset previousRenderPipeline;

        void OnEnable() {
            previousRenderPipeline = QualitySettings.renderPipeline;
            QualitySettings.renderPipeline = renderPipeline;
        }

        void OnDisable() {
            QualitySettings.renderPipeline = previousRenderPipeline;
        }
    }
}
