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
        [SerializeField] AliceSpecialBarView specialBarUI;
        [SerializeField] AliceComboView comboView;
        [SerializeField] AliceRingView beatRingPrefab;
        [SerializeField] Transform beatRingParent;

        IStrikerRegistry strikerRegistry;
        IBeatjudge beatJudge;
        IMusicPlayer musicPlayer;
        CompositeDisposable disposables = new();
        IDisposable hpSubscription;
        IDisposable specialPointSubscription;
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
            comboView.SetComboCount(0);

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
            specialPointSubscription?.Dispose();
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
            comboView.SetComboCount(0);
        }

        void SetupPlayerSubscriptions() {
            var beatPlayer = beatJudge.GetBeatPlayer(playerId);

            beatPlayer.ComboCount
                .Subscribe(comboCount => {
                    comboView.SetComboCount(comboCount);
                })
                .AddTo(disposables);

            beatPlayer.OnBeatCommandRequested
                .Subscribe(result => {
                    if (!roundPlayable) return;
                    ringView.NotifyBeatRequested(result.IsSuccess);
                })
                .AddTo(disposables);

            beatPlayer.OnBeatPassed
                .Subscribe(result => {
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
            specialPointSubscription?.Dispose();
            hpSubscription = null;
            specialPointSubscription = null;
            strikerHub = null;
            comboView.SetComboCount(0);
            specialBarUI.SetSpecialRatio(0f);
        }

        void BindHpSubscriptionIfMatched(int registeredPlayerId, IStrikerHub registeredHub) {
            if (registeredPlayerId != playerId) return;

            hpSubscription?.Dispose();
            specialPointSubscription?.Dispose();
            strikerHub = registeredHub;
            hpBarUI.SetHpRatio(Mathf.Clamp01(registeredHub.HitPoint.CurrentValue / Mathf.Max(1f, registeredHub.MaxHitPoint.CurrentValue)));
            specialBarUI.SetSpecialRatio(Mathf.Clamp01(registeredHub.SpecialPoint.CurrentValue / Mathf.Max(1f, registeredHub.MaxSpecialPoint.CurrentValue)));
            hpSubscription = registeredHub.HitPoint.Subscribe(currentHp => {
                var maxHp = Mathf.Max(1f, registeredHub.MaxHitPoint.CurrentValue);
                hpBarUI.SetHpRatio(Mathf.Clamp01(currentHp / maxHp));
            });
            specialPointSubscription = registeredHub.SpecialPoint.Subscribe(currentSpecialPoint => {
                var maxSpecialPoint = Mathf.Max(1f, registeredHub.MaxSpecialPoint.CurrentValue);
                specialBarUI.SetSpecialRatio(Mathf.Clamp01(currentSpecialPoint / maxSpecialPoint));
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