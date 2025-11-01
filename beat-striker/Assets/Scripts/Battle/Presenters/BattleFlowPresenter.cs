
using Core.App.Types;
using Core.Utils;
using System.Collections.Generic;

namespace Core.Battle {
    public class BattleFlowPresenter : IBattleStateMutator {
        private IBattleState currentState;
        private readonly IBus bus;
        private readonly ILife life;
        private readonly IRythmTrackModel rythmTrackModel;
        private readonly IBattleResetter resetter;
        private readonly IBattleView view;
        private readonly TrackId trackId;

        public BattleFlowPresenter(IBus bus, ILife life, IBattleModel battleModel, IRythmTrackModel rythmTrackModel, IBattleResetter resetter, IBattleView view, TrackId trackId) {
            this.bus = bus;
            this.life = life;
            this.rythmTrackModel = rythmTrackModel;
            this.resetter = resetter;
            this.view = view;
            this.trackId = trackId;
            currentState = new IntroState(this, bus, battleModel, rythmTrackModel, resetter, view, trackId);
            life.Link(OnEnable, OnDisable);
        }

        public void DebugMode() {
            ChangeState(new RoundState(this, bus, null, rythmTrackModel, resetter, view, trackId));
        }

        public void OnUpdate(float deltaTime) {
            currentState.OnUpdate(deltaTime);
            
            // 見逃したビートを検出してイベント発行
            if (view.IsPlaying()) {
                var missedPlayers = rythmTrackModel.SetTime(view.GetAudioTime());
                if (missedPlayers.Count > 0) {
                    var missResult = new BeatResult(BeatStatus.Miss);
                    foreach (var playerId in missedPlayers) {
                        bus.Publish(new BattleMessages.OnBeat(playerId, missResult));
                    }
                }
            }
        }

        public void ChangeState(IBattleState newState) {
            currentState.Exit();
            currentState = newState;
            currentState.Enter();
        }

        public void OnEnable() {
            currentState.Enter();
        }

        public void OnDisable() {
            currentState.Exit();
        }
    }
}