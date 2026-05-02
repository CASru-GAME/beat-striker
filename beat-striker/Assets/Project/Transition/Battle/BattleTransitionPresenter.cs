using System;
using System.Threading.Tasks;
using R3;
using VContainer;

namespace Alice {
    public class BattleTransitionPresenter : IDisposable {
        readonly BattleTransitionView view;
        readonly IPlayerSelectSetting playerSelectSetting;
        readonly IAppStrikerRegistry appStrikerRegistry;
        readonly CompositeDisposable subscriptions = new();

        [Inject]
        public BattleTransitionPresenter(
            BattleTransitionView view,
            IPlayerSelectSetting playerSelectSetting,
            IAppStrikerRegistry appStrikerRegistry) {
            this.view = view;
            this.playerSelectSetting = playerSelectSetting;
            this.appStrikerRegistry = appStrikerRegistry;

            view.Bind(this);
            UpdatePortraits();

            playerSelectSetting.OnPlayerStrikerSelected
                .Subscribe(_ => UpdatePortraits())
                .AddTo(subscriptions);
        }

        public Task PresentTransitionOutAsync(TransitionContext context) {
            UpdatePortraits();
            return view.PlayTransitionOutAsync();
        }

        public Task PresentTransitionInAsync(TransitionContext context) {
            return view.PlayTransitionInAsync();
        }

        public void Dispose() {
            subscriptions.Dispose();
        }

        void UpdatePortraits() {
            var leftPortrait = ResolvePortrait(0);
            var rightPortrait = ResolvePortrait(1);
            view.SetPortraits(leftPortrait, rightPortrait);
        }

        UnityEngine.Sprite ResolvePortrait(int playerId) {
            if (playerSelectSetting.TryGetStriker(playerId, out var striker)) {
                return appStrikerRegistry.GetByStriker(striker).Portrait;
            }

            return appStrikerRegistry.Default.Portrait;
        }
    }
}
