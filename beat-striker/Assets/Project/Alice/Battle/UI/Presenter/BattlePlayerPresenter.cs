using System;
using R3;
using UnityEngine;

namespace Alice {
    public interface IBattlePlayerPresenter {
        void PresentRoundPlayableStart();
        void PresentRoundPlayableFinish();
    }

    public class BattlePlayerPresenter : MonoBehaviour, IBattlePlayerPresenter {
        [SerializeField] int playerId;
        [SerializeField] AliceHpBarView hpBarUI;
        [SerializeField] AliceRingView beatRingPrefab;
        [SerializeField] Transform beatRingParent;

        IStrikerRegistry strikerRegistry;
        IBeatjudge beatJudge;
        IMusicPlayer musicPlayer;
        CompositeDisposable disposables = new();
        IDisposable hpSubscription;
        IStrikerHub strikerHub;
        AliceRingView ringView;
        bool roundPlayable;

        [VContainer.Inject]
        public void Construct(IStrikerRegistry strikerRegistry, IBeatjudge beatJudge, IMusicPlayer musicPlayer) {
            this.strikerRegistry = strikerRegistry;
            this.beatJudge = beatJudge;
            this.musicPlayer = musicPlayer;
        }

        void Awake() {
            ringView = Instantiate(beatRingPrefab, beatRingParent);
        }

        void Start() {
            musicPlayer.OnBeatTimelinePrepared
                .Subscribe(ringView.SetBeatTimeline)
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
            ringView.SetBeatTimeline(musicPlayer.CurrentBeatTimeline);
            PresentFrame(musicPlayer.CurrentViewPlaybackTime);

            foreach (var striker in strikerRegistry.GetAllStrikers()) {
                BindHpSubscriptionIfMatched(striker.PlayerId.CurrentValue, striker);
            }
        }

        void OnDestroy() {
            hpSubscription?.Dispose();
            disposables.Dispose();
            Destroy(ringView.gameObject);
        }

        public void PresentRoundPlayableStart() {
            roundPlayable = true;
            ringView.ActivateBattleView();
        }

        public void PresentRoundPlayableFinish() {
            roundPlayable = false;
            ringView.DeactivateBattleView();
        }

        void SetupPlayerSubscriptions() {
            var beatPlayer = beatJudge.GetBeatPlayer(playerId);

            beatPlayer.OnBeatCommandRequested
                .Subscribe(result => {
                    if (!roundPlayable) return;
                    ringView.NotifyBeatRequested(result.IsSuccess);
                })
                .AddTo(disposables);

            beatPlayer.OnBeatPassed
                .Subscribe(_ => {
                    if (!roundPlayable) return;
                    ringView.NotifyBeatPassed();
                })
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
            hpBarUI.SetHpRatio(Mathf.Clamp01(registeredHub.HitPoint.CurrentValue / Mathf.Max(1f, registeredHub.MaxHitPoint.CurrentValue)));
            hpSubscription = registeredHub.HitPoint.Subscribe(currentHp => {
                var maxHp = Mathf.Max(1f, registeredHub.MaxHitPoint.CurrentValue);
                hpBarUI.SetHpRatio(Mathf.Clamp01(currentHp / maxHp));
            });
        }

        void PresentFrame(float playbackTime) {
            ringView.SetViewPlaybackTime(playbackTime);
            if (strikerHub != null) {
                ringView.SetPosition(strikerHub.CenterPosition.CurrentValue);
                ringView.SetLookDirection(strikerHub.LookDirection.CurrentValue);
            }
        }
    }
}