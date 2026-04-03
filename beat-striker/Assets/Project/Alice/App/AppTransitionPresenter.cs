using System.Threading.Tasks;
using UnityEngine;

namespace Alice {

    public record TransitionContext();

    public interface IAppTransitionPresenter {
        Task PresentTransitionOut(TransitionContext context);
        Task PresentTransitionIn(TransitionContext context);
        void DestroyGameObject();
    }


    public abstract class AppTransitionPresenter : MonoBehaviour, IAppTransitionPresenter {
        protected abstract Task PresentTransitionOut(TransitionContext context);
        protected abstract Task PresentTransitionIn(TransitionContext context);

        Task IAppTransitionPresenter.PresentTransitionOut(TransitionContext context) {
            return PresentTransitionOut(context);
        }

        Task IAppTransitionPresenter.PresentTransitionIn(TransitionContext context) {
            return PresentTransitionIn(context);
        }

        void IAppTransitionPresenter.DestroyGameObject() {
            Destroy(gameObject);
        }
    }
}
