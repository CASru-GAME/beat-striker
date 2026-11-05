using System;
using System.Threading.Tasks;
using R3;
using VContainer;

namespace Alice {
    public class BattleTransitionPresenter : IDisposable {
        readonly BattleTransitionView view;
        readonly IPlayerSelectSetting playerSelectSetting;
        readonly IAppStrikerRegistry appStrikerRegistry;
        readonly PlayerSession session;
        readonly CompositeDisposable subscriptions = new();

        [Inject]
        public BattleTransitionPresenter(
            BattleTransitionView view,
            IPlayerSelectSetting playerSelectSetting,
            IAppStrikerRegistry appStrikerRegistry, PlayerSession session) {
            this.view = view;
            this.playerSelectSetting = playerSelectSetting;
            this.appStrikerRegistry = appStrikerRegistry;
            this.session = session;

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
            var leftThumbnail = ResolveThumbnail(0);
            var rightThumbnail = ResolveThumbnail(1);
            view.SetPortraits(leftThumbnail, rightThumbnail);
            string Identity(int slot) {
                if (slot >= session.Participants.Count) return "";
                var player = session.Participants[slot];
                return $"{player.name}\n{player.GradeLabel}" + (player.isCpu ? $" / CPU Lv{player.cpuLevel}" : "");
            }
            view.SetIdentities(Identity(0), Identity(1));
        }

        UnityEngine.Sprite ResolveThumbnail(int playerId) {
            if (playerSelectSetting.TryGetStriker(playerId, out var striker)) {
                return appStrikerRegistry.GetByStriker(striker).Thumbnail;
            }

            return appStrikerRegistry.Default.Thumbnail;
        }
    }
}
