using System;
using R3;
using UnityEngine;

namespace Alice {
    public interface IBattlePlayerPresenter {
        void PresentRoundPlayableStart();
    }

    public class BattlePlayerPresenter : MonoBehaviour, IBattlePlayerPresenter {
        [SerializeField] int playerId;
        [SerializeField] AliceHpBarUI hpBarUI;
        [SerializeField] AliceRingUI ringUI;

        IStrikerRegistry strikerRegistry;
        IBeatjudge beatJudge;
        IMusicPlayer musicPlayer;
        CompositeDisposable disposables = new();
        IDisposable hpSubscription;
        IStrikerHub strikerHub;

        [VContainer.Inject]
        public void Construct(IStrikerRegistry strikerRegistry, IBeatjudge beatJudge, IMusicPlayer musicPlayer) {
            this.strikerRegistry = strikerRegistry;
            this.beatJudge = beatJudge;
            this.musicPlayer = musicPlayer;
        }

        void Start() {
            musicPlayer.OnBeatTimelinePrepared
                .Subscribe(ringUI.SetBeatTimeline)
                .AddTo(disposables);

            musicPlayer.OnViewPlaybackTimeChanged
                .Subscribe(PresentFrame)
                .AddTo(disposables);

            strikerRegistry.OnRegistered
                .Subscribe(OnStrikerRegistered)
                .AddTo(disposables);

            strikerRegistry.OnUnregistered
                .Subscribe(OnStrikerUnregistered)
                .AddTo(disposables);

            SetupPlayerSubscriptions();
            ringUI.SetBeatTimeline(musicPlayer.CurrentBeatTimeline);
            PresentFrame(musicPlayer.CurrentViewPlaybackTime);

            foreach (var striker in strikerRegistry.GetAllStrikers()) {
                BindHpSubscriptionIfMatched(striker.PlayerId, striker);
            }
        }

        void OnDestroy() {
            hpSubscription?.Dispose();
            disposables.Dispose();
        }

        public void PresentRoundPlayableStart() {
            ringUI.ActivateBattleView();
        }

        void SetupPlayerSubscriptions() {
            var beatPlayer = beatJudge.GetBeatPlayer(playerId);

            beatPlayer.OnBeatCommandRequested
                .Subscribe(result => ringUI.NotifyBeatRequested(result.IsSuccess))
                .AddTo(disposables);

            beatPlayer.OnBeatPassed
                .Subscribe(_ => ringUI.NotifyBeatPassed())
                .AddTo(disposables);
        }

        void OnStrikerRegistered(StrikerRegistration registration) {
            BindHpSubscriptionIfMatched(registration.PlayerId, registration.Hub);
        }

        void OnStrikerUnregistered(StrikerUnregistration registration) {
            if (registration.PlayerId != playerId) return;

            hpSubscription?.Dispose();
            hpSubscription = null;
            strikerHub = null;
        }

        void BindHpSubscriptionIfMatched(int registeredPlayerId, IStrikerHub registeredHub) {
            if (registeredPlayerId != playerId) return;

            hpSubscription?.Dispose();
            strikerHub = registeredHub;
            hpSubscription = registeredHub.HitPointRatio.Subscribe(hpBarUI.SetHpRatio);
        }

        void PresentFrame(float playbackTime) {
            ringUI.SetViewPlaybackTime(playbackTime);
            if (strikerHub != null) {
                ringUI.SetPlayerWorldPosition(strikerHub.Position);
            }
        }
    }
}