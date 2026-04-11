using System;
using System.Threading.Tasks;
using R3;
using UnityEngine;

namespace Alice {
    public interface IBattlePlayerPresenter {
        void PresentRoundPlayableStart();
        void PresentRoundPlayableFinish();
        Task PlayOpeningHpFillAsync();
    }

    public class BattlePlayerPresenter : IBattlePlayerPresenter, IDisposable {
        readonly BattlePlayerView battlePlayerView;
        readonly int playerId;
        readonly AliceHpBarView hpBarUI;
        readonly AliceSpecialBarView specialBarUI;
        readonly AliceComboView comboView;
        readonly IStrikerRegistry strikerRegistry;
        readonly IBeatjudge beatJudge;
        readonly IMusicPlayer musicPlayer;
        readonly Observable<bool> attentionActiveState;
        readonly CompositeDisposable disposables = new();
        readonly CompositeDisposable attentionSubscriptions = new();
        IDisposable hpSubscription;
        IDisposable specialPointSubscription;
        IStrikerHub strikerHub;
        readonly AliceRingView ringView;
        bool roundPlayable;
        bool isAttentionActive;

        public BattlePlayerPresenter(BattlePlayerView battlePlayerView, IStrikerRegistry strikerRegistry, IBeatjudge beatJudge, IMusicPlayer musicPlayer, Observable<bool> attentionActiveState, IPlayerSelectSetting playerSelectSetting, IAppStrikerRegistry appStrikerRegistry) {
            this.battlePlayerView = battlePlayerView;
            playerId = battlePlayerView.PlayerId;
            hpBarUI = battlePlayerView.HpBarUI;
            specialBarUI = battlePlayerView.SpecialBarUI;
            comboView = battlePlayerView.ComboView;
            this.strikerRegistry = strikerRegistry;
            this.beatJudge = beatJudge;
            this.musicPlayer = musicPlayer;
            this.attentionActiveState = attentionActiveState;
            ringView = UnityEngine.Object.Instantiate(battlePlayerView.BeatRingPrefab, battlePlayerView.BeatRingParent);

            comboView.SetComboCount(0);
            hpBarUI.ResetToZeroImmediately();

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

            this.attentionActiveState
                .Subscribe(OnAttentionActiveStateChanged)
                .AddTo(attentionSubscriptions);

            SetupPlayerSubscriptions();
            ringView.SetBeatTimeline(musicPlayer.CurrentBeatTimeline);
            PresentFrame(musicPlayer.CurrentViewPlaybackTime);

            foreach (var striker in strikerRegistry.GetAllStrikers()) {
                BindHpSubscriptionIfMatched(striker.PlayerId.CurrentValue, striker);
            }

            var strikerInfo = playerSelectSetting.TryGetStriker(playerId, out var selectedStriker)
                ? appStrikerRegistry.GetByStriker(selectedStriker)
                : appStrikerRegistry.Default;
            battlePlayerView.SetStrikerPortrait(strikerInfo.Portrait);
        }

        public void Dispose() {
            hpSubscription?.Dispose();
            specialPointSubscription?.Dispose();
            disposables.Dispose();
            attentionSubscriptions.Dispose();
            UnityEngine.Object.Destroy(ringView.gameObject);
        }

        public void PresentRoundPlayableStart() {
            roundPlayable = true;
            if (!isAttentionActive) {
                ringView.ActivateBattleView(playerId);
            }
        }

        public void PresentRoundPlayableFinish() {
            roundPlayable = false;
            ringView.DeactivateBattleView();
            comboView.SetComboCount(0);
        }

        public Task PlayOpeningHpFillAsync() {
            return battlePlayerView.PlayOpeningHpFillAsync();
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
                    ringView.NotifyBeatRequested(result.Zone);
                })
                .AddTo(disposables);

            musicPlayer.OnViewBeatTiming
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
            if (strikerHub == null) return;

            ringView.SetPosition(strikerHub.CenterPosition.CurrentValue);
            ringView.SetLookDirection(strikerHub.LookDirection.CurrentValue);
        }

        void OnAttentionActiveStateChanged(bool isActive) {
            isAttentionActive = isActive;

            if (isAttentionActive) {
                ringView.DeactivateBattleView();
                return;
            }

            if (roundPlayable) {
                ringView.ActivateBattleView(playerId);
            }
        }
    }
}