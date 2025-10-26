

using System.Threading.Tasks;
using Core.App.Types;

namespace Core.App.Presenters.Scene {

    public interface ISceneView {
        Task LoadSceneAsync(AppScene scene);
        void StartTransitionAnimation();
    }
}